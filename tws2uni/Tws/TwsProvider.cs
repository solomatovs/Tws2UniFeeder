using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using IBApi;

namespace Tws2UniFeeder
{
    class TwsProvider : ITwsProvider
    {
        private readonly ILogger logger;
        private readonly EWrapperImpl wrapper;
        private readonly SubscriptionDictionary subscription;
        public TwsProvider(IBackgroundQueue<Quote> queue, ILoggerFactory loggerFactory)
        {
            this.subscription = new SubscriptionDictionary();
            this.logger = loggerFactory.CreateLogger<TwsProvider>();
            this.wrapper = new EWrapperImpl(this.subscription, queue, loggerFactory);
        }

        public void Connect(string host, int port, int clientId)
        {
            VConnect(host, port, clientId);
        }

        private void VConnect(string host, int port, int clientId)
        {
            try
            {
                wrapper.ClientSocket.eConnect(host, port, clientId);
                var reader = new EReader(wrapper.ClientSocket, wrapper.signal);
                reader.Start();
                StartLoopProcessMsgs(reader);
                StartSubscribeProcess();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                throw;
            }
        }

        public bool IsConnected
        {
            get { return wrapper.ClientSocket.IsConnected(); }
        }

        private void StartLoopProcessMsgs(EReader reader)
        {
            new Thread(() =>
            {
                while (wrapper.ClientSocket.IsConnected())
                {
                    wrapper.signal.waitForSignal();
                    reader.processMsgs();
                }
            })
            { IsBackground = true }.Start();
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

        public void SubscribeTickByTick(string symbol, Contract contract)
        {
            this.subscription.AddSymbol(symbol, contract);
        }

        protected void StartSubscribeProcess()
        {
            new Thread(() =>
            {
                while (wrapper.ClientSocket.IsConnected())
                {
                    this.subscription.ForUnsubscribed((mapping, contract) =>
                    {
                        wrapper.ClientSocket.reqTickByTickData(mapping.RequestId, contract, mapping.TickType, 0, false);
                        mapping.RequestStatus = RequestStatus.RequestSuccess;
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    });

                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            })
            { IsBackground = true }.Start();
        }
    }
}
