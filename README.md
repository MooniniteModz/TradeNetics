# 🚀 TradeNetics

> **AI-Powered Cryptocurrency Trading Bot** with Real-time Dashboard & Machine Learning

[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-WebApp-blue.svg)](https://blazor.net/)
[![ML.NET](https://img.shields.io/badge/ML.NET-Enabled-green.svg)](https://dotnet.microsoft.com/apps/machinelearning-ai/ml-dotnet)
[![Binance API](https://img.shields.io/badge/Binance-API-yellow.svg)](https://binance-docs.github.io/apidocs/)

---

## ✨ Features

**🤖 Automated Trading**  
ML-powered buy/sell decisions with configurable risk management

**📊 Real-time Dashboard**  
Live portfolio tracking, dynamic charts, and trading activity

**🧠 Machine Learning**  
ML.NET models trained on market data for intelligent predictions

**⚡ Dual Architecture**  
Console app for automated trading + Web dashboard for monitoring

**🛡️ Risk Management**  
Stop-loss, position sizing, and daily loss limits

**🔄 Live Data Integration**  
Real-time portfolio sync with Binance API

---

## 🏃‍♂️ Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Binance API Keys](https://www.binance.com/en/support/faq/how-to-create-api-360002502072) (or Binance.us)

### 1. Clone & Build
```bash
git clone https://github.com/your-username/TradeNetics.git
cd TradeNetics
dotnet build
```

### 2. Start Web Dashboard
```bash
cd TradeNetics.WebApp
dotnet run
```
→ Open https://localhost:5001

### 3. Configure API Keys
1. Go to **Configuration** tab
2. Enter your Binance API credentials
3. Set Base API URL to `https://api.binance.us` (US users)
4. Save configuration

### 4. Run Trading Bot
```bash
cd TradeNetics.Console  
dotnet run
```

---

## 🎯 Architecture

```
TradeNetics/
├── 🖥️  TradeNetics.Console     # Automated trading engine
├── 🌐  TradeNetics.WebApp      # Real-time dashboard  
└── 📦  TradeNetics.Shared      # Common models & services
```

**Shared Library Design** eliminates code duplication and ensures consistency across applications.

---

## 💡 Dashboard Features

| Feature | Description |
|---------|-------------|
| **Portfolio Overview** | Real-time balance, holdings, and allocations |
| **Dynamic Charts** | Portfolio performance and trading activity |
| **Bot Controls** | Start/stop trading with live status |
| **Trading Pairs** | Manage active cryptocurrency pairs |
| **Configuration** | API keys, ML settings, and risk parameters |

---

## 🔧 Configuration

### Environment Variables (Optional)
```bash
export BINANCE_API_KEY="your_api_key"
export BINANCE_API_SECRET="your_api_secret"
```

### Web Configuration
All settings can be configured through the **Web Dashboard**:
- API credentials
- Trading parameters
- ML.NET algorithms
- Risk management rules

---

## 📈 Machine Learning

**Supported Algorithms:**
- FastTree
- LightGBM  
- FastForest
- GAM (Generalized Additive Models)

**Training Features:**
- Price movements
- Volume analysis
- Technical indicators (RSI, MACD, Bollinger Bands)
- Moving averages

---

## 🛡️ Safety Features

✅ **Paper Trading Mode** - Test without real money  
✅ **Geographic Compliance** - Binance.us support  
✅ **API Rate Limiting** - Prevents API violations  
✅ **Graceful Fallbacks** - Continues operation during API issues  
✅ **Real-time Monitoring** - Web dashboard oversight  

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.

---

<div align="center">

**⚠️ Trading Disclaimer**  
Cryptocurrency trading involves substantial risk. This software is for educational purposes.  
Always start with paper trading and never invest more than you can afford to lose.

**Made with ❤️ using .NET 8 & Blazor**

</div>