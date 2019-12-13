using System;
using System.Collections.Generic;
using IBApi;

namespace tws2uni.tws
{
    public interface IRealTimeDataProvider
    {
        void Connect(string host, int port, int clientId, bool autoReconnect = true);
        void Disconnect();
        void SubscribeTickByTick(Contract symbol);
        void UnSubscribeTickByTick(Contract symbol);
    }
}
