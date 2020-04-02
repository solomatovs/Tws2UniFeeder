using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Diagnostics;
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
        private readonly IOptions<UniFeederOption> option;
        private readonly ILogger logger;
        private readonly ILogger loggerQuotes;
        private readonly IBackground<Quote> queue;
        private IRxSocketServer server;
        private readonly ConcurrentBag<UniFeederQuote> quotes;
        private readonly ConcurrentDictionary<int, IRxSocketClient> clients;
        private readonly Stopwatch timer;

        public UniFeedConsumer(IOptions<UniFeederOption> option, IOptions<TwsOption> twsOption, IBackground<Quote> queue, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.queue = queue;
            this.clients = new ConcurrentDictionary<int, IRxSocketClient>();
            this.quotes = new ConcurrentBag<UniFeederQuote>(option.Value.TranslatesToUniFeederQuotes(twsOption.Value));
            this.timer = new Stopwatch();
            logger = loggerFactory.CreateLogger<UniFeedConsumer>();
            loggerQuotes = loggerFactory.CreateLogger("quotes");
        }

        protected async override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("UniFeeder starting...");
            var end = new IPEndPoint(IPAddress.Parse(option.Value.Ip), option.Value.Port);

            this.server = end.CreateRxSocketServer();
            UniFeederServer(server);

            Loop(token: cancellationToken);
            await Task.CompletedTask;

            logger.LogInformation("UniFeeder started");
        }

        public override void Dispose()
        {
            this.server?.Dispose();
            logger.LogInformation("UniFeeder stoped");
        }

        private void Loop(CancellationToken token)
        {
            new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var tick = queue.DequeueAsync(token).Result;
                        timer.Restart();
                        loggerQuotes.LogDebug("{@Timestamp:HH:mm:ss.ffffff}; {@symbol}; {@ask:f5}; {@bid:f5}; {@spread:f5}; {@event_type}", tick.Time, tick.Symbol, tick.Ask, tick.Bid, (tick.Ask - tick.Bid), "raw");

                        quotes.AsParallel().Where(q => q.Source == tick.Symbol).ForAll(q =>
                        {
                            var setMs = timer.ElapsedTicks;
                            q.SetQuote(tick, logger);
                            setMs = timer.ElapsedTicks - setMs;

                            if (q.Change)
                            {
                                var uniStringMs = timer.ElapsedTicks;
                                var quoteUniFeederFormat = q.ToUniFeederStringFormat().ToUniFeederByteArray();
                                uniStringMs = timer.ElapsedTicks - uniStringMs;

                                var sendMs = timer.ElapsedTicks;
                                foreach (var c in clients)
                                {
                                    try
                                    {
                                        c.Value.Send(quoteUniFeederFormat);
                                    }
                                    catch (Exception e)
                                    {
                                        logger.LogError("client: {0} error send quote: {1}", c.Key, e.Message);
                                        Task.Run(() => RemoveClient(c.Key, 5));
                                    }
                                }
                                sendMs = timer.ElapsedTicks - sendMs;

                                loggerQuotes.LogDebug("{@Timestamp:HH:mm:ss.ffffff}; {@symbol}; {@ask:f5}; {@bid:f5}; {@spread:f5}; {@event_type}", tick.Time, q.Symbol, q.LastAsk, q.LastBid, (q.LastAsk - q.LastBid), "translate");
                                loggerQuotes.LogDebug("{@Timestamp:HH:mm:ss.ffffff}; {@stopwatch}; {@event_type}", tick.Time, new { translate=setMs, build_unifeeder=uniStringMs, send_quotes=sendMs, count_clients=clients.Count }, "stopwatch");
                            }
                        });
                    }
                    catch(Exception e)
                    {
                        if (e is OperationCanceledException || e.InnerException is OperationCanceledException)
                            break;
                        else
                            logger.LogError("error in queue processing: {0}", e.Message);
                    }
                }
            })
            { IsBackground = true }.Start();
        }

        protected void UniFeederServer(IRxSocketServer server)
        {
            server.AcceptObservable.Subscribe(onNext: accept =>
            {
                
                @"> Universal DDE Connector 9.00
> Copyright 1999 - 2008 MetaQuotes Software Corp.
> Login: ".ToUniFeederByteArray().SendTo(accept);
                var clientId = new Random().Next();
                var auth = new UniFeederAuthorizationOption();
                var started = DateTimeOffset.UtcNow;
                var ended = started.AddSeconds(5);
                logger.LogInformation("accepted new client {0}", clientId);

                accept.ReceiveObservable.ToUniFeederStrings().Subscribe(
                    onNext: message =>
                    {
                        logger.LogDebug("client: {0} receive message: {1}", clientId, message);

                        if (!clients.ContainsKey(clientId) && DateTimeOffset.UtcNow >= ended)
                        {
                            accept.Dispose();
                            logger.LogInformation("a non-authenticated client {0} is disconnected after 5 seconds inactivity", clientId);
                            return;
                        }

                        if (!auth.IsFilled)
                        {
                            FillAuth(accept, auth, message);
                        }

                        if (auth.IsFilled)
                        {
                            if (Authentificate(auth))
                            {
                                if(clients.TryAdd(clientId, accept))
                                {
                                    "> Access granted".ToUniFeederByteArray().SendTo(accept);
                                }
                                else
                                switch (message)
                                {
                                    case "> Ping": "> Ping".ToUniFeederByteArray().SendTo(accept); logger.LogDebug("send Ping to client {0}", clientId); break;
                                    default: break;
                                }
                            }
                            else
                            {
                                "> Access denied".ToUniFeederByteArray().SendTo(accept);
                                accept.Dispose();
                            }
                        }
                    },
                    onError: e =>
                    {
                        RemoveClient(clientId, 5);
                        logger.LogError("client: {0} rxsocket error {1}. client disposed. current clients: {2}", clientId, e.Message, clients.Count);
                    },
                    onCompleted: () =>
                    {
                        RemoveClient(clientId, 5);
                        logger.LogInformation("client: {0} rxsocket complited. client disposed. current clients: {1}", clientId, clients.Count);
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

        private bool RemoveClient(int clientId, uint tryRemoveCount = 5)
        {
            var i = tryRemoveCount;
            while (i > 0)
            {
                if (clients.TryRemove(clientId, out IRxSocketClient client))
                {
                    client.Dispose();
                    return true;
                }

                i--;
            }

            return false;
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
