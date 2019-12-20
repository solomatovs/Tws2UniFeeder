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

            while(!stoppingToken.IsCancellationRequested)
            {
                while (!provider.IsConnected && !stoppingToken.IsCancellationRequested)
                {
                    await ConnectAndSubscribe(stoppingToken);
                }
            }
        }

        protected async Task ConnectAndSubscribe(CancellationToken stoppingToken)
        {
            while (!provider.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    provider.Connect(option.Host, option.Port, option.ClientID);
                }
                catch (Exception e)
                {
                    logger.LogError($"Connection failed: {e.Message}");
                }
                finally
                {
                    await Task.Delay(5000, stoppingToken);
                }
            }

            if (!stoppingToken.IsCancellationRequested)
                foreach (var contract in option.Mapping)
                    provider.SubscribeTickByTick(contract.Value);
        }
    }
}
