using TradeNetics.Shared.Models;
using System.Collections.Generic;

namespace TradeNetics.Shared.Interfaces
{
    public interface IMLTradingModel
    {
        void TrainModel(List<TrainingData> trainingData);
        TradingPrediction PredictAction(CryptoFeatures features);
        void SaveModel(string filePath);
        void LoadModel(string filePath);
    }
}