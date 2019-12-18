using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Reactive.Linq;
using RxSockets;


namespace Tws2UniFeeder
{
    public class UniFeedConsumer : BackgroundService
    {
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IBackgroundQueue<Quote> queue;
        private readonly IOptions<UniFeederOption> option;

        public UniFeedConsumer(IOptions<UniFeederOption> option, IBackgroundQueue<Quote> queue, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.queue = queue;
            this.loggerFactory = loggerFactory;
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var end = new IPEndPoint(IPAddress.Parse(option.Value.Ip), option.Value.Port);

            using (var server = end.CreateRxSocketServer(loggerFactory.CreateLogger<RxSocketServer>()))
            {
                UniFeederServer(server, cancellationToken);
                
                await Task.Delay(-1, cancellationToken);
            }
        }

        protected void UniFeederServer(IRxSocketServer server, CancellationToken cancellationToken)
        {
            server.AcceptObservable.Subscribe(onNext: accept =>
            {
                @"> Universal DDE Connector 9.00
> Copyright 1999 - 2008 MetaQuotes Software Corp.
> Login: ".ToByteArrayWithZeroEnd().SendTo(accept);

                var auth = new UniFeederAuthorizationOption();
                accept.ReceiveObservable.ToUniFeederStrings().Subscribe(
                    onNext: async message =>
                    {
                        if (!auth.IsFilled)
                        {
                            FillAuth(accept, auth, message);
                        }

                        if (auth.IsFilled)
                        {
                            if (Authentificate(auth)) {
                                "> Access granted".ToUniFeederByteArray().SendTo(accept);
                                
                                try {
                                    await Loop(cancellationToken);
                                }
                                catch(OperationCanceledException e) {
                                    logger.LogInformation("Loop canceled");
                                }
                            }
                            else {
                                "> Access denied".ToUniFeederByteArray().SendTo(accept);
                                accept.Dispose();
                            }
                        }
                    },
                    onError: e => logger.LogError(e.Message)
                 );
            });
        }

        private void FillAuth(IRxSocketClient accept, UniFeederAuthorizationOption auth, string message)
        {
            if (string.IsNullOrEmpty(auth.Login)) {
                auth.Login = message;

                "> Password: ".ToByteArrayWithZeroEnd().SendTo(accept);

                return;
            }

            if (string.IsNullOrEmpty(auth.Password)) {
                auth.Password = message;
                return;
            }
        }

        private bool Authentificate(UniFeederAuthorizationOption auth)
        {
            return option.Value.Authorization.Any(a => a.Equals(auth));
        }
        
        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var tick = await queue.DequeueAsync(token);
                logger.LogInformation(tick.ToString());
            }
        }
    }
}
