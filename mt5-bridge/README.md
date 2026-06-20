# TradeSphere MT5 Bridge

This local bridge lets TradeSphere talk to the MetaTrader 5 terminal through the official `MetaTrader5` Python package.

## Setup

1. Install MetaTrader 5 on this Windows machine.
2. Install the Python package:

```bat
py -m pip install MetaTrader5
```

3. Start the bridge:

```bat
start-mt5-bridge.cmd
```

4. In TradeSphere, add your MT5 account and click `Test`.

## Endpoints

- `GET /health`
- `POST /account-info`
- `POST /positions`
- `POST /order/market`

The bridge only listens on `127.0.0.1:8765` by default.
