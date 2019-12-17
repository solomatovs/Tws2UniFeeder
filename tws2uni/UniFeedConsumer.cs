using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
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
            this.server = (new IPEndPoint(IPAddress.Parse("0.0.0.0"), 5001)).CreateRxSocketServer(loggerFactory.CreateLogger<RxSocketServer>());
            this.taskQueue = taskQueue;
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await UniFeederServerLoop(this.server, cancellationToken);
            this.server.Dispose();
        }

        protected async Task UniFeederServerLoop(IRxSocketServer server, CancellationToken cancellationToken)
        {
            server.AcceptObservable.Subscribe(onNext: accept =>
            {
                @"> Universal DDE Connector 9.00
> Copyright 1999 - 2008 MetaQuotes Software Corp.
> Login: ".ToByteArray().SendTo(accept);

                accept.ReceiveObservable.ToUniFeederStrings().Subscribe(login =>
                {
                    logger.LogInformation($"login reveived: '{login}'");

                    accept.Send("> Password: ".ToByteArray());
                    accept.ReceiveObservable.ToUniFeederStrings().Subscribe(onNext: async password =>
                    {
                        logger.LogInformation($"password reveived: '{password}'");
                        accept.Send("> Access granted".ToByteArray());

                        await ProccessLoop(cancellationToken);
                    });
                });
            }, cancellationToken);

            await server.AcceptObservable.ToTask(cancellationToken);
        }

        protected async Task ProccessLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var tick = await taskQueue.DequeueAsync(token);
                logger.LogInformation(tick.ToString());
            }
        }
    }
}
