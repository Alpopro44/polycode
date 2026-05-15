import asyncio
import json
import logging

from repl_engine import REPLEngine
from sync_manager import SyncManager

logger = logging.getLogger("PolyCode.Server")


class PolyCodeServer:
    def __init__(self, host: str = "127.0.0.1", port: int = 9765):
        self.host = host
        self.port = port
        self.repl = REPLEngine()
        self.sync = SyncManager()
        self.clients: dict[str, asyncio.StreamWriter] = {}
        self.sessions: dict[str, REPLEngine] = {}
        self._counter = 0

    async def handle_client(self, reader: asyncio.StreamReader, writer: asyncio.StreamWriter):
        self._counter += 1
        client_id = f"user_{self._counter:04d}"
        self.clients[client_id] = writer
        self.sessions[client_id] = REPLEngine()
        addr = writer.get_extra_info("peername")
        logger.info(f"Client connected: {client_id} ({addr})")

        try:
            await self._send(writer, {
                "type": "welcome",
                "user_id": client_id,
                "state": self.sync.get_state(),
            })

            while True:
                line = await reader.readline()
                if not line:
                    break
                try:
                    msg = json.loads(line.decode())
                    await self._handle_message(client_id, msg)
                except json.JSONDecodeError as e:
                    await self._send(writer, {"type": "error", "message": f"Invalid JSON: {e}"})

        except (asyncio.CancelledError, ConnectionResetError, BrokenPipeError):
            pass
        finally:
            self.clients.pop(client_id, None)
            self.sessions.pop(client_id, None)
            self.sync.cursors.pop(client_id, None)
            try:
                writer.close()
            except Exception:
                pass
            logger.info(f"Client disconnected: {client_id}")

    async def _handle_message(self, client_id: str, msg: dict):
        msg_type = msg.get("type", "")
        data = msg.get("data", {})

        if msg_type == "execute":
            code = data.get("code", "")
            engine = self.sessions.get(client_id, self.repl)
            result = engine.execute(code)
            await self._send_to(client_id, {
                "type": "exec_result",
                "user_id": client_id,
                "data": result,
            })
            await self._broadcast({
                "type": "exec_notify",
                "user_id": client_id,
                "data": {"success": result["success"]},
            }, exclude=client_id)

        elif msg_type == "code_update":
            code = data.get("code", "")
            op = self.sync.set_code(code)
            await self._broadcast({
                "type": "code_update",
                "user_id": client_id,
                "data": {
                    "code": code,
                    "lamport_clock": op.lamport_clock,
                    "op_id": op.op_id,
                },
            })

        elif msg_type == "cursor_update":
            cursor = data.get("cursor", {})
            op = self.sync.set_cursor(cursor.get("line", 0), cursor.get("column", 0))
            await self._broadcast({
                "type": "cursor_update",
                "user_id": client_id,
                "data": {
                    "user_id": client_id,
                    "line": cursor.get("line", 0),
                    "column": cursor.get("column", 0),
                },
            }, exclude=client_id)

        elif msg_type == "get_state":
            await self._send_to(client_id, {
                "type": "state",
                "data": self.sync.get_state(),
            })

        elif msg_type == "reset":
            if client_id in self.sessions:
                self.sessions[client_id].reset()
            await self._send_to(client_id, {"type": "reset_ack"})

    async def _send(self, writer: asyncio.StreamWriter, msg: dict):
        data = (json.dumps(msg) + "\n").encode()
        writer.write(data)
        await writer.drain()

    async def _send_to(self, client_id: str, msg: dict):
        writer = self.clients.get(client_id)
        if writer:
            await self._send(writer, msg)

    async def _broadcast(self, msg: dict, exclude: str | None = None):
        for cid, writer in list(self.clients.items()):
            if cid != exclude:
                try:
                    await self._send(writer, msg)
                except Exception:
                    pass

    async def start(self):
        server = await asyncio.start_server(self.handle_client, self.host, self.port)
        logger.info(f"PolyCode Server listening on {self.host}:{self.port}")
        async with server:
            await server.serve_forever()


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    asyncio.run(PolyCodeServer().start())
