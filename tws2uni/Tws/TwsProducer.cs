using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;

namespace Tws2UniFeeder
{
    class TwsProducer : BackgroundService
    {
        public TwsProducer(IOptions<TwsOption> option, ITwsProvider provider, ILoggerFactory loggerFactory)
        {
            this.option = option.Value;
            this.provider = provider;
            this.logger = loggerFactory.CreateLogger<TwsProducer>();
        }

        private readonly TwsOption option;
        private readonly ITwsProvider provider;
        private readonly ILogger logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                provider.Disconnect();
            });

            foreach (var contract in option.Mapping)
                provider.SubscribeMktData(contract.Key, contract.Value);

            await Loop(stoppingToken);
        }

        protected async Task Loop(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (!provider.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation("Connecting to {Host} ...", $"{option.Host}:{option.Port}");

                    provider.Connect(option.Host, option.Port, option.ClientID, stoppingToken);

                    //logger.LogInformation("Connected success to {Host} ...", $"{option.Host}:{option.Port}");
                    await Task.Delay(TimeSpan.FromSeconds(option.ReconnectPeriodSecond), stoppingToken).ContinueWith(p => { });
                }
            }
        }
    }
}
