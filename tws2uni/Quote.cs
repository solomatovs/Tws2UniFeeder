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
    }
}
