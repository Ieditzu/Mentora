import contextlib
import importlib.util
import io
import json
import traceback


def main():
    captured_stdout = io.StringIO()
    try:
        specification = importlib.util.spec_from_file_location("student_solution", "/workspace/solution.py")
        module = importlib.util.module_from_spec(specification)
        with contextlib.redirect_stdout(captured_stdout):
            specification.loader.exec_module(module)
            if not hasattr(module, "solve") or not callable(module.solve):
                raise ValueError("Define a callable solve(train_path, test_path) function.")
            result = module.solve("/workspace/train.csv", "/workspace/test.csv")
        payload = {"success": True, "result": result, "stdout": captured_stdout.getvalue()[:16384], "error": ""}
    except BaseException as exception:
        payload = {
            "success": False,
            "result": None,
            "stdout": captured_stdout.getvalue()[:16384],
            "error": ("".join(traceback.format_exception_only(type(exception), exception))).strip()[:16384],
        }
    print("MENTORA_RESULT=" + json.dumps(payload, allow_nan=False, separators=(",", ":")))


if __name__ == "__main__":
    main()
