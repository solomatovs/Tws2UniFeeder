using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
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
        private readonly IOptions<UniFeederOption> option;
        private readonly ILogger logger;
        private readonly ILoggerFactory loggerFactory;
        private readonly IBackgroundQueue<Tick> queue;
        private readonly ConcurrentDictionary<string, Quote> quotes;
        private readonly ConcurrentDictionary<int, IRxSocketClient> clients;

        public UniFeedConsumer(IOptions<UniFeederOption> option, IBackgroundQueue<Tick> queue, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.queue = queue;
            this.loggerFactory = loggerFactory;
            this.clients = new ConcurrentDictionary<int, IRxSocketClient>();
            this.quotes = new ConcurrentDictionary<string, Quote>();
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var end = new IPEndPoint(IPAddress.Parse(option.Value.Ip), option.Value.Port);

            using var server = end.CreateRxSocketServer(loggerFactory.CreateLogger<RxSocketServer>());
            UniFeederServer(server, cancellationToken);

            await Loop(token: cancellationToken);
        }
        private async Task Loop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var tick = await queue.DequeueAsync(token);

                quotes.AddOrUpdate(tick.Symbol, s => new Quote
                {
                    Symbol = tick.Symbol,
                    Ask = tick.TickType == TickType.AskPrice ? tick.Price : 0,
                    Bid = tick.TickType == TickType.BidPrice ? tick.Price : 0
                }, (s, q) => new Quote
                {
                    Symbol = tick.Symbol,
                    Ask = tick.TickType == TickType.AskPrice ? tick.Price : q.Ask,
                    Bid = tick.TickType == TickType.BidPrice ? tick.Price : q.Bid
                });

                if (quotes.TryGetValue(tick.Symbol, out Quote quote))
                {
                    if (quote.IsFilled())
                    {
                        var quoteUniFeederFormat = quote.ToUniFeederStringFormat().ToUniFeederByteArray();
                        try
                        {
                            clients.AsParallel().ForAll(c =>
                            {
                                c.Value.Send(quoteUniFeederFormat);
                            });
                        }
                        catch { }
                    }
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
    }

    public static class QuotesUniFeederEx
    {
        public static string ToUniFeederStringFormat(this Quote q)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", q.Symbol, q.Bid, q.Ask);
        }
    }
}
