using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
 
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;

using PuppeteerSharp;
using Tripous; 

namespace PuppetMvc
{
    // https://stackoverflow.com/questions/50935730/asp-net-core-2-1-kestrel-how-to-disable-https
    // https://gunnarpeipman.com/dotnet-core-self-contained-executable/
    // dotnet publish -c Release -r win10-x64 /p:PublishSingleFile=true
    public class Program
    {

        public static void Main(string[] args)
        {
            CreateHostBuilder(args)
                .Build() 
                .Run();
        } 

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {                    
                    webBuilder.ConfigureKestrel(o =>
                    {
                        o.Listen(IPAddress.Loopback, 5000); //HTTP port
                    })
                    .UseStartup<Startup>();
                });

 
    }

 
}
