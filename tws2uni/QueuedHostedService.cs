using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace tws2uni
{
    public class QueuedHostedService : BackgroundService
    {
        private readonly ILogger logger;

        public QueuedHostedService(IBackgroundTaskQueue<Func<CancellationToken, Task>> taskQueue, ILoggerFactory loggerFactory)
        {
            TaskQueue = taskQueue;
            logger = loggerFactory.CreateLogger<QueuedHostedService>();
        }

        public IBackgroundTaskQueue<Func<CancellationToken, Task>> TaskQueue { get; }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Hosted Service is starting.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var workItem = await TaskQueue.DequeueAsync(cancellationToken);

                try
                {
                    await workItem(cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred executing {WorkItem}.", nameof(workItem));
                }
            }

            logger.LogInformation("Hosted Service is stopping.");
        }
    }
}
