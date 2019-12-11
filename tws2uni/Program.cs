using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace tws2uni
{
    using tws;
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddCommandLine(args);
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole(c =>
                    {
                        c.Format = Microsoft.Extensions.Logging.Console.ConsoleLoggerFormat.Systemd;
                    });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();
                    services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
                    services.AddSingleton<IRealTimeDataProvider, RealTimeDataProvider>();
                    services.AddHostedService<QueuedHostedService>();
                    services.AddHostedService<TwsConsumer>();
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
