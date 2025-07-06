using Microsoft.ML.Data;

namespace TradeNetics.Shared.Models
{
    public class TradingPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedAction { get; set; } = "";

        [ColumnName("Score")]
        public float[] Confidence { get; set; } = Array.Empty<float>();
    }
}