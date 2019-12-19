using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tws2UniFeeder
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddCommandLine(args);
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole(c =>
                    {
                        c.Format = Microsoft.Extensions.Logging.Console.ConsoleLoggerFormat.Default;
                    });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.Configure<TwsOption>(option => hostContext.Configuration.GetSection("Tws").Bind(option));
                    services.Configure<UniFeederOption>(option => hostContext.Configuration.GetSection("UniFeeder").Bind(option));

                    services.AddSingleton<IBackgroundQueue<Quote>, BackgroundQuoteQueue>();
                    services.AddSingleton<ITwsProvider, TwsProvider>();

                    services.AddHostedService<TwsProducer>();
                    services.AddHostedService<UniFeedConsumer>();
                })
                .Build();

            using (host)
            {
                Console.WriteLine("Starting!");
                await host.StartAsync();

                Console.WriteLine("Started! Press ctrl+c to stop.");
                await host.WaitForShutdownAsync();

                Console.WriteLine("Stopping!");

                await host.StopAsync();
                Console.WriteLine("Stopped!");
            }
        }
    }
}
