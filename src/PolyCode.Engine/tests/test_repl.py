import sys
sys.path.insert(0, "..")

from repl_engine import REPLEngine


def test_simple_print():
    engine = REPLEngine()
    result = engine.execute("print('hello')")
    assert result["success"]
    assert result["output"] == "hello\n"


def test_arithmetic():
    engine = REPLEngine()
    result = engine.execute("x = 42\ny = x * 2\nprint(y)")
    assert result["success"]
    assert "84" in result["output"]


def test_last_expression():
    engine = REPLEngine()
    result = engine.execute("1 + 2")
    assert result["success"]
    assert "3" in result["output"]


def test_syntax_error():
    engine = REPLEngine()
    result = engine.execute("print('unclosed")
    assert not result["success"]
    assert "SyntaxError" in result["error"]


def test_runtime_error():
    engine = REPLEngine()
    result = engine.execute("1/0")
    assert not result["success"]
    assert "ZeroDivisionError" in result["error"]


def test_variables_persist():
    engine = REPLEngine()
    engine.execute("x = 42")
    result = engine.execute("print(x)")
    assert result["output"] == "42\n"


def test_variable_list():
    engine = REPLEngine()
    engine.execute("a = 1\nb = 'hello'\nc = [1,2,3]")
    result = engine.execute("")
    assert "a" in result["variables"]
    assert "b" in result["variables"]
    assert "c" in result["variables"]


def test_reset():
    engine = REPLEngine()
    engine.execute("x = 42")
    engine.reset()
    result = engine.execute("print(x)")
    assert "NameError" in result["error"]


def test_multiline():
    engine = REPLEngine()
    code = """
for i in range(3):
    print(f'line {i}')
"""
    result = engine.execute(code)
    assert result["success"]
    assert "line 0" in result["output"]
    assert "line 2" in result["output"]


def test_function_definition():
    engine = REPLEngine()
    engine.execute("def add(a, b):\n    return a + b")
    result = engine.execute("print(add(3, 4))")
    assert result["success"]
    assert "7" in result["output"]
