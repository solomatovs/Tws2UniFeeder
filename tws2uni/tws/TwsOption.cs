using System.Collections.Generic;
using IBApi;

namespace Tws2UniFeeder
{
    public class TwsOption
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7497;
        public int ClientID { get; set; } = 0;
        public IDictionary<string, Mapping> Mapping { get; set; } = new SortedDictionary<string, Mapping>();
    }

    public static class ContractEx
    {
        public static bool Equal(this Contract b1, Contract b2)
        {
            if (b1 == null && b2 == null)
                return true;
            else if (b1 == null || b2 == null)
                return false;

            return (
                b1.ConId == b2.ConId &&
                b1.Symbol == b2.Symbol &&
                b1.SecType == b2.SecType &&
                b1.LastTradeDateOrContractMonth == b2.LastTradeDateOrContractMonth &&
                b1.Strike == b2.Strike &&
                b1.Right == b2.Right &&
                b1.Multiplier == b2.Multiplier &&
                b1.Exchange == b2.Exchange &&
                b1.Currency == b2.Currency &&
                b1.LocalSymbol == b2.LocalSymbol &&
                b1.PrimaryExch == b2.PrimaryExch &&
                b1.TradingClass == b2.TradingClass &&
                b1.IncludeExpired == b2.IncludeExpired &&
                b1.SecIdType == b2.SecIdType &&
                b1.SecId == b2.SecIdType &&
                b1.ComboLegsDescription == b2.ComboLegsDescription
            );
        }
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

        public override string ToString()
        {
            return $"Time: {Util.UnixSecondsToString(time, "yyyy-MM-dd HH:mm:ss zzz")}, BidPrice: {bidPrice}, AskPrice: {askPrice}";
        }
    }

    public class Mapping : Contract
    {
        public int RequestId { get; set; } = 0;
        public string Name { get; set; }
    }
}
