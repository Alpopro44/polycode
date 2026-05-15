import asyncio
import json
import logging
import uuid

from sync_manager import SyncManager
from repl_engine import REPLEngine
from session_manager import SessionManager
from websockets import WebSocketServerProtocol
from websockets.asyncio.server import serve

logger = logging.getLogger("PolyCode.Server")


class PolyCodeServer:
    def __init__(self, host: str = "127.0.0.1", port: int = 9765):
        self.host = host
        self.port = port
        self.sessions = SessionManager()
        self.connections: dict[str, tuple[WebSocketServerProtocol, str]] = {}

    async def handle_client(self, websocket: WebSocketServerProtocol):
        client_id = str(uuid.uuid4())[:8]
        current_session_id: str | None = None
        logger.info(f"Client connected: {client_id} from {websocket.remote_address}")

        try:
            async for raw in websocket:
                try:
                    msg = json.loads(raw)
                    response = await self._handle_message(
                        client_id, msg, websocket, current_session_id
                    )
                    if response:
                        if isinstance(response, tuple):
                            response, new_session_id = response
                            current_session_id = new_session_id
                        await self._send(websocket, response)
                except json.JSONDecodeError:
                    await self._send(websocket, {"type": "error", "message": "Invalid JSON"})

        except asyncio.CancelledError:
            pass
        finally:
            if current_session_id:
                session = self.sessions.get_session(current_session_id)
                if session:
                    session.remove_user(client_id)
                    await self._broadcast_session(current_session_id, {
                        "type": "user_left",
                        "data": {"user_id": client_id, "users": session.get_user_list()},
                    })
                    self._cleanup_session(current_session_id)
            self.connections.pop(client_id, None)
            logger.info(f"Client disconnected: {client_id}")

    async def _handle_message(self, client_id, msg, websocket, current_session_id):
        msg_type = msg.get("type", "")
        data = msg.get("data", {})

        if msg_type == "create_session":
            session = self.sessions.create_session(client_id)
            user_info = session.add_user(client_id)
            self.connections[client_id] = (websocket, session.session_id)
            return (
                {
                    "type": "session_created",
                    "data": {
                        "session_id": session.session_id,
                        "user_id": client_id,
                        "username": user_info["username"],
                        "color": user_info["color"],
                        "state": session.get_state(),
                    },
                },
                session.session_id,
            )

        if msg_type == "join_session":
            session_id = data.get("session_id", "")
            session = self.sessions.get_session(session_id)
            if not session:
                return {"type": "error", "message": f"Session '{session_id}' not found"}
            user_info = session.add_user(client_id)
            self.connections[client_id] = (websocket, session_id)
            await self._broadcast_session(session_id, {
                "type": "user_joined",
                "data": {"user_id": client_id, "users": session.get_user_list()},
            })
            return (
                {
                    "type": "session_joined",
                    "data": {
                        "session_id": session_id,
                        "user_id": client_id,
                        "username": user_info["username"],
                        "color": user_info["color"],
                        "state": session.get_state(),
                    },
                },
                session_id,
            )

        if not current_session_id:
            return {"type": "error", "message": "No active session"}

        session = self.sessions.get_session(current_session_id)
        if not session:
            return {"type": "error", "message": "Session not found"}

        if msg_type == "execute":
            code = data.get("code", "")
            result = session.repl.execute(code)
            await self._send(websocket, {
                "type": "exec_result",
                "user_id": client_id,
                "data": result,
            })
            await self._broadcast_session(current_session_id, {
                "type": "exec_notify",
                "user_id": client_id,
                "data": {"success": result["success"]},
            }, exclude=client_id)

        elif msg_type == "code_update":
            code = data.get("code", "")
            clock = data.get("lamport_clock", 0)
            session.sync.apply_code(code, clock)
            await self._broadcast_session(current_session_id, {
                "type": "code_update",
                "user_id": client_id,
                "data": {"code": code, "lamport_clock": session.sync.lamport_clock},
            })

        elif msg_type == "cursor_update":
            line = data.get("line", 0)
            column = data.get("column", 0)
            session.sync.set_cursor(client_id, line, column)
            await self._broadcast_session(current_session_id, {
                "type": "cursor_update",
                "user_id": client_id,
                "data": {"user_id": client_id, "line": line, "column": column},
            }, exclude=client_id)

        elif msg_type == "get_state":
            return {"type": "state", "data": session.get_state()}

        elif msg_type == "reset":
            session.repl.reset()
            return {"type": "reset_ack"}

        return None

    async def _send(self, websocket: WebSocketServerProtocol, msg: dict):
        try:
            await websocket.send(json.dumps(msg))
        except Exception as e:
            logger.warning(f"Send failed: {e}")

    async def _broadcast_session(self, session_id: str, msg: dict, exclude: str | None = None):
        for cid, (ws, sid) in list(self.connections.items()):
            if sid == session_id and cid != exclude:
                await self._send(ws, msg)

    def _cleanup_session(self, session_id: str):
        session = self.sessions.get_session(session_id)
        if session and not session.users:
            self.sessions.remove_session(session_id)
            logger.info(f"Session {session_id} removed (empty)")

    async def start(self):
        logger.info(f"PolyCode Server starting on ws://{self.host}:{self.port}")
        async with serve(self.handle_client, self.host, self.port):
            await asyncio.get_running_loop().create_future()
