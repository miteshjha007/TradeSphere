# TradeSphere

TradeSphere is a personal algorithmic trading workstation for testing, deploying, and monitoring rule-based strategies across crypto, MT5, and Indian market research workflows.

The app combines a .NET backend, Angular dashboard, PostgreSQL database, background trading engine, and an optional MT5 Python bridge. It is designed for local/private use first, especially where MT5 desktop automation is required.

> Disclaimer: TradeSphere is software for research, automation, and personal trading workflow experiments. It is not financial advice. Live trading, prop-firm trading, options trading, crypto futures, and leveraged instruments carry significant risk. Test thoroughly on demo/paper accounts before using real funds.

## What It Does

- Backtests strategy templates on historical candle data.
- Deploys strategies to a background trading engine for live scanning.
- Connects exchange/broker accounts and tests API connectivity.
- Places and reconciles live/demo trades through supported execution adapters.
- Tracks open positions, filled orders, failures, manual closes, SL/TP closes, break-even events, and trailing-stop updates.
- Provides a separate MT5 workflow for demo/prop-firm accounts through a local Windows MT5 terminal.
- Includes Indian market research sections for options desk, IPO intelligence, intraday picks, and long-term picks.

## Main Modules

- Dashboard: high-level account and strategy overview.
- Exchanges: connect and test supported crypto exchange accounts.
- Strategies: deploy, stop, and monitor live strategy instances.
- Backtesting: run strategy simulations by symbol, source, interval, and date range.
- Reports: monitor open broker positions, realized/unrealized P/L, failed orders, tickets, and trade lifecycle events.
- MT5: connect demo/live MT5 accounts through the local bridge and map broker symbols.
- Prop Firms: track funded challenge metadata and link accounts when needed.
- Indian Market: parent section for options desk, IPOs, intraday stock picks, and long-term stock picks.

## Supported Integrations

### Delta Exchange

- API key connection test.
- Wallet/balance display.
- Historical candle based backtesting.
- Strategy deployment through the trading engine.
- Order/report tracking.

### CoinDCX

- API key connection test.
- Coins and futures balance display.
- Historical data adapter support for backtesting.
- Execution integration foundation for strategy-based trading.

### MT5

MT5 support works through a local Python bridge because MT5 itself is a desktop terminal, not a cloud REST broker.

- MT5 demo/live account registration.
- Connection test through `mt5-bridge`.
- Broker symbol mapping, for example `XAUUSD`, `XAGUSD`, and forex pairs.
- Strategy execution through MT5 market orders.
- SL/TP order placement.
- Break-even and trailing-stop position modification.
- Report reconciliation from MT5 history.
- Manual close, stop-loss, take-profit, break-even, and trailing-stop reporting.

Important MT5 requirement:

- MT5 terminal must be running on the same Windows machine/VPS as the bridge.
- Account must be logged in.
- Algo Trading must be enabled.
- The Python bridge must be running.

## Strategy Support

Strategy templates are seeded from:

```text
TradeSphere.API/SeedData/strategy-templates.json
```

Current strategy work includes:

- HA-EMA Directional Bias Strategy
- Fib + 55 EMA V2 [Trend-Following Enhanced]
- Previous Day Liquidity Sweep Scalping Strategy
- SMC Multi-TF Strategy | D-L-E Framework
- ATR Fib Range Oscillator V2 - Refined BOS Strategy

Backtest and live execution logic is implemented per strategy. A strategy should only be enabled for live execution after its backtest logic has been validated against the intended market and timeframe.

## Risk Management

TradeSphere supports the following risk-management concepts where the broker adapter allows it:

- Initial stop loss and take profit.
- Fixed risk-reward exits.
- Strategy exit signals.
- Break-even stop movement.
- Trailing stop updates.
- Manual close reconciliation.
- Failed order logging.
- Insufficient fund/error reporting.

MT5 risk updates are recorded in Reports as activity rows such as:

- Entry
- Exit Signal
- Risk Update
- Closed
- Reconciled
- Failed

The report table also includes broker/source, account, symbol, ticket, side, quantity, price, P/L, status, and reason so that MT5 and exchange activity can be separated clearly.

## Architecture

The backend follows a Clean Architecture style:

```text
TradeSphere.Domain
  Core entities and domain models.

TradeSphere.Application
  DTOs, interfaces, and application contracts.

TradeSphere.Infrastructure
  EF Core database access, broker clients, backtest services, trading services.

TradeSphere.API
  ASP.NET Core Web API and seed data loading.

TradeSphere.TradingEngine
  Background worker that scans deployed strategies and triggers broker execution.

TradeSphere-UI
  Angular dashboard.

mt5-bridge
  Python bridge that talks to a locally running MT5 terminal.
```

## Tech Stack

- .NET 8
- ASP.NET Core Web API
- EF Core
- PostgreSQL
- Angular
- TailwindCSS / custom UI styling
- Python for MT5 bridge
- MetaTrader 5 terminal for MT5 execution

## Local Setup

### Prerequisites

- .NET 8 SDK
- Node.js LTS
- PostgreSQL
- Python 3.10+ for MT5 bridge
- MetaTrader 5 terminal if using MT5 execution

### Database

Create a PostgreSQL database and update the connection string in local appsettings files:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=TradeSphereDb;Username=your_user;Password=your_password"
  }
}
```

Do not commit real API keys, broker tokens, database passwords, or MT5 credentials.

### Run API

```bash
dotnet restore
dotnet run --project TradeSphere.API/TradeSphere.API.csproj
```

Default API URL:

```text
http://localhost:5083
```

### Run Angular UI

```bash
cd TradeSphere-UI
npm install
npm start
```

Default UI URL:

```text
http://localhost:4200
```

### Run Trading Engine

```bash
dotnet run --project TradeSphere.TradingEngine/TradeSphere.TradingEngine.csproj
```

### Run MT5 Bridge

```bash
cd mt5-bridge
python mt5_bridge.py
```

Or use the helper script if available:

```bash
mt5-bridge/start-mt5-bridge.cmd
```

Default bridge URL:

```text
http://127.0.0.1:8765
```

## Typical Local Workflow

1. Start PostgreSQL.
2. Start API.
3. Start Angular UI.
4. Start Trading Engine.
5. If using MT5, open MT5 terminal and start `mt5-bridge`.
6. Connect broker/exchange accounts.
7. Backtest strategies first.
8. Deploy only the strategy/symbol/timeframe combinations you want to test live.
9. Monitor Reports for entries, exits, failed orders, P/L, tickets, and reconciliation.

## Deployment Notes

The API, UI, and database can be deployed to cloud services such as:

- API: Render or similar .NET hosting
- UI: Vercel or similar static hosting
- DB: Neon/PostgreSQL

MT5 is different. MT5 requires a Windows desktop/VPS environment because the terminal must stay open and logged in. For MT5 automation, run:

```text
Windows VPS or local Windows machine
  MT5 Terminal
  mt5-bridge
```

Do not expose the MT5 bridge publicly without authentication, IP allowlisting, HTTPS, and firewall rules.

## Security Notes

- Never commit broker API keys.
- Never commit MT5 passwords.
- Never expose `mt5-bridge` without authentication.
- Use demo accounts before live execution.
- Keep prop-firm rules, lot sizes, daily drawdown, max drawdown, and news restrictions in mind.

## Development Commands

Build backend:

```bash
dotnet build TradeSphere.sln
```

Build UI:

```bash
cd TradeSphere-UI
npm run build
```

## Current Status

TradeSphere is actively evolving as a personal trading research and automation platform. The local workflow is currently the most practical setup because MT5 execution depends on a running terminal and bridge.
