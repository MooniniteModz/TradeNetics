using Microsoft.Extensions.Logging;
using Microsoft.ML;
using TradeNetics.Interfaces;
using TradeNetics.Models;

namespace TradeNetics.Services
{
    public class MLTradingModel : IMLTradingModel
    {
        private readonly MLContext _mlContext;
        private readonly ILogger<MLTradingModel> _logger;
        private ITransformer? _model;

        public MLTradingModel(ILogger<MLTradingModel> logger)
        {
            _mlContext = new MLContext(seed: 0);
            _logger = logger;
        }

        public async Task TrainModelAsync(List<TrainingData> trainingData)
        {
            try
            {
                _logger.LogInformation("Training ML model with {Count} samples", trainingData.Count);

                var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "LabelKey", inputColumnName: "Label")
                    .Append(_mlContext.Transforms.Concatenate("Features",
                        nameof(CryptoFeatures.Price),
                        nameof(CryptoFeatures.Volume),
                        nameof(CryptoFeatures.PriceChange24h),
                        nameof(CryptoFeatures.VolumeChange24h),
                        nameof(CryptoFeatures.RSI),
                        nameof(CryptoFeatures.MovingAverage5),
                        nameof(CryptoFeatures.MovingAverage20),
                        nameof(CryptoFeatures.BollingerUpper),
                        nameof(CryptoFeatures.BollingerLower),
                        nameof(CryptoFeatures.MACD),
                        nameof(CryptoFeatures.Signal),
                        nameof(CryptoFeatures.VolumeRatio)))
                    .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: "LabelKey", featureColumnName: "Features"))
                    .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", inputColumnName: "PredictedLabel"));

                _model = pipeline.Fit(dataView);

                _logger.LogInformation("ML model training completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training ML model");
                throw;
            }
        }

        public TradingPrediction PredictAction(CryptoFeatures features)
        {
            if (_model == null)
                throw new InvalidOperationException("Model must be trained before making predictions");

            var trainingData = new TrainingData
            {
                Price = features.Price,
                Volume = features.Volume,
                PriceChange24h = features.PriceChange24h,
                VolumeChange24h = features.VolumeChange24h,
                RSI = features.RSI,
                MovingAverage5 = features.MovingAverage5,
                MovingAverage20 = features.MovingAverage20,
                BollingerUpper = features.BollingerUpper,
                BollingerLower = features.BollingerLower,
                MACD = features.MACD,
                Signal = features.Signal,
                VolumeRatio = features.VolumeRatio,
                Label = "HOLD"
            };

            var predictionEngine = _mlContext.Model.CreatePredictionEngine<TrainingData, TradingPrediction>(_model);
            return predictionEngine.Predict(trainingData);
        }

        public async Task SaveModelAsync(string filePath)
        {
            if (_model == null)
                throw new InvalidOperationException("Model must be trained before saving");

            _mlContext.Model.Save(_model, null, filePath);
            _logger.LogInformation("Model saved to {FilePath}", filePath);
        }

        public async Task LoadModelAsync(string filePath)
        {
            if (File.Exists(filePath))
            {
                _model = _mlContext.Model.Load(filePath, out var modelInputSchema);
                _logger.LogInformation("Model loaded from {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("Model file not found: {FilePath}", filePath);
            }
        }
    }
}
