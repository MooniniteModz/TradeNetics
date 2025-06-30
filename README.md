# TradeNetics - Design Documentation

This document outlines the architecture and design of the TradeNetics application. The project is structured to be modular, testable, and maintainable, following modern .NET best practices.

## High-Level Architecture

The application is a .NET Core background service that continuously runs a trading cycle. It uses a layered architecture with a clear separation of concerns:

- **Dependency Injection:** The application heavily relies on dependency injection to manage the lifecycle of services and decouple components. This is configured in `Program.cs`.
- **Service-Oriented:** Functionality is broken down into distinct services, each with a single responsibility (e.g., interacting with the crypto exchange, managing the portfolio, making ML predictions).
- **Repository Pattern:** Database access is abstracted through a repository pattern (`IMarketDataRepository`), separating the application's business logic from the data access layer (Entity Framework Core).
- **Configuration-Driven:** Key parameters are managed through `appsettings.json` and environment variables, allowing for easy adjustments without changing the code.

## Project Structure and Namespaces

The code is organized into logical namespaces and corresponding files to make it easy to navigate and understand.

```
TradeNetics_Refactored/
├── TradeNetics.csproj
├── Program.cs
├── Models.cs
├── DataModels.cs
├── MLModels.cs
├── ApiModels.cs
├── Interfaces.cs
├── TechnicalAnalysis.cs
├── MarketDataRepository.cs
├── MLTradingModel.cs
├── RiskManager.cs
├── PortfolioManager.cs
├── CryptoTraderService.cs
├── BacktestEngine.cs
└── TradingBotService.cs
```

### Namespaces

#### `TradeNetics` (Root Namespace)
- **`Program.cs`**: The application's entry point. It is responsible for:
    - Setting up the dependency injection container.
    - Configuring services, logging, and application settings.
    - Loading the `TradingConfiguration` and ensuring API keys are present as environment variables.
    - Initializing the database.
    - Starting the `TradingBotService`.

#### `TradeNetics.Models`
This namespace contains all the Plain Old C# Object (POCO) models used throughout the application.
- **`Models.cs`**: Contains core configuration and enums.
    - `TradingConfiguration`: Holds all application settings.
    - `TradeAction`, `OrderType`: Enums to avoid "magic strings".
- **`ApiModels.cs`**: Contains models that map directly to the Binance API responses (e.g., `TickerPrice`, `OrderResponse`).
- **`MLModels.cs`**: Defines the data structures for the ML.NET model (`CryptoFeatures`, `TradingPrediction`).

#### `TradeNetics.Data`
This namespace is responsible for all data persistence concerns.
- **`DataModels.cs`**:
    - Defines the database entities (`MarketData`, `TradeRecord`, etc.).
    - `TradingContext`: The Entity Framework Core `DbContext` for interacting with the PostgreSQL database. It defines the table schemas and relationships.

#### `TradeNetics.Interfaces`
This file defines the contracts (interfaces) for all the services. Using interfaces is key to achieving loose coupling and enabling testability.
- `ICryptoTraderService`: Contract for interacting with the crypto exchange API.
- `IMarketDataRepository`: Contract for database operations.
- `IRiskManager`: Contract for enforcing risk management rules.
- `IPortfolioManager`: Contract for portfolio calculations and tracking.
- `IMLTradingModel`: Contract for the machine learning model's lifecycle (train, predict, save, load).

#### `TradeNetics.Services`
This namespace contains the concrete implementations of the interfaces defined in `TradeNetics.Interfaces`.
- **`TradingBotService.cs`**: The main background service that orchestrates the entire trading process. It runs in a continuous loop.
- **`CryptoTraderService.cs`**: Implements `ICryptoTraderService`. Handles all HTTP requests to the Binance API, including signing requests and handling retries with Polly.
- **`MarketDataRepository.cs`**: Implements `IMarketDataRepository`. Uses the `TradingContext` to perform CRUD operations on the database.
- **`MLTradingModel.cs`**: Implements `IMLTradingModel`. Manages training the ML.NET model, making predictions, and saving/loading the model from a file.
- **`PortfolioManager.cs`**: Implements `IPortfolioManager`. Calculates portfolio value, PnL, and other metrics.
- **`RiskManager.cs`**: Implements `IRiskManager`. Checks if proposed trades adhere to the configured risk parameters (e.g., max position size).
- **`BacktestEngine.cs`**: A service for simulating trading strategies on historical data to evaluate performance.

#### `TradeNetics.Helpers`
- **`TechnicalAnalysis.cs`**: A static utility class containing methods for calculating technical indicators like RSI, Moving Averages, Bollinger Bands, and MACD.

## Workflow

1.  **Initialization**: `Program.cs` builds the host, registers all services, and starts `TradingBotService`.
2.  **Model Loading**: `TradingBotService` first attempts to load a pre-trained `crypto_trading_model.zip`. If it doesn't exist, it generates synthetic data and trains a new model.
3.  **Trading Cycle**: The service enters a loop that runs every 5 minutes.
4.  **Portfolio Assessment**: It calls `IPortfolioManager` to get the current state of the portfolio.
5.  **Symbol Analysis**: For each symbol in the configuration (`TradingSymbols`):
    a. It calls `ICryptoTraderService.GetMLPredictionAsync()`.
    b. This service fetches the latest market data (prices, klines) from the API.
    c. It uses `TechnicalAnalysis` helper to calculate indicators and create a `CryptoFeatures` object.
    d. It passes the features to `IMLTradingModel.PredictAction()` to get a "BUY", "SELL", or "HOLD" prediction.
6.  **Decision & Execution**:
    a. Based on the prediction, `TradingBotService` decides whether to place an order.
    b. For a "BUY" order, it consults `IRiskManager.CanPlaceOrder()` to ensure the trade is within risk limits.
    c. If the order is approved, it calls `ICryptoTraderService.PlaceOrderAsync()`.
    d. The response from the executed order is used to create a `TradeRecord` which is saved to the database via the `TradingContext`.
7.  **Snapshot**: After each full cycle, `IPortfolioManager.SavePortfolioSnapshotAsync()` is called to record the portfolio's value over time.
8.  **Loop**: The service waits for the configured interval and starts the cycle again.
