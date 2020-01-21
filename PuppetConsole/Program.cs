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
            ManualResetEvent CloseEvent = new ManualResetEvent(false);            
            Chrome.Launch(new ChromeStartOptions(false), () => {
                CloseEvent.Set();
            });            
            CloseEvent.WaitOne();
        }
 
    }
}
