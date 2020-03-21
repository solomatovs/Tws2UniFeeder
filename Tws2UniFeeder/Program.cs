using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Tws2UniFeeder
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureHostConfiguration(c => c.AddEnvironmentVariables())
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json",      optional: true, reloadOnChange: true);
                    config.AddJsonFile("appsettings.dev.json",  optional: true, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                    config.AddUserSecrets(assembly: Assembly.GetExecutingAssembly(), optional: true, reloadOnChange: true);
                    config.AddCommandLine(args);
                })
                .ConfigureLogging((hostContext, logging) =>
                {
                    logging.AddSerilog((new LoggerConfiguration()).ReadFrom.Configuration(hostContext.Configuration).CreateLogger(), dispose: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddOptions();

                    services.Configure<TwsOption>(option => hostContext.Configuration.GetSection("Tws").Bind(option));
                    services.Configure<TwsWatchDogOption>(option => hostContext.Configuration.GetSection("WatchDog").Bind(option));
                    services.Configure<UniFeederOption>(option => hostContext.Configuration.GetSection("UniFeeder").Bind(option));

                    services.AddSingleton<IBackground<Quote>,   BackgroundQueue<Quote>>();
                    services.AddSingleton<IBackground<string>,  BackgroundQueue<string>>();

                    services.AddHostedService<UniFeedConsumer>();
                    services.AddHostedService<TwsProducer>();
                    services.AddHostedService<TwsWatchDog>();
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
