using System.Collections.Generic;
using IBApi;

namespace tws2uni.tws
{
    public class TwsOption
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7497;
        public int ClientID { get; set; } = 0;
        public IList<Contract> Contracts { get; set; } = new List<Contract>();
    }

    public class TwsTick
    {
        public int reqId { get; set; }
        public long time { get; set; }
        public double bidPrice { get; set; }
        public double askPrice { get; set; }
        public int bidSize { get; set; }
        public int askSize { get; set; }
        public TickAttribBidAsk tickAttribBidAsk { get; set; }
        public Contract symbol { get; set; }

        public override string ToString()
        {
            return $"Symbol: {symbol}, Time: {Util.UnixSecondsToString(time, "yyyy-MM-dd HH:mm:ss zzz")}, BidPrice: {bidPrice}, AskPrice: {askPrice}";
        }

    }
}
