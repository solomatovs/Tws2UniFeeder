using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using IBApi;

namespace Tws2UniFeeder
{
    public class TwsOption
    {
        public string Host { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 7497;
        public int ClientID { get; set; } = 0;
        public int ReconnectPeriodSecond { get; set; } = 5;
        public IDictionary<string, Contract> Mapping { get; set; } = new Dictionary<string, Contract>();
    }

    public enum RequestStatus
    {
        WasNotRequested,
        RetryNeeded,
        RequestSuccess,
        RequestFailed
    }

    public class Mapping
    {
        public int RequestId { get; set; } = 0;
        public RequestStatus RequestStatus { get; set; } = RequestStatus.WasNotRequested;
        public string Symbol { get; set; } = string.Empty;
        public string TickType { get; set; } = "BidAsk";
    }

    public class SubscriptionDictionary
    {
        private readonly Dictionary<Mapping, Contract> map = new Dictionary<Mapping, Contract>();
        private readonly Random rand = new Random();

        public SubscriptionDictionary() { }
        public SubscriptionDictionary(IDictionary<string, Contract> map)
        {
            foreach (var m in map)
            {
                AddSymbol(m.Key, m.Value);
            }
        }

        public string GetSymbolNameByRequestId(int requestId)
        {
            return this.map.Keys.FirstOrDefault(c => c.RequestId == requestId)?.Symbol ?? string.Empty;
        }

        public void ChangeStatusForRequest(int requestId, RequestStatus status)
        {
            foreach(var m in this.map)
            {
                if(m.Key.RequestId == requestId)
                {
                    m.Key.RequestStatus = status;
                }
            }
        }

        public void AddSymbol(string name, Contract symbol)
        {
            var predicate = new KeyValuePair<Mapping, Contract>(new Mapping
            {
                Symbol = name,
                RequestId = rand.Next(),
                RequestStatus = RequestStatus.WasNotRequested
            }, symbol);

            if (!this.map.Contains(predicate, new SymbolDictionaryComparer()))
            {
                this.map.Add(predicate.Key, predicate.Value);
            }
        }

        public void ForUnsubscribed(Action<Mapping, Contract> actionOnSymbol)
        {
            foreach (var s in this.map)
            {
                if (s.Key.RequestStatus == RequestStatus.WasNotRequested || s.Key.RequestStatus == RequestStatus.RetryNeeded)
                {
                    actionOnSymbol(s.Key, s.Value);
                }
            }
        }

        public void SetNotRequestedForAllSymbols()
        {
            foreach(var s in this.map)
            {
                s.Key.RequestStatus = RequestStatus.WasNotRequested;
            }
        }

        public int RequestIdBySymbolName(string name)
        {
            return this.map.FirstOrDefault(p => p.Key.Symbol == name).Key.RequestId;
        }

        public void ReGenerateRequestIdForSymbol(int requestId)
        {
            foreach (var s in this.map)
            {
                if (s.Key.RequestId == requestId)
                {
                    s.Key.RequestId = rand.Next();
                    s.Key.RequestStatus = RequestStatus.WasNotRequested;
                }
            }
        }

        public void ClearSymbols()
        {
            this.map.Clear();
        }
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

        public static bool Equal(this Mapping b1, Mapping b2)
        {
            if (b1 == null && b2 == null)
                return true;
            else if (b1 == null || b2 == null)
                return false;

            return (
                b1.Symbol == b2.Symbol
            );
        }
    }

    class ContractDictionaryComparer : IEqualityComparer<Contract>
    {
        public bool Equals(Contract x, Contract y)
        {
            if (x.Equal(y))
                return true;

            return false;
        }

        public int GetHashCode(Contract obj)
        {
            return obj.GetHashCode();
        }
    }

    class MappingDictionaryComparer : IEqualityComparer<Mapping>
    {
        public bool Equals(Mapping x, Mapping y)
        {
            if (x.Equal(y))
                return true;

            return false;
        }

        public int GetHashCode(Mapping obj)
        {
            return obj.Symbol.GetHashCode();
        }
    }

    class SymbolDictionaryComparer : IEqualityComparer<KeyValuePair<Mapping, Contract>>
    {
        public bool Equals(KeyValuePair<Mapping, Contract> x, KeyValuePair<Mapping, Contract> y)
        {
            if (x.Key.Equal(y.Key) && x.Value.Equal(y.Value))
                return true;

            return false;
        }

        public int GetHashCode(KeyValuePair<Mapping, Contract> obj)
        {
            return obj.GetHashCode();
        }
    }
}
