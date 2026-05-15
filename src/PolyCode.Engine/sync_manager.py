import time
import uuid
from dataclasses import dataclass, field
from typing import Any


@dataclass
class CursorPosition:
    user_id: str
    line: int = 0
    column: int = 0
    selection_start: tuple | None = None
    selection_end: tuple | None = None


@dataclass
class CodeOperation:
    op_id: str
    user_id: str
    op_type: str
    data: Any
    timestamp: float
    lamport_clock: int


class SyncManager:
    def __init__(self, user_id: str | None = None):
        self.user_id = user_id or str(uuid.uuid4())[:8]
        self.lamport_clock = 0
        self.cursors: dict[str, CursorPosition] = {}
        self.code_buffer: str = ""
        self.operations: list[CodeOperation] = []
        self._listeners: list = []

    def tick(self) -> int:
        self.lamport_clock += 1
        return self.lamport_clock

    def update_clock(self, remote_clock: int):
        self.lamport_clock = max(self.lamport_clock, remote_clock) + 1

    def create_operation(self, op_type: str, data: Any) -> CodeOperation:
        op = CodeOperation(
            op_id=str(uuid.uuid4()),
            user_id=self.user_id,
            op_type=op_type,
            data=data,
            timestamp=time.time(),
            lamport_clock=self.tick(),
        )
        self.operations.append(op)
        return op

    def apply_remote_operation(self, op: dict) -> bool:
        if op["op_type"] == "cursor":
            self.cursors[op["user_id"]] = CursorPosition(**op["data"])
            self.update_clock(op["lamport_clock"])
            self._notify()
            return True

        if op["op_type"] == "code_full":
            if op["lamport_clock"] > self.lamport_clock:
                self.code_buffer = op["data"]
                self.update_clock(op["lamport_clock"])
                self._notify()
                return True
        return False

    def set_code(self, code: str) -> CodeOperation:
        self.code_buffer = code
        op = self.create_operation("code_full", code)
        self._notify()
        return op

    def set_cursor(self, line: int, column: int) -> CodeOperation:
        self.cursors[self.user_id] = CursorPosition(
            user_id=self.user_id, line=line, column=column
        )
        op = self.create_operation(
            "cursor",
            {"user_id": self.user_id, "line": line, "column": column},
        )
        self._notify()
        return op

    def get_state(self) -> dict:
        return {
            "code_buffer": self.code_buffer,
            "cursors": {
                uid: {
                    "user_id": c.user_id,
                    "line": c.line,
                    "column": c.column,
                }
                for uid, c in self.cursors.items()
            },
            "user_count": len(self.cursors),
        }

    def on_change(self, callback):
        self._listeners.append(callback)

    def _notify(self):
        for cb in self._listeners:
            cb(self.get_state())
