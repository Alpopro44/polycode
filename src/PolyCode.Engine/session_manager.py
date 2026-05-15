import uuid
import string
import random

from sync_manager import SyncManager
from repl_engine import REPLEngine


def _generate_session_id() -> str:
    return "".join(random.choices(string.ascii_lowercase + string.digits, k=6))


class Session:
    def __init__(self, session_id: str, creator_id: str):
        self.session_id = session_id
        self.creator_id = creator_id
        self.sync = SyncManager()
        self.repl = REPLEngine()
        self.users: dict[str, dict] = {}
        self.created_at = 0

    def add_user(self, user_id: str, username: str = "") -> dict:
        color = self._assign_color(len(self.users))
        info = {
            "user_id": user_id,
            "username": username or f"User_{user_id[:4]}",
            "color": color,
        }
        self.users[user_id] = info
        self.sync.cursors[user_id] = type(
            "obj", (object,), {"user_id": user_id, "line": 0, "column": 0}
        )()
        return info

    def remove_user(self, user_id: str):
        self.users.pop(user_id, None)
        self.sync.remove_user(user_id)

    def get_user_list(self) -> list[dict]:
        return [
            {
                "user_id": uid,
                "username": info["username"],
                "color": info["color"],
                "cursor": {
                    "line": getattr(self.sync.cursors.get(uid), "line", 0),
                    "column": getattr(self.sync.cursors.get(uid), "column", 0),
                },
            }
            for uid, info in self.users.items()
        ]

    def get_state(self) -> dict:
        return {
            "session_id": self.session_id,
            "code_buffer": self.sync.code_buffer,
            "users": self.get_user_list(),
            "user_count": len(self.users),
        }

    def _assign_color(self, index: int) -> str:
        palette = [
            "#F5E0DC", "#F2CDCD", "#F5C2E7", "#CBA6F7",
            "#F38BA8", "#EBA0AC", "#FAB387", "#F9E2AF",
            "#A6E3A1", "#94E2D5", "#89DCEB", "#74C7EC",
        ]
        return palette[index % len(palette)]


class SessionManager:
    def __init__(self):
        self.sessions: dict[str, Session] = {}

    def create_session(self, user_id: str) -> Session:
        session_id = _generate_session_id()
        while session_id in self.sessions:
            session_id = _generate_session_id()
        session = Session(session_id, user_id)
        self.sessions[session_id] = session
        return session

    def get_session(self, session_id: str) -> Session | None:
        return self.sessions.get(session_id)

    def remove_session(self, session_id: str):
        self.sessions.pop(session_id, None)
