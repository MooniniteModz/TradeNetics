using Microsoft.ML.Data;

namespace TradeNetics.Models
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

    public class TradingPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedAction { get; set; } = "";

        [ColumnName("Score")]
        public float[] Confidence { get; set; } = Array.Empty<float>();
    }

    public class TrainingData : CryptoFeatures
    {
        public string Label { get; set; } = ""; // "BUY", "SELL", "HOLD"
    }
}
