import sys
sys.path.insert(0, "..")

from sync_manager import SyncManager


def test_initial_state():
    sm = SyncManager("test_user")
    state = sm.get_state()
    assert state["code_buffer"] == ""
    assert state["user_count"] == 0


def test_set_code():
    sm = SyncManager("test_user")
    sm.set_code("print('hello')")
    assert sm.code_buffer == "print('hello')"


def test_set_cursor():
    sm = SyncManager("test_user")
    sm.set_cursor("test_user", 5, 10)
    assert sm.cursors["test_user"].line == 5
    assert sm.cursors["test_user"].column == 10


def test_lamport_clock():
    sm = SyncManager("test_user")
    initial = sm.lamport_clock
    sm.tick()
    assert sm.lamport_clock == initial + 1


def test_apply_code_outdated():
    sm = SyncManager("test_user")
    sm.apply_code("new_code", clock=10)
    assert sm.code_buffer == "new_code"
    assert sm.lamport_clock >= 10


def test_remove_user():
    sm = SyncManager("test_user")
    sm.set_cursor("user_a", 1, 1)
    sm.set_cursor("user_b", 2, 2)
    assert len(sm.cursors) == 2
    sm.remove_user("user_a")
    assert len(sm.cursors) == 1
    assert "user_b" in sm.cursors


def test_get_state():
    sm = SyncManager("test_user")
    sm.set_code("x = 1")
    sm.set_cursor("test_user", 3, 5)
    state = sm.get_state()
    assert state["code_buffer"] == "x = 1"
    assert state["user_count"] == 1
    assert state["cursors"]["test_user"]["line"] == 3


def test_multiple_cursors():
    sm = SyncManager("test_user")
    sm.set_cursor("alice", 1, 1)
    sm.set_cursor("bob", 2, 2)
    sm.set_cursor("charlie", 3, 3)
    assert len(sm.cursors) == 3
    state = sm.get_state()
    assert state["user_count"] == 3


def test_reset():
    sm = SyncManager("test_user")
    sm.set_code("print('hello')")
    sm.set_cursor("test_user", 1, 1)
    sm.reset()
    assert sm.code_buffer == ""
    assert sm.lamport_clock == 0
    assert len(sm.cursors) == 0
