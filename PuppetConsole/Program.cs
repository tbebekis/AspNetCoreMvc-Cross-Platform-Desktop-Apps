using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

using PuppeteerSharp;
using Tripous;

namespace PuppetConsole
{
    class Program
    {
 
        static void Main(string[] args)
        {
            ChromeStartOptions Options = new ChromeStartOptions()
            {
                HomeUrl = @"Index.html"
            };

            ManualResetEvent CloseEvent = new ManualResetEvent(false);
            Chrome.Launch(Options, () => {
                CloseEvent.Set();
            });
            CloseEvent.WaitOne();

        }



 
    }
}
