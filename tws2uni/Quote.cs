namespace Tws2UniFeeder
{
    public class Quote
    {
        public string Symbol { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }

        public override string ToString()
        {
            return $"{Symbol} {Bid} {Ask}";
        }
    }
}
