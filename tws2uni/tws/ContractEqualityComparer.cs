using System.Collections.Generic;
using IBApi;

namespace tws2uni.tws
{
    class ContractEqualityComparer : EqualityComparer<Contract>
    {
        public override bool Equals(Contract b1, Contract b2)
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

        public override int GetHashCode(Contract bx)
        {
            return $"{bx.Symbol}{bx.LocalSymbol}{bx.ConId}{bx.SecType}{bx.Currency}{bx.Exchange}".GetHashCode();
        }
    }
}
