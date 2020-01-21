using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net.NetworkInformation;
using System.Net;

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
        #region Constants
        /// <summary>
        /// Default command line switches
        /// <para>FROM: https://peter.sh/experiments/chromium-command-line-switches/</para>
        /// </summary>
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

        #endregion

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

            Uri Uri = new Uri(e.Request.Url);
            string FileName = Uri.LocalPath;

            if (string.IsNullOrWhiteSpace(FileName) || FileName == "/")
            {
                Uri = new Uri(HomeUrl);
                FileName = Uri.LocalPath;
            }                

            if (FileName.StartsWith("/"))
                FileName = FileName.Remove(0, 1);
            FileName = FileName.Replace(@"/", @"\");

            int Index = FileName.LastIndexOf(".");
            string Extension = FileName.Substring(Index + 1);
            string FilePath = Path.Combine(ContentFolder, FileName);
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

        /* construction */
        /// <summary>
        /// Static constructor
        /// </summary>
        static Chrome()
        {
            Port = GetNextFreePort();
        }


        /* public */
        /// <summary>
        /// Writes log to a file. Used for debugging.
        /// </summary>
        static public void Log(string Text)
        {
            string FilePath = Path.Combine(Path.GetDirectoryName(typeof(Chrome).Assembly.Location), "LOG.TXT");
            Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " ==> " + Text + Environment.NewLine;
            File.AppendAllText(FilePath, Text, Encoding.UTF8);
        }
        /// <summary>
        /// Returns the next free port.
        /// <para>FROM: https://gist.github.com/jrusbatch/4211535 </para>
        /// </summary>
        static public int GetNextFreePort(int startingPort = 4500)
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();

            //getting active connections
            var tcpConnectionPorts = properties.GetActiveTcpConnections()
                                .Where(n => n.LocalEndPoint.Port >= startingPort)
                                .Select(n => n.LocalEndPoint.Port);

            //getting active tcp listners - WCF service listening in tcp
            var tcpListenerPorts = properties.GetActiveTcpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            //getting active udp listeners
            var udpListenerPorts = properties.GetActiveUdpListeners()
                                .Where(n => n.Port >= startingPort)
                                .Select(n => n.Port);

            var port = Enumerable.Range(startingPort, ushort.MaxValue)
                .Where(i => !tcpConnectionPorts.Contains(i))
                .Where(i => !tcpListenerPorts.Contains(i))
                .Where(i => !udpListenerPorts.Contains(i))
                .FirstOrDefault();

            return port;
        }

        /// <summary>
        /// Launches Chrome using the specified options 
        /// </summary>
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
                IsAspNetCoreApp = Options.IsAspNetCoreApp;

                if (!string.IsNullOrWhiteSpace(Options.ContentFolder))
                {
                    ContentFolder = Path.GetFullPath(Options.ContentFolder);
                } 

                HomeUrl = !IsAspNetCoreApp ? $@"http://{SStaticApp}/{Options.HomeUrl}" : $"http://localhost:{Port}"; 

                List<string> ArgList = new List<string>(DefaultArgs);

                // --app=data:text/html,TITLE
                string AppValue = !IsAspNetCoreApp ? "data:text/html, loading..." : Chrome.HomeUrl;

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
                if (!IsAspNetCoreApp)
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
        /// <summary>
        /// True after the browser is constructed,
        /// </summary>
        static public bool IsStarted { get { return TabPage != null; } }
        /// <summary>
        /// The <see cref="BrowserContext"/> of the browser.
        /// </summary>
        static public BrowserContext Context { get; private set; }
        /// <summary>
        /// The browser
        /// </summary>
        static public Browser Browser { get; private set; }
        /// <summary>
        /// The single tab page of the browser where content is displayed.
        /// </summary>
        static public Page TabPage { get; private set; }
 
        /// <summary>
        /// When true, indicates that this is an AspNet Core app.
        /// <para>When false, indicates that this is a normal HTML "static" app, comprised of static files (html, javascript and css) </para>
        /// </summary>
        static public bool IsAspNetCoreApp { get; private set; }
        /// <summary>
        /// For non AspNet Core apps only. 
        /// <para>The home relative url, e.g. Default.html, Index.html or Home.html.</para>
        /// </summary>
        static public string HomeUrl { get; private set; }
        /// <summary>
        /// Optional. For non AspNet Core apps only. 
        /// <para> Indicates the root content folder where static files (html, js, css) are placed.</para>
        /// </summary>
        static public string ContentFolder { get; private set; }
        /// <summary>
        /// For non AspNet Core apps only. 
        /// <para>The Port where the Kestrel server should listen. It is computed automatically.</para>
        /// </summary>
        static public int Port { get; private set; }
    }

 


    /// <summary>
    /// Options class for the <see cref="Chrome"/> static class.
    /// </summary>
    public class ChromeStartOptions
    {

        /// <summary>
        /// Constructor
        /// </summary>
        public ChromeStartOptions(bool IsAspNetCoreApp = true)
        {
            this.IsAspNetCoreApp = IsAspNetCoreApp;
        }

        /* properties */
        /// <summary>
        /// When true, the default, indicates that this is an AspNet Core app.
        /// <para>When false, indicates that this is a normal HTML "static" app, comprised of static files (html, javascript and css) </para>
        /// </summary>
        public bool IsAspNetCoreApp { get; set; } = true;
        /// <summary>
        /// The path where Chrome is installed in this machine.
        /// </summary>
        public string ChromePath { get; set; } = "";
        /// <summary>
        /// For non AspNet Core apps only. 
        /// <para>The home relative url, e.g. Default.html, Index.html or Home.html.</para>
        /// </summary>
        public string HomeUrl { get; set; } = @"Index.html";
        /// <summary>
        /// Optional. For non AspNet Core apps only. 
        /// <para> Indicates the root content folder where static files (html, js, css) are placed.</para>
        /// </summary>
        public string ContentFolder { get; set; } = "wwwroot";
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


    }


}
