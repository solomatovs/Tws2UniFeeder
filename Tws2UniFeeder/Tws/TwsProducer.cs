using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IBApi;

namespace Tws2UniFeeder
{
    class TwsProducer : BackgroundService
    {
        private readonly IOptions<TwsOption> option;
        private readonly SubscriptionDictionary subscription;
        private readonly IBackground<Quote> queue;
        private readonly IBackground<string> state;
        private readonly Timer addNewSubscription;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger logger;

        public TwsProducer(IOptions<TwsOption> option, IBackground<Quote> queue, IBackground<string> state, ILoggerFactory loggerFactory)
        {
            this.option = option;
            this.subscription = new SubscriptionDictionary();
            this.queue = queue;
            this.state = state;
            this.addNewSubscription = new Timer(new TimerCallback(o => AddNewSubscribtion()), null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10));
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<TwsProducer>();
        }

        private TwsOption Option => option.Value;

        protected override async Task ExecuteAsync(CancellationToken token)
        {
            logger.LogInformation("TwsProducer starting...");
            new Thread(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    StartLoopProcess(token);
                }
            })
            { IsBackground = true }.Start();

            await Task.CompletedTask;

            logger.LogInformation("TwsProducer started");
        }

        private void StartLoopProcess(CancellationToken token)
        {
            try
            {
                logger.LogDebug("Starting connection TWS...");
                AddNewSubscribtion();

                var wrapper = new EWrapperImpl(subscription, queue, state, loggerFactory);

                logger.LogDebug("Connecting to {0}:{1} ...", Option.Host, Option.Port);
                wrapper.ClientSocket.eConnect(Option.Host, Option.Port, Option.ClientID);

                while (!wrapper.ClientSocket.IsConnected() && !token.IsCancellationRequested)
                {
                    logger.LogDebug("Waiting connection to {0}:{1}", Option.Host, Option.Port);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }

                logger.LogInformation("Successfully connected to {0}:{1} ...", Option.Host, Option.Port);

                var reader = new EReader(wrapper.ClientSocket, wrapper.signal); reader.Start();

                Task.Run(() => StartSubscribeProcess(wrapper, token));

                StartLoopProcessMsgs(wrapper, reader, token);

                logger.LogDebug("Disconnecting to TWS {0}:{1}", Option.Host, Option.Port);
                wrapper.ClientSocket.eDisconnect();
                wrapper.ClientSocket.Close();
                logger.LogInformation("Successfully disconnected {0}:{1} ...", Option.Host, Option.Port);
            }
            catch (Exception e)
            {
                logger.LogError("TwsProducer.StartLoopProcess error: {0}:{1}", e.GetType().Name, e.Message);
                return;
            }
            finally
            {
                subscription.ClearSymbols();
            }
        }

        private void StartLoopProcessMsgs(EWrapperImpl wrapper, EReader reader, CancellationToken stoppingToken)
        {
            while (wrapper.ClientSocket.IsConnected() && !stoppingToken.IsCancellationRequested)
            {
                wrapper.signal.waitForSignal();
                reader.processMsgs();
            }

            subscription.SetNotRequestedForAllSymbols();
        }

        private void StartSubscribeProcess(EWrapperImpl wrapper, CancellationToken stoppingToken)
        {
            while (wrapper.ClientSocket.IsConnected() && !stoppingToken.IsCancellationRequested)
            {
                this.subscription.ForUnsubscribed((mapping, contract) =>
                {
                    wrapper.ClientSocket.reqMktData(mapping.RequestId, contract, string.Empty, false, false, null);
                    mapping.RequestStatus = RequestStatus.RequestSuccess;
                });

                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }

        private void AddNewSubscribtion()
        {
            foreach (var contract in Option.Mapping)
            {
                subscription.AddSymbolIfNotExists(contract.Key, contract.Value);
            }
        }
    }
}
