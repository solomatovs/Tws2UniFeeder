using System;
using System.Collections.Generic;
using IBApi;

namespace tws2uni.tws
{
    public interface IRealTimeDataProvider
    {
        void SubscribeTickByTick(Contract symbol);
        void UnSubscribeTickByTick(Contract symbol);
    }
}
