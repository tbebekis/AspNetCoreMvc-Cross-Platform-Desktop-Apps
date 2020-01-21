using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using PuppeteerSharp;

namespace Tripous
{

    /// <summary>
    /// Helper class used in presenting Web applications as Desktop applications inside a Google Chrome window. 
    /// <para>A very light alternative to Electron and Electron.NET libraries.</para>
    /// <para>It uses the Chrome browser currently installed in the machine. </para>
    /// <para>Can handle two types of applications: 1. AspNet Core MVC and 2. an ordinary HTML Application comprised of static files (html, javascript and css)</para>
    /// <para>Uses the excellent PuppeteerSharp library found at https://github.com/kblok/puppeteer-sharp </para>
    /// <para>WARNING: For an AspNet Core MVC application to work properly, 
    /// the OutOfProcess hosting model should be defined in the *.csproj file with the AspNetCoreHostingModel directive.
    /// </para>
    /// <para>See the https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/aspnet-core-module for details regarding hosting models.</para>
    /// </summary>
    static public class Chrome
    {

        /* private */

        // https://peter.sh/experiments/chromium-command-line-switches/
        static readonly string[] DefaultArgs = {
            "--disable-background-networking",
            "--disable-background-timer-throttling",
            "--disable-client-side-phishing-detection",
            "--disable-default-apps",
            "--disable-extensions",
            "--disable-hang-monitor",
            "--disable-popup-blocking",
            "--disable-prompt-on-repost",
            "--disable-sync",
            "--disable-translate",
            "--metrics-recording-only",
            "--no-first-run",
            "--safebrowsing-disable-auto-update",
            "--force-app-mode",
            "--ipc-connection-timeout=5",

            "--aggressive-cache-discard",
            "--disable-cache",
            "--disable-application-cache",
            "--disable-offline-load-stale-cache",
            "--disk-cache-size=0",
        };
 

        static Dictionary<string, string> ImageContentTypes = new Dictionary<string, string>()
        {
            {"jpeg", "image/jpeg"}, {"jpg", "image/jpeg"}, {"svg", "image/svg+xml"}, {"gif", "image/gif"}, {"webp", "image/webp"},
            {"png", "image/png"}, {"ico", "image/ico"}, {"tiff", "image/tiff"}, {"tif", "image/tiff"}, {"bmp", "image/bmp"}
        };

        static Dictionary<string, string> FontContentTypes = new Dictionary<string, string>()
        {
            {"ttf", "font/opentype"}, {"otf", "font/opentype"}, {"ttc", "font/opentype"}, {"woff", "application/font-woff"}
        };

        const string SStaticApp = "StaticApp";
 
        /* private */
        /// <summary>
        /// Returns the path of the chrome.exe
        /// </summary>
        static string FindChromPath()
        {
            // TODO: https://github.com/GoogleChrome/chrome-launcher has code for finding Chrome in various OSes
            return @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        }
        /// <summary>
        /// Handles Chrome requests regarding static files, when this class is used with an ordinary HTML Application totally comprised of static files .
        /// </summary>
        static async void StaticRequestHandler(object sender, RequestEventArgs e)
        {

            ResponseData Data;

            /*
                         if (!IsStaticApp)
                        {


                            string HtmlText = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <title></title>
            </head>
            <body>
                <script type='text/javascript'>
                setTimeout(() => {{ window.location.href = '{HomeUrl}'; }}, 1500);
                </script>
            </body>
            </html>
            ";
                            Data = new ResponseData();
                            Data.ContentType = "text/html";
                            Data.Body = HtmlText;
                            Data.Status = System.Net.HttpStatusCode.OK;

                            await e.Request.RespondAsync(Data);

                            return;
                        }
                         */

 
            var url = new Uri(e.Request.Url);
            var FileName = url.LocalPath;
            if (FileName.StartsWith("/"))
                FileName = FileName.Remove(0, 1);
            FileName = FileName.Replace(@"/", @"\");

            int Index = FileName.LastIndexOf(".");
            string Extension = FileName.Substring(Index + 1);
            string FilePath = Path.Combine(RootFolder, FileName);
            if (!File.Exists(FilePath))
                //throw new ApplicationException($"File not found: {FilePath}");
                return;

            string ContentType;
            switch (e.Request.ResourceType)
            {
                case ResourceType.Document:
                    ContentType = "text/html";
                    break;
                case ResourceType.Script:
                    ContentType = "text/javascript";
                    break;
                case ResourceType.StyleSheet:
                    ContentType = "text/css";
                    break;
                case ResourceType.Image:
                    ContentType = ImageContentTypes.ContainsKey(Extension) ? ImageContentTypes[Extension] : "image/png";
                    break;
                case ResourceType.Font:
                    ContentType = FontContentTypes.ContainsKey(Extension) ? FontContentTypes[Extension] : "application/font-woff";
                    break;
                default:
                    throw new ApplicationException(string.Format("Not supported static resource type: {0}", e.Request.ResourceType.ToString()));
            }

            Data = new ResponseData();
            Data.ContentType = ContentType;

            if (e.Request.ResourceType == ResourceType.Image || e.Request.ResourceType == ResourceType.Font)
                Data.BodyData = File.ReadAllBytes(FilePath);
            else
                Data.Body = File.ReadAllText(FilePath);

            Data.Status = System.Net.HttpStatusCode.OK;

            await e.Request.RespondAsync(Data);

        }

        /* public */
        static public void Log(string Text)
        {
            string FilePath = Path.Combine(Path.GetDirectoryName(typeof(Chrome).Assembly.Location), "LOG.TXT");
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " ==> " + Text + Environment.NewLine;
            File.AppendAllText(FilePath, Text, Encoding.UTF8);
        }

        static public void Launch(ChromeStartOptions Options, Action Closed = null)
        {
            LaunchAsync(Options, Closed).GetAwaiter().GetResult();
        }
        /// <summary>
        /// Launches Chrome using the specified options 
        /// </summary>
        static public async Task LaunchAsync(ChromeStartOptions Options, Action Closed = null)
        {
            if (Browser == null)
            {
                // prepare options
                IsStaticApp = Options.Port == 0;
                if (!string.IsNullOrWhiteSpace(Options.StaticRootFolder))
                {
                    RootFolder = Path.GetFullPath(Options.StaticRootFolder);
                }

                Port = Options.Port;

                HomeUrl = IsStaticApp ? $@"http://{SStaticApp}/{Options.HomeUrl}" : $"http://localhost:{Port}"; 

                List<string> ArgList = new List<string>(DefaultArgs);

                // --app=data:text/html,TITLE
                string AppValue = IsStaticApp ? "data:text/html, loading..." : Chrome.HomeUrl;

                ArgList.Add($"--app={AppValue}");   // The --app= argument opens Chrome in app mode that is no fullscreen, no url bar, just the window
                ArgList.Add($"--window-size={Options.Width},{Options.Height}");
                ArgList.Add($"--window-position={Options.Left},{Options.Top}");

                LaunchOptions LaunchOptions = new LaunchOptions
                {
                    Devtools = false,
                    Headless = false,
                    Args = ArgList.ToArray(),
                    ExecutablePath = !string.IsNullOrWhiteSpace(Options.ChromePath) ? Options.ChromePath : FindChromPath(),
                    DefaultViewport = null
                };

                // launch Chrome
                Browser = await Puppeteer.LaunchAsync(LaunchOptions);

                // get the main tab page
                Page[] Pages = await Browser.PagesAsync().ConfigureAwait(false);
                TabPage = Pages[0];
 
                // event handler for static files
                if (IsStaticApp)
                {
                    await TabPage.SetRequestInterceptionAsync(true);
                    TabPage.Request += StaticRequestHandler;
                    await TabPage.GoToAsync(Chrome.HomeUrl, WaitUntilNavigation.DOMContentLoaded);
                }
 
                // event handler for close
                TabPage.Close += (sender, ea) => {
                    Closed?.Invoke();
                    Closed = null;
                    TabPage = null;
                    if (!Browser.IsClosed)
                        Browser.CloseAsync();
                    Browser = null;
                };
 
            }
        }
 

 

        /* properties */  
        static public BrowserContext Context { get; private set; }
        static public Browser Browser { get; private set; }
        static public Page TabPage { get; private set; }
        static public bool IsStarted { get { return TabPage != null; } }
        static public string HomeUrl { get; private set; }
        static public int Port { get; private set; }
        static public bool IsStaticApp { get; private set; }
        static public string RootFolder { get; private set; }
    }











    /// <summary>
    /// Options class for the <see cref="Chrome"/> static class.
    /// </summary>
    public class ChromeStartOptions
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public ChromeStartOptions()
        {
        }

        /* properties */
        /// <summary>
        /// The path where Chrome is installed in this machine.
        /// </summary>
        public string ChromePath { get; set; } = "";
        /// <summary>
        /// The home relative url.
        /// <para>For an AspNetCore MVC could be Home\Index</para>
        /// <para>For an HTML app with static files, could be Index.html</para>
        /// </summary>
        public string HomeUrl { get; set; }
        /// <summary>
        /// Optional. The initial X location of the browser
        /// </summary>
        public int Left { get; set; } = 300;
        /// <summary>
        /// Optional. The initial Y location of the browser
        /// </summary>
        public int Top { get; set; } = 150;
        /// <summary>
        /// Optional. The initial width of the browser
        /// </summary>
        public int Width { get; set; } = 1024;
        /// <summary>
        /// Optional. The initial height of the browser
        /// </summary>
        public int Height { get; set; } = 768;
        /// <summary>
        /// Optional. A root folder where static files (html, js, css) are placed.
        /// <para>NOTE: Used only with a "static" app.</para>
        /// </summary>
        public string StaticRootFolder { get; set; } = "wwwroot";
        /// <summary>
        /// Required. The SSL Port. It is defined in the launchSettings.json file.
        /// <para>NOTE: Used only with an AspNetCore MVC app.</para>
        /// </summary>
        public int Port { get; set; }
    }


}
