using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IBApi;

namespace tws2uni
{
    using tws;
    class TwsConsumer : BackgroundService
    {
        public TwsConsumer(IRealTimeDataProvider realTimeDataProvider, IBackgroundTaskQueue queue, ILoggerFactory loggerFactory)
        {
            this.queue = queue;
            this.logger = loggerFactory.CreateLogger<TwsConsumer>();
            this.realTimeDataProvider = realTimeDataProvider;
        }

        private readonly IBackgroundTaskQueue queue;
        private readonly ILogger logger;
        private readonly IRealTimeDataProvider realTimeDataProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() => logger.LogInformation($"Stopping"));

            while (!stoppingToken.IsCancellationRequested)
            {
                queue.QueueBackgroundWorkItem(async token =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                });
                logger.LogInformation($"Added new task. all tasks: {queue.CountTask()}");

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
