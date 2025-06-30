namespace TradeNetics.Helpers
{
    public static class TechnicalAnalysis
    {
        public static float CalculateRSI(List<decimal> prices, int period = 14)
        {
            if (prices.Count < period + 1) return 50f;

            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < prices.Count; i++)
            {
                var change = prices[i] - prices[i - 1];
                gains.Add(change > 0 ? change : 0);
                losses.Add(change < 0 ? Math.Abs(change) : 0);
            }

            var avgGain = gains.TakeLast(period).Average();
            var avgLoss = losses.TakeLast(period).Average();

            if (avgLoss == 0) return 100f;

            var rs = avgGain / avgLoss;
            return (float)(100 - (100 / (1 + rs)));
        }

        public static float CalculateMovingAverage(List<decimal> prices, int period)
        {
            if (prices.Count < period) return (float)prices.Average();
            return (float)prices.TakeLast(period).Average();
        }

        public static (float upper, float lower) CalculateBollingerBands(List<decimal> prices, int period = 20, float multiplier = 2f)
        {
            if (prices.Count < period)
            {
                var avg = (float)prices.Average();
                return (avg, avg);
            }

            var recentPrices = prices.TakeLast(period).ToList();
            var sma = (float)recentPrices.Average();
            var variance = recentPrices.Select(p => Math.Pow((double)(p - (decimal)sma), 2)).Average();
            var stdDev = (float)Math.Sqrt(variance);

            return (sma + (multiplier * stdDev), sma - (multiplier * stdDev));
        }

        public static (float macd, float signal) CalculateMACD(List<decimal> prices, int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            if (prices.Count < slowPeriod) return (0f, 0f);

            var fastEMA = CalculateEMA(prices, fastPeriod);
            var slowEMA = CalculateEMA(prices, slowPeriod);
            var macd = fastEMA - slowEMA;

            var macdLine = new List<decimal> { (decimal)macd };
            var signal = CalculateMovingAverage(macdLine, Math.Min(signalPeriod, macdLine.Count));

            return (macd, signal);
        }

        private static float CalculateEMA(List<decimal> prices, int period)
        {
            if (prices.Count < period) return (float)prices.Average();

            var multiplier = 2.0f / (period + 1);
            var ema = (float)prices.Take(period).Average();

            for (int i = period; i < prices.Count; i++)
            {
                ema = ((float)prices[i] * multiplier) + (ema * (1 - multiplier));
            }

            return ema;
        }
    }
}
