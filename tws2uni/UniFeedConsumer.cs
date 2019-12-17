using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using RxSockets;

namespace tws2uni
{
    using tws;
    public class UniFeedConsumer : BackgroundService
    {
        private readonly ILogger logger;
        private readonly IRxSocketServer server;
        private readonly IBackgroundQueue<TwsTick> taskQueue;

        public UniFeedConsumer(IBackgroundQueue<TwsTick> taskQueue, ILoggerFactory loggerFactory)
        {
            this.server = (new IPEndPoint(IPAddress.Parse("0.0.0.0"), 3309)).CreateRxSocketServer(loggerFactory.CreateLogger<RxSocketServer>());
            this.taskQueue = taskQueue;
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        public async override Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() => BootstrapServer(server));

            
        }

        public async override Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() => server.Dispose());

            
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("UniFeedConsumer is starting.");
            await base.StartAsync(cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var tick = await taskQueue.DequeueAsync(cancellationToken);
                logger.LogInformation($"{tick}");
            }

            await base.StopAsync(cancellationToken);
            logger.LogInformation("UniFeedConsumer is stopping.");
        }

        protected void BootstrapServer(IRxSocketServer server)
        {
            // Start accepting connections from clients.
            server.AcceptObservable.Subscribe(
                onNext: async acceptClient => {
                    var hello_message = @"Universal DDE Connector 9.00
Copyright 1999 - 2008 MetaQuotes Software Corp.
Login: ";
                    acceptClient.Send(hello_message.ToByteArray());

                    var t = await acceptClient.ReceiveObservable.ToStrings().Take(1).FirstAsync();
                    logger.LogInformation(t);
                    acceptClient.ReceiveObservable.ToStrings().Subscribe(message =>
                    {
                        // Echo received messages back to the client.
                        // acceptClient.Send(message.ToByteArray());
                        logger.LogInformation(message);
                    }
                    );

                },
                onError: error => {
                    logger.LogInformation(error.Message);
                },
                onCompleted: () => logger.LogInformation("complite")
            );
        }
    }
}
