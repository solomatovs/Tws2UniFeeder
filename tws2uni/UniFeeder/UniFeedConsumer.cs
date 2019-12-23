using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Globalization;
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
        private readonly ConcurrentDictionary<int, IRxSocketClient> clients;

        public UniFeedConsumer(IOptions<UniFeederOption> option, IBackgroundQueue<Quote> queue, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.queue = queue;
            this.loggerFactory = loggerFactory;
            this.clients = new ConcurrentDictionary<int, IRxSocketClient>();
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var end = new IPEndPoint(IPAddress.Parse(option.Value.Ip), option.Value.Port);

            using (var server = end.CreateRxSocketServer(loggerFactory.CreateLogger<RxSocketServer>()))
            {
                UniFeederServer(server, cancellationToken);

                try {
                    await Loop(token: cancellationToken);
                }
                catch (OperationCanceledException) {
                    logger.LogInformation("Loop canceled");
                }
            }
        }

        protected void UniFeederServer(IRxSocketServer server, CancellationToken cancellationToken)
        {
            server.AcceptObservable.Subscribe(onNext: accept =>
            {
                @"> Universal DDE Connector 9.00
> Copyright 1999 - 2008 MetaQuotes Software Corp.
> Login: ".ToUniFeederByteArray().SendTo(accept);
                var clientId = new Random().Next();
                var auth = new UniFeederAuthorizationOption();
                accept.ReceiveObservable.ToUniFeederStrings().Subscribe(
                    onNext: message =>
                    {
                        if (!auth.IsFilled) {
                            FillAuth(accept, auth, message);
                        }

                        if (auth.IsFilled) {
                            if (Authentificate(auth)) {
                                clients.TryAdd(clientId, accept);
                                "> Access granted".ToUniFeederByteArray().SendTo(accept);
                            }
                            else {
                                "> Access denied".ToUniFeederByteArray().SendTo(accept);
                                accept.Dispose();
                            }
                        }
                    },
                    onError: e => logger.LogError(e.Message), 
                    onCompleted: () =>
                    {
                        if (clients.TryRemove(clientId, out IRxSocketClient client))
                        {
                            client.Dispose();
                        }
                    }
                 );
            });
        }

        private void FillAuth(IRxSocketClient accept, UniFeederAuthorizationOption auth, string message)
        {
            if (string.IsNullOrEmpty(auth.Login)) {
                auth.Login = message;

                "> Password: ".ToUniFeederByteArray().SendTo(accept);

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
                var quote = await queue.DequeueAsync(token);
                var quoteUniFeederFormat = quote.ToUniFeederStringFormat().ToUniFeederByteArray();

                clients.AsParallel().ForAll(c =>
                {
                    c.Value.Send(quoteUniFeederFormat);
                });
            }
        }
    }

    public static class QuotesUniFeederEx
    {
        public static string ToUniFeederStringFormat(this Quote q)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", q.Symbol, q.Bid, q.Ask);
        }
    }
}
