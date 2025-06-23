# TradeNetics 🚀

> **AI-Powered Cryptocurrency Trading Bot Built with ML.NET**

<p align="center">
  <img src="https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET" />
  <img src="https://img.shields.io/badge/ML.NET-FF6F00?style=for-the-badge&logo=microsoft&logoColor=white" alt="ML.NET" />
  <img src="https://img.shields.io/badge/Binance-F0B90B?style=for-the-badge&logo=binance&logoColor=black" alt="Binance" />
  <img src="https://img.shields.io/badge/License-MIT-green.svg?style=for-the-badge" alt="License" />
</p>

<p align="center">
  Autonomous cryptocurrency trading powered by machine learning and technical analysis
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#how-it-works">How It Works</a> •
  <a href="#documentation">Docs</a> •
  <a href="#contributing">Contributing</a>
</p>

---

## Overview

TradeNetics combines **machine learning**, **technical analysis**, and **real-time market data** to make autonomous trading decisions on cryptocurrency markets. Built with C# and ML.NET, it learns from historical patterns to predict optimal buy/sell/hold signals.

### Key Capabilities

- 🤖 **AI Predictions** - ML.NET classification models with confidence scoring
- 📊 **Technical Analysis** - RSI, MACD, Bollinger Bands, Moving Averages
- ⚡ **Real-time Trading** - Live Binance API integration
- 🛡️ **Risk Management** - Built-in safety features and error handling

---

## Features

<table>
<tr>
<td width="50%">

### 🧠 Machine Learning
- Multi-class classification (BUY/SELL/HOLD)
- Feature engineering from market data
- Model persistence and continuous learning
- Confidence-based decision making

### 🔗 Market Integration
- Real-time Binance API connectivity
- Historical candlestick data analysis
- Automated order placement
- Portfolio monitoring

</td>
<td width="50%">

### 📈 Technical Analysis
- **RSI** - Momentum oscillator
- **MACD** - Trend analysis
- **Bollinger Bands** - Volatility detection
- **Moving Averages** - Trend smoothing
- **Volume Analysis** - Market sentiment

### 🛡️ Security & Safety
- HMAC-SHA256 request signing
- Secure API credential management
- Comprehensive error handling
- Demo mode for safe testing

</td>
</tr>
</table>

---

## Quick Start

### Prerequisites

```bash
.NET 6.0+ SDK
Binance API credentials
```

### Installation

```bash
# Clone repository
git clone https://github.com/yourusername/TradeNetics.git
cd TradeNetics

# Install dependencies
dotnet restore
dotnet add package Microsoft.ML
dotnet add package System.Text.Json
```

### Configuration

Update your API credentials in `TraderConfig`:

```csharp
public static class TraderConfig
{
    public const string ApiKey = "your_binance_api_key";
    public const string ApiSecret = "your_binance_api_secret";
    public const string BaseApiUrl = "https://api.binance.com";
}
```

### Run

```bash
dotnet run
```

---

## How It Works

### Data Flow Pipeline

```
📡 Binance API → 📊 Market Data → 🔬 Technical Analysis → 🤖 ML Model → 💰 Trading Decision
```

### 1. Market Data Collection

The bot continuously fetches:
- Current prices and 24hr statistics
- Historical candlestick (OHLCV) data
- Trading volume and market depth

### 2. Technical Analysis

Calculates key indicators:

| Indicator | Purpose | Signal Interpretation |
|-----------|---------|----------------------|
| **RSI** | Momentum | <30: Oversold (Buy) • >70: Overbought (Sell) |
| **MACD** | Trend | Above signal: Bullish • Below signal: Bearish |
| **Bollinger Bands** | Volatility | Price at upper: Sell • At lower: Buy |
| **Moving Averages** | Direction | Price above MA: Uptrend • Below MA: Downtrend |

### 3. ML Prediction

```csharp
// Feature vector example
CryptoFeatures features = {
    Price: 43250.00,
    RSI: 28.45,
    MACD: 125.30,
    VolumeRatio: 1.34
    // ... more indicators
};

// AI prediction
TradingPrediction result = model.Predict(features);
// Output: { Action: "BUY", Confidence: 84.7% }
```

### 4. Trade Execution

Based on ML predictions:
- **BUY** signals trigger market/limit orders
- **SELL** signals close positions
- **HOLD** signals maintain current state

---

## Example Output

```
🤖 TradeNetics v1.0 - Starting Analysis...

📊 BTCUSDT Analysis
├── Price: $43,250.00 (-2.1% 24h)
├── RSI: 28.45 (Oversold)
├── MACD: 125.30 (Bullish crossover)
└── 🎯 ML Prediction: BUY (84.7% confidence)
    ✅ Placing buy order: 0.001 BTC

📊 ETHUSDT Analysis  
├── Price: $2,640.50 (+3.2% 24h)
├── RSI: 72.1 (Overbought) 
├── MACD: -45.80 (Bearish)
└── 🎯 ML Prediction: SELL (91.2% confidence)
    ❌ Placing sell order: 0.5 ETH

💼 Portfolio Summary
├── BTC: 0.00234 (Free) • $102.45
├── ETH: 0.48920 (Free) • $1,291.23  
└── USDT: 1,250.45 (Available)
```

---

## Architecture

### Core Components

```
📦 TradeNetics
├── 🤖 MLTradingModel        # AI prediction engine
├── 📊 TechnicalAnalysis     # Indicator calculations
├── 🌐 CryptoTraderService   # Binance integration
├── 📋 DataModels           # Market data structures
└── ⚙️ Configuration        # Settings & credentials
```

### Data Models

```csharp
// ML Features
public class CryptoFeatures
{
    public float Price { get; set; }
    public float RSI { get; set; }
    public float MACD { get; set; }
    // ... technical indicators
}

// Market Data
public class KlineData
{
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}
```

---

## Documentation

### Technical Indicators

<details>
<summary><strong>RSI (Relative Strength Index)</strong></summary>

Momentum oscillator measuring speed and magnitude of price changes.
- **Range**: 0-100
- **Overbought**: >70 (potential sell signal)
- **Oversold**: <30 (potential buy signal)
- **Calculation**: RSI = 100 - (100 / (1 + RS))

</details>

<details>
<summary><strong>MACD (Moving Average Convergence Divergence)</strong></summary>

Trend-following momentum indicator showing relationship between two moving averages.
- **Signal Line**: 9-period EMA of MACD
- **Bullish**: MACD above signal line
- **Bearish**: MACD below signal line
- **Crossovers**: Strong buy/sell signals

</details>

<details>
<summary><strong>Bollinger Bands</strong></summary>

Volatility indicator with upper and lower bands around moving average.
- **Middle Band**: 20-period SMA
- **Upper/Lower**: ±2 standard deviations
- **Squeeze**: Low volatility (potential breakout)
- **Expansion**: High volatility period

</details>

### API Configuration

```csharp
// Supported trading pairs
string[] symbols = { "BTCUSDT", "ETHUSDT", "ADAUSDT", "BNBUSDT" };

// Timeframe options
"1m", "5m", "15m", "1h", "4h", "1d"  // Binance intervals

// Order types
"MARKET"  // Immediate execution
"LIMIT"   // Price-specific execution
```

---

## Development

### Building

```bash
# Development build
dotnet build

# Release build  
dotnet build --configuration Release

# Run tests
dotnet test
```

### Environment Configuration

```bash
# Safe demo mode (synthetic data)
dotnet run --environment=Demo

# Testnet trading (fake money)
dotnet run --environment=Testnet  

# Live trading (real money - be careful!)
dotnet run --environment=Production
```

### Adding New Indicators

```csharp
// 1. Add calculation method
public static float CalculateStochastic(List<decimal> highs, List<decimal> lows, List<decimal> closes)
{
    // Implementation
}

// 2. Add to features model
public class CryptoFeatures 
{
    // ... existing properties
    public float Stochastic { get; set; }
}

// 3. Update feature extraction
features.Stochastic = TechnicalAnalysis.CalculateStochastic(highs, lows, closes);
```

---

## Contributing

We welcome contributions! Areas for improvement:

- 🔄 **New Indicators** - Stochastic, Williams %R, Ichimoku
- 🧠 **Advanced ML** - LSTM networks, ensemble methods
- 📊 **Backtesting** - Historical strategy validation
- 🛡️ **Risk Management** - Position sizing, stop-losses
- 📱 **Dashboard** - Web-based monitoring interface
- 🔗 **Exchanges** - Coinbase, Kraken, Bybit support

### Process

1. Fork the repository
2. Create feature branch (`git checkout -b feature/new-indicator`)
3. Commit changes (`git commit -m 'Add Stochastic oscillator'`)
4. Push branch (`git push origin feature/new-indicator`)
5. Open Pull Request

---

## Risk Disclaimer

> ⚠️ **IMPORTANT**: This software is for educational purposes only.
> 
> - Cryptocurrency trading involves substantial risk
> - Past performance does not guarantee future results  
> - Never invest more than you can afford to lose
> - Always test with paper trading or small amounts first
> - Do your own research (DYOR) before making investment decisions

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

---

## Support

- 🐛 **Issues**: [GitHub Issues](https://github.com/yourusername/TradeNetics/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/yourusername/TradeNetics/discussions)
- 📖 **Documentation**: [Wiki](https://github.com/yourusername/TradeNetics/wiki)

---

<div align="center">

**Built with** [ML.NET](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet) • [Binance API](https://binance-docs.github.io/apidocs/spot/en/) • [.NET 6](https://dotnet.microsoft.com/)

⭐ **Star this repo if it helped you learn ML.NET or algorithmic trading!**

</div>
