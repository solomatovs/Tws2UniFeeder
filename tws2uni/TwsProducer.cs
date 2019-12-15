using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;

namespace tws2uni
{
    using tws;
    class TwsProducer : BackgroundService
    {
        public TwsProducer(IOptions<TwsOption> option, IRealTimeDataProvider provider, ILoggerFactory loggerFactory)
        {
            this.option = option.Value;
            this.provider = provider;
            this.logger = loggerFactory.CreateLogger<TwsProducer>();
        }

        private readonly TwsOption option;
        private readonly IRealTimeDataProvider provider;
        private readonly ILogger logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var contracts = option.Contracts;

            stoppingToken.Register(() =>
            {
                logger.LogInformation("Stoping...");
                provider.Disconnect();
                logger.LogInformation("Stoped");
            });

            provider.Connect(option.Host, option.Port, option.ClientID, autoReconnect: true);

            foreach (var contract in contracts)
                provider.SubscribeTickByTick(contract);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
