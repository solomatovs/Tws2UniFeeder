using System.Globalization;

namespace Tws2UniFeeder
{
    public class Quote
    {
        public string Symbol { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }

        public bool IsFilled()
        {
            return !(Bid == 0 || Ask == 0);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1} {2})", Symbol, Bid, Ask);
        }
    }

    public static class QuoteEx
    {
        public static bool QuoteEqual(this Quote q, Quote quote)
        {
            return 
                q.Ask == quote.Ask &&
                q.Bid == quote.Bid;
        }

        public static bool IsValidQuote(this Quote q)
        {
            return q.Ask > 0 && q.Bid > 0;
        }
    }
}
