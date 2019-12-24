using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Configuration;

namespace Tws2UniFeeder
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(hostContext.HostingEnvironment.ContentRootPath);
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    config.AddCommandLine(args);
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.AddSerilog((new LoggerConfiguration()).ReadFrom.Configuration(hostContext.Configuration).CreateLogger() , dispose: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.Configure<TwsOption>(option => hostContext.Configuration.GetSection("Tws").Bind(option));
                    services.Configure<UniFeederOption>(option => hostContext.Configuration.GetSection("UniFeeder").Bind(option));

                    services.AddSingleton<IBackgroundQueue<Quote>, BackgroundQueue<Quote>>();
                    services.AddSingleton<IBackgroundQueue<Tick>,  BackgroundQueue<Tick>>();
                    services.AddSingleton<ITwsProvider, TwsProvider>();

                    services.AddHostedService<UniFeedConsumer>();
                    services.AddHostedService<TwsProducer>();
                })
                .Build();

            using (host)
            {
                await host.StartAsync();

                await host.WaitForShutdownAsync();

                await host.StopAsync();
            }
        }
    }
}
