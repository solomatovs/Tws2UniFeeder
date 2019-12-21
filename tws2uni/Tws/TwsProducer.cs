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
                var taskConnet = Connect(stoppingToken);
                var taskSubscribe = Subscribe(stoppingToken);

                Task.WaitAll(taskConnet, taskConnet);
            }
        }

        protected async Task Connect(CancellationToken stoppingToken)
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
                    await Task.Delay(option.ReconnectPeriodSecond * 1000, stoppingToken);
                }
            }
        }

        protected async Task Subscribe(CancellationToken stoppingToken)
        {
            while (provider.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                foreach (var contract in option.Mapping)
                    provider.SubscribeTickByTick(contract.Value);

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
