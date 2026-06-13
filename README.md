# TradeSphere

TradeSphere is a high-performance algorithmic and paper trading platform built with a .NET 8 Clean Architecture backend, a background Strategy Execution Engine, and a modern Angular 16+ dashboard frontend.

It enables traders to backtest historical data, run automated strategies in real-time using mock or live exchanges, and monitor trading metrics in a premium glassmorphic interface.

---

## 🚀 Key Features

*   **Algorithmic Backtesting Simulator**: Simulate historical trading performance using past candle data, charting equity curves, drawdowns, win rates, and logging detailed trade metrics.
*   **Strategy Execution Engine**: Background worker that processes live market tick/candle data, calculates technical indicators, evaluates buy/sell signals, and triggers execution.
*   **Built-in HA-EMA Directional Bias Strategy**:
    *   **Daily Trend Filter**: Uses Heikin Ashi daily candles to identify general trend direction (bullish/bearish).
    *   **Intraday Crossover Bands**: Utilizes 34-period EMA High/Low bands on intraday charts (e.g., 15m) for trade entries and exits.
    *   **Dynamic Risk Management**: Built-in options for ATR (Average True Range) Stop Loss, Fixed Risk-Reward ratio, and automatic intraday session Square-Off.
*   **Exchange Integrations**: Delta Exchange REST client integration for fetching candles and executing trades.
*   **Premium Angular Dashboard**: Sleek, fully responsive glassmorphism dark-mode UI with live strategy control panels, metrics cards, backtest logs, and account integration dialogs.

---

## 🏗️ Architecture & Project Structure

The project follows the principles of **Clean Architecture** for decoupling business rules from external frameworks:

*   `TradeSphere.Domain`: Core entities, models, and domain rules (e.g., `Backtest`, `Trade`, `Strategy`).
*   `TradeSphere.Application`: Interfaces, DTOs, and application logic.
*   `TradeSphere.Infrastructure`: Database context (`ApplicationDbContext`), PostgreSQL configuration, Identity/Authentication service, and REST API clients (Delta Exchange).
*   `TradeSphere.API`: ASP.NET Core Web API controllers serving the frontend application.
*   `TradeSphere.TradingEngine`: Background worker service running continuous strategy evaluations and paper trade triggers.
*   `TradeSphere-UI`: Angular frontend using TailwindCSS, Angular Material, and reactive components.

---

## 🛠️ Getting Started & Setup

### Prerequisites
*   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
*   [Node.js (LTS version)](https://nodejs.org/)
*   [PostgreSQL Database](https://www.postgresql.org/)

### Database Configuration
1.  Create a PostgreSQL database named `TradeSphereDb`.
2.  Import the database schema using the provided [TradeSphere_Schema.sql](file:///D:/Mitesh/StockMarket/Project/TradeSphere/TradeSphere_Schema.sql) file.
3.  Update the PostgreSQL connection string in `TradeSphere.API/appsettings.json`:
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Database=TradeSphereDb;Username=your_username;Password=your_password"
    }
    ```

### Running the Services

#### 1. Backend Web API
Navigate to the API project folder and start the server:
```bash
cd TradeSphere.API
dotnet restore
dotnet run
```
*   The API will listen on `http://localhost:5083`.
*   Swagger documentation is available at `http://localhost:5083/swagger`.

#### 2. Trading Engine Background Worker
Navigate to the engine project folder and start the worker:
```bash
cd ../TradeSphere.TradingEngine
dotnet run
```

#### 3. Angular UI Frontend
Navigate to the frontend folder, install dependencies, and start the development server:
```bash
cd ../TradeSphere-UI
npm install
npm start
```
*   The frontend will run on `http://localhost:4200`.

---

## 📊 Strategy: HA-EMA Directional Bias

This built-in strategy is designed for intraday momentum trading:

1.  **Trend Bias (Daily Heikin Ashi)**: 
    *   If yesterday's daily Heikin Ashi candle is green (`Close > Open`), the bias is **Long-only**.
    *   If yesterday's daily Heikin Ashi candle is red (`Close < Open`), the bias is **Short-only**.
2.  **Entry Trigger**:
    *   **Long Entry**: When Daily Bias is Bullish and the intraday Close price crosses above the upper EMA High band.
    *   **Short Entry**: When Daily Bias is Bearish and the intraday Close price crosses below the lower EMA Low band.
3.  **Exit & Risk Management**:
    *   **Band-Based Exit**: Closes the trade if price crosses back through the opposite band.
    *   **Fixed Risk-Reward**: Sets target profits and stop losses using a fixed R:R ratio, optionally calculated using a multiplier of the ATR indicator.
    *   **Session Square-Off**: Automatically exits all open trades at a configured intraday time (e.g. 15:15 IST).
