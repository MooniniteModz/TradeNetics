# Trader Walk-Through: Using the TradeNetics App

This guide provides a detailed walk-through of how to use the TradeNetics application, from understanding the output to customizing your trading strategy.

## 1. Understanding the Console Output

When you run the application, you'll see a stream of log messages in your console. Here's what they mean:

*   **`Trading bot service started`**: The application has successfully initialized.
*   **`Loaded existing ML model` / `Training new ML model...`**: The bot is either loading a pre-trained model or creating a new one.
*   **`Starting trading cycle...`**: A new trading cycle is beginning. This happens every 5 minutes by default.
*   **`Analyzing {Symbol}...`**: The bot is analyzing a specific cryptocurrency (e.g., `BTCUSDT`).
*   **`{Symbol}: Price=${Price:F2}, Prediction={Prediction}`**: The bot has made a prediction for the symbol at the current price.
*   **`BUY order executed` / `SELL order executed`**: A trade has been placed.
*   **`BUY order rejected by risk manager`**: A potential trade was blocked because it didn't meet the risk criteria.
*   **`No position to sell for {Symbol}`**: The bot predicted a "SELL", but you don't own any of that cryptocurrency.

## 2. Customizing Your Trading Strategy

You can customize the trading strategy by modifying the `appsettings.json` file.

### `TradingSymbols`

This is an array of the cryptocurrency symbols you want to trade.

```json
"TradingSymbols": ["BTCUSDT", "ETHUSDT", "SOLUSDT"]
```

### `BacktestInitialBalance`

The initial balance to use when running a backtest.

```json
"BacktestInitialBalance": 10000
```

### `MinConfidenceScore`

The minimum confidence score required for the bot to place a trade. This is a value between 0 and 1. A higher value means the bot will be more selective about its trades.

```json
"MinConfidenceScore": 0.75
```

### `PaperTradingMode`

When set to `true`, the bot will simulate trades without using real money. This is useful for testing your strategy.

```json
"PaperTradingMode": true
```

### `SymbolQuantities`

The amount of each cryptocurrency to buy or sell in a single trade.

```json
"SymbolQuantities": {
    "BTCUSDT": 0.001,
    "ETHUSDT": 0.01,
    "SOLUSDT": 0.1
}
```

## 3. Backtesting Your Strategy

The backtesting engine allows you to test your trading strategy on historical data. To run a backtest, you'll need to modify the `Trader-Main.cs` file to call the `BacktestEngine`.

*This feature is still under development. Instructions on how to run a backtest will be added soon.*

## 4. Viewing Your Trade History

All trades are recorded in the PostgreSQL database. You can use a database client like DBeaver or pgAdmin to connect to the database and view the `TradeRecords` table.

## 5. Next Steps

Now that you have a basic understanding of how to use the TradeNetics app, you can start experimenting with different trading strategies and configurations. Remember to always test your strategies in paper trading mode before using real money.
