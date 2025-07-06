namespace TradeNetics.Shared.Models
{
    public class CryptoFeatures
    {
        public float Price { get; set; }
        public float Volume { get; set; }
        public float PriceChange24h { get; set; }
        public float VolumeChange24h { get; set; }
        public float RSI { get; set; }
        public float MovingAverage5 { get; set; }
        public float MovingAverage20 { get; set; }
        public float BollingerUpper { get; set; }
        public float BollingerLower { get; set; }
        public float MACD { get; set; }
        public float Signal { get; set; }
        public float VolumeRatio { get; set; }
    }
}
