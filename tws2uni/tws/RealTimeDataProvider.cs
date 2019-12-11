using System;
using System.Collections.Concurrent;
using System.Threading;
using IBApi;

namespace tws2uni.tws
{
    class RealTimeDataProvider : IRealTimeDataProvider
    {
        private readonly EReader reader;
        private readonly EClientSocket clientSocket;
        private readonly EReaderSignal readerSignal;
        private readonly ConcurrentDictionary<string, int> requestIdsBySymbol = new ConcurrentDictionary<string, int>();
        public RealTimeDataProvider()
        {
            var wrapper = new EWrapperImpl();
            this.clientSocket = wrapper.ClientSocket;
            this.readerSignal = wrapper.Signal;
            this.reader = new EReader(wrapper.ClientSocket, wrapper.Signal);
        }
        public void Connect(string host, int port, int clientId)
        {
            try
            {
                clientSocket.eConnect(host, port, clientId);

                var reader = new EReader(clientSocket, readerSignal);
                reader.Start();
                //Once the messages are in the queue, an additional thread can be created to fetch them
                new Thread(() =>
                {
                    while (clientSocket.IsConnected())
                    {
                        readerSignal.waitForSignal();
                        reader.processMsgs();
                    }
                })
                { IsBackground = true }.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public Task ConnectAsync(string host, int port, int clientId)
        {
            var ct = new CancellationTokenSource(DefaultTimeoutMs);
            var res = new TaskCompletionSource<object>();
            ct.Token.Register(() => res.TrySetCanceled(), false);

            EventHandler connectAck = (sender, args) =>
            {
                res.SetResult(new object());
            };

            EventDispatcher.ConnectAck += connectAck;

            Connect(host, port, clientId);

            res.Task.ContinueWith(x =>
            {
                EventDispatcher.ConnectAck -= connectAck;

            }, TaskContinuationOptions.None);

            return res.Task;
        }

        public void Disconnect()
        {
            try
            {
                ClientSocket.eDisconnect();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void SubscribeTickByTick(Contract symbol)
        {
            var request = new Random().Next();
            ClientSocket.reqMktData(request, symbol, string.Empty, false, false, null);

            throw new NotImplementedException();
        }

        public void UnSubscribeTickByTick(Contract symbol)
        {
            throw new NotImplementedException();
        }
    }
}
