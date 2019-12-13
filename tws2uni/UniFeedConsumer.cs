using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using IBApi;

namespace tws2uni
{
    using tws;
    public class UniFeedConsumer : BackgroundService
    {
        private readonly ILogger logger;

        public UniFeedConsumer(IBackgroundTaskQueue<TwsTick> taskQueue, ILoggerFactory loggerFactory)
        {
            TaskQueue = taskQueue;
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        public IBackgroundTaskQueue<TwsTick> TaskQueue { get; }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("UniFeedConsumer is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var tick = await TaskQueue.DequeueAsync(cancellationToken);
                logger.LogInformation($"{tick}");
            }

            logger.LogInformation("HUniFeedConsumer is stopping.");
        }
    }
}
