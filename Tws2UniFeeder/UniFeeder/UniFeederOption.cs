﻿using System;
using System.Globalization;
using System.Collections.Generic;

namespace Tws2UniFeeder
{
    public class UniFeederOption
    {
        public string Ip { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 2222;
        public IList<UniFeederAuthorizationOption> Authorization { get; set; } = new List<UniFeederAuthorizationOption>();
        public IList<UniFeederQuote> Translates { get; set; } = new List<UniFeederQuote>();
    }

    public class UniFeederAuthorizationOption
    {
        public string Login { get; set; }
        public string Password { get; set; }

        public bool IsFilled
        {
            get
            {
                return !(string.IsNullOrWhiteSpace(this.Login) || string.IsNullOrWhiteSpace(this.Password)); 
            }
        }

        public bool Equals(UniFeederAuthorizationOption obj)
        {
            if (obj is null) return false;

            return (
                obj.Login == this.Login &&
                obj.Password == this.Password
            );
        }
    }

    public class UniFeederTranslate
    {
        public string Symbol { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public int Digits { get; set; } = 1;
        public int BidMarkup { get; set; } = 0;
        public int AskMarkup { get; set; } = 0;
        public double Percent { get; set; } = 0;
        public int Fix { get; set; } = -1;
        public int Min { get; set; } = -1;
        public int Max { get; set; } = -1;
    }

    public static class UniFeederTranslateEx
    {
        public static string ToStringTranslates(this UniFeederTranslate t)
        {
            return string.Format(CultureInfo.InvariantCulture, "Symbol:{0} Source:{1} Digits:{2} BidMarkup:{3} AskMarkup:{4} Percent:{5} Fix:{6} Min:{7} Max:{8}", t.Symbol, t.Source, t.Digits, t.BidMarkup, t.AskMarkup, t.Percent, t.Fix, t.Min, t.Max);
        }
    }

    public class UniFeederQuote : UniFeederTranslate
    {
        public double LastBid { get; set; }
        public double LastAsk { get; set; }
        public Quote LastTick { get; set; } = new Quote();
        public bool Change { get; private set; } = false;

        public void SetQuote(Quote quote)
        {
            if (quote.IsValidQuote())
            {
                if (!LastTick.QuoteEqual(quote))
                {
                    double last_bid = quote.Bid;
                    double last_ask = quote.Ask;
                    double point = Math.Pow(10, -Digits);
                    double contract = Math.Pow(10, Digits);

                    if (BidMarkup != 0)
                    {
                        last_bid += point * BidMarkup;
                    }

                    if (AskMarkup != 0)
                    {
                        last_ask += point * AskMarkup;
                    }

                    if (Percent != 0)
                    {
                        double pointModify = (last_ask - last_bid) * Percent / 100 / 2;
                        last_bid -= pointModify;
                        last_ask += pointModify;
                    }

                    if (Min != -1)
                    {
                        double spread = (last_ask - last_bid) * contract;
                        if (spread < Min)
                        {
                            double last_mid = (last_ask + last_bid) / 2;
                            last_bid = last_mid - (Min * point / 2);
                            last_ask = last_mid + (Min * point / 2);
                        }
                    }

                    if (Max != -1)
                    {
                        double spread = (last_ask - last_bid) * contract;
                        if (spread > Max)
                        {
                            double last_mid = (last_ask + last_bid) / 2;
                            last_bid = last_mid - (Max * point / 2);
                            last_ask = last_mid + (Max * point / 2);
                        }
                    }

                    if (Fix != -1)
                    {
                        double last_mid = (last_bid + last_ask) / 2;
                        last_bid = last_mid - (Fix * point / 2);
                        last_ask = last_mid + (Fix * point / 2);
                    }

                    last_bid = Math.Round(last_bid, Digits, MidpointRounding.ToEven);
                    last_ask = Math.Round(last_ask, Digits, MidpointRounding.ToEven);

                    if (last_ask != LastAsk || last_bid != LastBid)
                    {
                        LastBid = last_bid;
                        LastAsk = last_ask;
                        Change = true;
                    }
                }

                LastTick = quote;
            }
        }

        public string ToUniFeederStringFormat()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", Symbol, LastBid, LastAsk);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} ({1} {2})", Symbol, LastBid, LastAsk);
        }
    }

    public static class UniFeederOptionEx
    {
        public static IList<UniFeederQuote> TranslatesToUniFeederQuotes(this UniFeederOption option, TwsOption twsOption)
        {
            var r = new List<UniFeederQuote>();
            foreach(var q in option.Translates)
            {
                r.Add(q);
            }

            foreach(var q in twsOption.Mapping)
            {
                if(!r.Exists(p => p.Symbol == q.Key))
                {
                    r.Add(new UniFeederQuote
                    {
                        Symbol = q.Key,
                        Source = q.Key,
                        Digits = 5,
                    });
                }
            }

            return r;
        }
    }
}
