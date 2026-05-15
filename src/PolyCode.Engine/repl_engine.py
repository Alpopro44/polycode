import sys
import io
import ast
import traceback
from contextlib import redirect_stdout, redirect_stderr


class REPLEngine:
    def __init__(self):
        self.global_ns = {"__builtins__": __builtins__}
        self.local_ns = {}

    def execute(self, code: str) -> dict:
        stdout_capture = io.StringIO()
        stderr_capture = io.StringIO()
        result = {"success": True, "output": "", "error": "", "variables": {}}

        try:
            tree = ast.parse(code)
            last_expr = None
            if tree.body and isinstance(tree.body[-1], ast.Expr):
                last_expr = tree.body.pop()

            with redirect_stdout(stdout_capture), redirect_stderr(stderr_capture):
                for node in tree.body:
                    exec(
                        compile(ast.Module(body=[node], type_ignores=[]), "<repl>", "exec"),
                        self.global_ns,
                        self.local_ns,
                    )

                if last_expr:
                    expr_result = eval(
                        compile(ast.Expression(body=last_expr.value), "<repl>", "eval"),
                        self.global_ns,
                        self.local_ns,
                    )
                    if expr_result is not None:
                        print(repr(expr_result))

        except SyntaxError as e:
            result["success"] = False
            result["error"] = f"SyntaxError: {e.msg} at line {e.lineno}\n{e.text}"
            if e.offset:
                result["error"] += f"\n{' ' * (e.offset - 1)}^"
        except Exception:
            result["success"] = False
            result["error"] = traceback.format_exc()
        finally:
            result["output"] = stdout_capture.getvalue()
            result["error"] = (result["error"] + stderr_capture.getvalue()).strip()
            result["variables"] = self._get_safe_vars()

        return result

    def _get_safe_vars(self) -> dict:
        safe = {}
        for k, v in self.local_ns.items():
            if not k.startswith("_"):
                try:
                    safe[k] = repr(v)[:200]
                except Exception:
                    safe[k] = "<unrepresentable>"
        return safe

    def reset(self):
        self.global_ns = {"__builtins__": __builtins__}
        self.local_ns = {}
