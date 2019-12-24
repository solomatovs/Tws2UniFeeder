using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IBApi;

namespace Tws2UniFeeder
{
    class TwsProvider : ITwsProvider
    {
        private readonly ILogger logger;
        private readonly EWrapperImpl wrapper;
        private readonly SubscriptionDictionary subscription;
        public TwsProvider(IBackgroundQueue<Tick> queue, ILoggerFactory loggerFactory)
        {
            this.subscription = new SubscriptionDictionary();
            this.logger = loggerFactory.CreateLogger<TwsProvider>();
            this.wrapper = new EWrapperImpl(this.subscription, queue, loggerFactory);
        }

        public void Connect(string host, int port, int clientId, CancellationToken stoppingToken)
        {
            try
            {
                wrapper.ClientSocket.eConnect(host, port, clientId);
                var reader = new EReader(wrapper.ClientSocket, wrapper.signal);
                reader.Start();

                Task.Run(() => StartLoopProcessMsgs(reader, stoppingToken));
                Task.Run(() => StartSubscribeProcess(stoppingToken));
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
            }
        }

        public bool IsConnected
        {
            get { return wrapper.ClientSocket.IsConnected(); }
        }

        private void StartLoopProcessMsgs(EReader reader, CancellationToken stoppingToken)
        {
            while (wrapper.ClientSocket.IsConnected() && !stoppingToken.IsCancellationRequested)
            {
                wrapper.signal.waitForSignal();
                reader.processMsgs();
            }
        }

        public void Disconnect()
        {
            try
            {
                wrapper.ClientSocket.eDisconnect();
                this.subscription.ClearSymbols();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                throw;
            }
        }

        public void SubscribeMktData(string symbol, Contract contract)
        {
            this.subscription.AddSymbol(symbol, contract);
        }

        protected void StartSubscribeProcess(CancellationToken stoppingToken)
        {
            while (wrapper.ClientSocket.IsConnected() && !stoppingToken.IsCancellationRequested)
            {
                this.subscription.ForUnsubscribed((mapping, contract) =>
                {
                    wrapper.ClientSocket.reqMktData(mapping.RequestId, contract, string.Empty, false, false, null);
                    mapping.RequestStatus = RequestStatus.RequestSuccess;
                    Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ContinueWith(p => { }).Wait();
                });

                Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ContinueWith(p => { }).Wait();
            }
        }
    }
}
