using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IBApi;

namespace Tws2UniFeeder
{
    class TwsProvider : ITwsProvider
    {
        private readonly ILogger logger;
        private readonly EWrapperImpl wrapper;
        public TwsProvider(IOptions<TwsOption> option, IBackgroundQueue<Quote> queue, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<TwsProvider>();
            this.wrapper = new EWrapperImpl(option, queue, loggerFactory);
        }

        public Action Disconnecting = null;

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
                Disconnecting = null;
                wrapper.ClientSocket.eDisconnect();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                throw;
            }
        }

        public void SubscribeTickByTick(Mapping contract)
        {
            wrapper.ClientSocket.reqTickByTickData(contract.RequestId, contract, "BidAsk", 0, false);
        }

        public void UnSubscribeTickByTick(Mapping contract)
        {
            wrapper.ClientSocket.cancelTickByTickData(contract.RequestId);
        }
    }
}
