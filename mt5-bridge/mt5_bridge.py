import json
from http.server import BaseHTTPRequestHandler, HTTPServer
from typing import Any, Dict, Tuple

HOST = "127.0.0.1"
PORT = 8765


def response(success: bool, message: str, **kwargs: Any) -> Dict[str, Any]:
    payload = {"success": success, "message": message}
    payload.update(kwargs)
    return payload


def import_mt5() -> Tuple[Any, str | None]:
    try:
        import MetaTrader5 as mt5  # type: ignore
        return mt5, None
    except Exception as exc:
        return None, (
            "MetaTrader5 Python package is not installed or cannot be loaded. "
            "Install it with: py -m pip install MetaTrader5. Details: "
            f"{exc}"
        )


def connect_mt5(payload: Dict[str, Any]) -> Tuple[Any, Dict[str, Any] | None]:
    mt5, error = import_mt5()
    if error:
        return None, response(False, error)

    login = int(read_value(payload, "login") or 0)
    password = str(read_value(payload, "password") or "")
    server = str(read_value(payload, "server") or "")

    if login <= 0 or not password or not server:
        return None, response(
            False,
            "login, password, and server are required. "
            f"Received login={login}, serverPresent={bool(server)}, passwordPresent={bool(password)}.",
        )

    if not mt5.initialize(login=login, password=password, server=server):
        code, description = mt5.last_error()
        return None, response(False, f"MT5 initialize/login failed: {code} {description}")

    return mt5, None


def read_value(payload: Dict[str, Any], key: str) -> Any:
    """Accept both camelCase/lowercase and .NET PascalCase JSON fields."""
    return payload.get(key) or payload.get(key[:1].upper() + key[1:])


class Mt5BridgeHandler(BaseHTTPRequestHandler):
    def do_GET(self) -> None:
        if self.path == "/health":
            mt5, error = import_mt5()
            if error:
                self.write_json(503, response(False, error, status="BridgeOnlineMt5PackageMissing"))
                return
            self.write_json(200, response(True, "MT5 bridge is running.", status="BridgeOnline"))
            return

        self.write_json(404, response(False, "Endpoint not found."))

    def do_POST(self) -> None:
        payload = self.read_json()

        if self.path == "/account-info":
            self.handle_account_info(payload)
            return

        if self.path == "/order/market":
            self.handle_market_order(payload)
            return

        if self.path == "/position/close":
            self.handle_close_position(payload)
            return

        if self.path == "/positions":
            self.handle_positions(payload)
            return

        if self.path == "/tick":
            self.handle_tick(payload)
            return

        if self.path == "/history/deals":
            self.handle_history_deals(payload)
            return

        if self.path == "/candles":
            self.handle_candles(payload)
            return

        self.write_json(404, response(False, "Endpoint not found."))

    def handle_account_info(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            account = mt5.account_info()
            if account is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Could not read account info: {code} {description}"))
                return

            data = account._asdict()
            self.write_json(
                200,
                response(
                    True,
                    "MT5 account connected successfully.",
                    login=data.get("login"),
                    server=data.get("server"),
                    currency=data.get("currency"),
                    leverage=data.get("leverage"),
                    balance=data.get("balance"),
                    equity=data.get("equity"),
                    freeMargin=data.get("margin_free"),
                ),
            )
        finally:
            mt5.shutdown()

    def handle_candles(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            symbol = str(read_value(payload, "symbol") or "").strip()
            resolution = str(read_value(payload, "resolution") or "5m").strip()
            start = int(read_value(payload, "startTime") or 0)
            end = int(read_value(payload, "endTime") or 0)

            if not symbol or start <= 0 or end <= 0:
                self.write_json(400, response(False, "symbol, startTime, and endTime are required."))
                return

            timeframe = self.map_timeframe(mt5, resolution)
            if timeframe is None:
                self.write_json(400, response(False, f"Unsupported MT5 timeframe/resolution: {resolution}"))
                return

            if not mt5.symbol_select(symbol, True):
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Symbol {symbol} is not available: {code} {description}"))
                return

            import datetime as dt
            utc_from = dt.datetime.fromtimestamp(start, tz=dt.timezone.utc)
            utc_to = dt.datetime.fromtimestamp(end, tz=dt.timezone.utc)
            rates = mt5.copy_rates_range(symbol, timeframe, utc_from, utc_to)
            if rates is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Could not read candles: {code} {description}"))
                return

            candles = []
            for rate in rates:
                candles.append(
                    {
                        "time": int(rate["time"]),
                        "open": float(rate["open"]),
                        "high": float(rate["high"]),
                        "low": float(rate["low"]),
                        "close": float(rate["close"]),
                        "volume": float(rate["tick_volume"]),
                    }
                )

            self.write_json(200, response(True, "Candles loaded.", candles=candles))
        finally:
            mt5.shutdown()

    def map_timeframe(self, mt5: Any, resolution: str) -> Any:
        mapping = {
            "1m": mt5.TIMEFRAME_M1,
            "3m": mt5.TIMEFRAME_M3,
            "5m": mt5.TIMEFRAME_M5,
            "15m": mt5.TIMEFRAME_M15,
            "30m": mt5.TIMEFRAME_M30,
            "1h": mt5.TIMEFRAME_H1,
            "2h": mt5.TIMEFRAME_H2,
            "4h": mt5.TIMEFRAME_H4,
            "1d": mt5.TIMEFRAME_D1,
            "1": mt5.TIMEFRAME_M1,
            "3": mt5.TIMEFRAME_M3,
            "5": mt5.TIMEFRAME_M5,
            "15": mt5.TIMEFRAME_M15,
            "30": mt5.TIMEFRAME_M30,
            "60": mt5.TIMEFRAME_H1,
            "120": mt5.TIMEFRAME_H2,
            "240": mt5.TIMEFRAME_H4,
            "D": mt5.TIMEFRAME_D1,
        }
        return mapping.get(resolution)

    def handle_positions(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            symbol = read_value(payload, "symbol")
            positions = mt5.positions_get(symbol=symbol) if symbol else mt5.positions_get()
            if positions is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Could not read positions: {code} {description}"))
                return

            self.write_json(200, response(True, "Positions loaded.", positions=[p._asdict() for p in positions]))
        finally:
            mt5.shutdown()

    def handle_tick(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            symbol = str(read_value(payload, "symbol") or "").strip()
            if not symbol:
                self.write_json(400, response(False, "symbol is required."))
                return

            if not mt5.symbol_select(symbol, True):
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Symbol {symbol} is not available: {code} {description}"))
                return

            tick = mt5.symbol_info_tick(symbol)
            if tick is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"No tick data for {symbol}: {code} {description}"))
                return

            data = tick._asdict()
            self.write_json(
                200,
                response(
                    True,
                    "Tick loaded.",
                    symbol=symbol,
                    bid=data.get("bid"),
                    ask=data.get("ask"),
                    last=data.get("last"),
                    time=data.get("time"),
                ),
            )
        finally:
            mt5.shutdown()

    def handle_history_deals(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            import datetime as dt

            symbol = str(read_value(payload, "symbol") or "").strip()
            start = int(read_value(payload, "startTime") or 0)
            end = int(read_value(payload, "endTime") or 0)

            if start <= 0 or end <= 0:
                self.write_json(400, response(False, "startTime and endTime are required."))
                return

            utc_from = dt.datetime.fromtimestamp(start, tz=dt.timezone.utc)
            utc_to = dt.datetime.fromtimestamp(end, tz=dt.timezone.utc)
            deals = mt5.history_deals_get(utc_from, utc_to)
            if deals is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Could not read MT5 history deals: {code} {description}"))
                return

            data = []
            for deal in deals:
                item = deal._asdict()
                if symbol and str(item.get("symbol") or "").upper() != symbol.upper():
                    continue
                data.append(item)

            self.write_json(200, response(True, "History deals loaded.", deals=data))
        finally:
            mt5.shutdown()

    def handle_close_position(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            symbol = str(read_value(payload, "symbol") or "").strip()
            position_ticket = int(read_value(payload, "positionTicket") or read_value(payload, "position") or 0)
            volume = float(read_value(payload, "volume") or 0)

            if not symbol or position_ticket <= 0 or volume <= 0:
                self.write_json(400, response(False, "symbol, positionTicket, and positive volume are required."))
                return

            positions = mt5.positions_get(ticket=position_ticket)
            if positions is None or len(positions) == 0:
                self.write_json(400, response(False, f"Open MT5 position {position_ticket} was not found."))
                return

            position_data = positions[0]._asdict()
            if str(position_data.get("symbol") or "").upper() != symbol.upper():
                self.write_json(400, response(False, f"Position {position_ticket} does not belong to symbol {symbol}."))
                return

            if not mt5.symbol_select(symbol, True):
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Symbol {symbol} is not available/tradable: {code} {description}"))
                return

            tick = mt5.symbol_info_tick(symbol)
            if tick is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"No tick data for {symbol}: {code} {description}"))
                return

            is_buy_position = int(position_data.get("type") or 0) == mt5.POSITION_TYPE_BUY
            order_type = mt5.ORDER_TYPE_SELL if is_buy_position else mt5.ORDER_TYPE_BUY
            price = tick.bid if is_buy_position else tick.ask
            request = {
                "action": mt5.TRADE_ACTION_DEAL,
                "symbol": symbol,
                "volume": min(volume, float(position_data.get("volume") or volume)),
                "type": order_type,
                "position": position_ticket,
                "price": price,
                "deviation": int(read_value(payload, "deviation") or 30),
                "magic": int(read_value(payload, "magic") or 20260616),
                "comment": str(read_value(payload, "comment") or "TradeSphere MT5 close"),
                "type_time": mt5.ORDER_TIME_GTC,
                "type_filling": mt5.ORDER_FILLING_IOC,
            }

            result = mt5.order_send(request)
            if result is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"position close failed: {code} {description}"))
                return

            raw = result._asdict()
            success = raw.get("retcode") in (mt5.TRADE_RETCODE_DONE, mt5.TRADE_RETCODE_PLACED)
            self.write_json(
                200 if success else 400,
                response(
                    success,
                    raw.get("comment") or ("Position closed." if success else "Position close rejected by MT5."),
                    orderId=str(raw.get("order") or ""),
                    dealId=str(raw.get("deal") or ""),
                    price=raw.get("price"),
                    rawResponse=json.dumps(raw, default=str),
                ),
            )
        finally:
            mt5.shutdown()

    def handle_market_order(self, payload: Dict[str, Any]) -> None:
        mt5, error_response = connect_mt5(payload)
        if error_response:
            self.write_json(400, error_response)
            return

        try:
            symbol = str(read_value(payload, "symbol") or "").strip()
            side = str(read_value(payload, "side") or "").strip().lower()
            volume = float(read_value(payload, "volume") or 0)

            if not symbol or side not in ("buy", "sell") or volume <= 0:
                self.write_json(400, response(False, "symbol, side buy/sell, and positive volume are required."))
                return

            if not mt5.symbol_select(symbol, True):
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"Symbol {symbol} is not available/tradable: {code} {description}"))
                return

            tick = mt5.symbol_info_tick(symbol)
            if tick is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"No tick data for {symbol}: {code} {description}"))
                return

            order_type = mt5.ORDER_TYPE_BUY if side == "buy" else mt5.ORDER_TYPE_SELL
            price = tick.ask if side == "buy" else tick.bid
            request = {
                "action": mt5.TRADE_ACTION_DEAL,
                "symbol": symbol,
                "volume": volume,
                "type": order_type,
                "price": price,
                "deviation": int(read_value(payload, "deviation") or 30),
                "magic": int(read_value(payload, "magic") or 20260616),
                "comment": str(read_value(payload, "comment") or "TradeSphere MT5"),
                "type_time": mt5.ORDER_TIME_GTC,
                "type_filling": mt5.ORDER_FILLING_IOC,
            }

            stop_loss = read_value(payload, "stopLoss")
            take_profit = read_value(payload, "takeProfit")
            if stop_loss is not None:
                request["sl"] = float(stop_loss)
            if take_profit is not None:
                request["tp"] = float(take_profit)

            result = mt5.order_send(request)
            if result is None:
                code, description = mt5.last_error()
                self.write_json(400, response(False, f"order_send failed: {code} {description}"))
                return

            raw = result._asdict()
            success = raw.get("retcode") in (mt5.TRADE_RETCODE_DONE, mt5.TRADE_RETCODE_PLACED)
            self.write_json(
                200 if success else 400,
                response(
                    success,
                    raw.get("comment") or ("Order placed." if success else "Order rejected by MT5."),
                    orderId=str(raw.get("order") or ""),
                    dealId=str(raw.get("deal") or ""),
                    price=raw.get("price"),
                    rawResponse=json.dumps(raw, default=str),
                ),
            )
        finally:
            mt5.shutdown()

    def read_json(self) -> Dict[str, Any]:
        length = int(self.headers.get("Content-Length") or 0)
        if length <= 0:
            return {}
        raw = self.rfile.read(length).decode("utf-8")
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            return {}

    def write_json(self, status_code: int, payload: Dict[str, Any]) -> None:
        body = json.dumps(payload, default=str).encode("utf-8")
        self.send_response(status_code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format: str, *args: Any) -> None:
        print(f"[MT5 Bridge] {self.address_string()} - {format % args}")


def main() -> None:
    server = HTTPServer((HOST, PORT), Mt5BridgeHandler)
    print(f"TradeSphere MT5 bridge listening on http://{HOST}:{PORT}")
    print("Keep MetaTrader 5 installed on this Windows machine. AutoTrading must be enabled for live order execution.")
    server.serve_forever()


if __name__ == "__main__":
    main()
