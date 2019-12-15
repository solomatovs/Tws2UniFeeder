using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IBApi;

namespace tws2uni.tws
{
    class RealTimeDataProvider : IRealTimeDataProvider
    {
        private readonly ILogger logger;
        private readonly EWrapperImpl wrapper;
        private readonly ConcurrentDictionary<Contract, int> requestIdsBySymbol = new ConcurrentDictionary<Contract, int>(new ContractEqualityComparer());
        public RealTimeDataProvider(IBackgroundQueue<TwsTick> queue, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<RealTimeDataProvider>();
            this.wrapper = new EWrapperImpl(queue, requestIdsBySymbol, loggerFactory);
        }

        public Action Disconnecting = null;

        public void Connect(string host, int port, int clientId, bool autoReconnect = true)
        {
            VConnect(host, port, clientId);

            if (autoReconnect)
                Disconnecting = () => VConnect(host, port, clientId);
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
                requestIdsBySymbol.Clear();
                Disconnecting = null;
                wrapper.ClientSocket.eDisconnect();
            }
            catch (Exception e)
            {
                logger.LogError(e.Message);
                throw;
            }
        }

        public void SubscribeTickByTick(Contract symbol)
        {
            var request = new Random().Next();
            wrapper.ClientSocket.reqTickByTickData(request, symbol, "BidAsk", 0, false);
            while (!requestIdsBySymbol.TryAdd(symbol, request)) { }
        }

        public void UnSubscribeTickByTick(Contract symbol)
        {
            requestIdsBySymbol.TryRemove(symbol, out int request);
            try
            {
                wrapper.ClientSocket.cancelTickByTickData(request);
            }
            catch
            {
                while (!requestIdsBySymbol.TryAdd(symbol, request)) { }
                throw;
            }
        }
    }
}
