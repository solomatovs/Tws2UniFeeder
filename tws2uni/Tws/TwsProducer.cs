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
            var contracts = option.Mapping;

            stoppingToken.Register(() =>
            {
                logger.LogInformation("Stoping...");
                provider.Disconnect();
                logger.LogInformation("Stoped");
            });

            while(!provider.IsConnected && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    provider.Connect(option.Host, option.Port, option.ClientID, autoReconnect: true);
                }
                catch(Exception e)
                {
                    logger.LogError($"Connection failed: {e.Message}");
                }
                finally
                {
                    await Task.Delay(5000, stoppingToken);
                }
            }
            
            if (!stoppingToken.IsCancellationRequested)
            foreach (var contract in contracts)
                provider.SubscribeTickByTick(contract.Value);

            await Task.Delay(-1, stoppingToken);
        }
    }
}
