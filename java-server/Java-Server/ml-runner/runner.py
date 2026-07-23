import contextlib
import importlib.util
import io
import json
import os
import sys
import traceback


MAX_CAPTURED_STDOUT_CHARS = 8192
MAX_ERROR_CHARS = 4096
MAX_STRUCTURED_RESULT_BYTES = 49152
TRUNCATION_MARKER = "\n...[truncated]"


class BoundedTextWriter(io.TextIOBase):
    def __init__(self, limit):
        self.limit = limit
        self.parts = []
        self.length = 0
        self.truncated = False

    def write(self, value):
        text = str(value)
        remaining = max(0, self.limit - len(TRUNCATION_MARKER) - self.length)
        if remaining:
            stored = text[:remaining]
            self.parts.append(stored)
            self.length += len(stored)
        if len(text) > remaining:
            self.truncated = True
        return len(text)

    def getvalue(self):
        value = "".join(self.parts)
        return value + TRUNCATION_MARKER if self.truncated else value

    def flush(self):
        pass


def encode_bounded(payload):
    encoder = json.JSONEncoder(allow_nan=False, ensure_ascii=False, separators=(",", ":"))
    chunks = []
    length = 0
    for chunk in encoder.iterencode(payload):
        length += len(chunk.encode("utf-8"))
        if length > MAX_STRUCTURED_RESULT_BYTES:
            return None
        chunks.append(chunk)
    return "".join(chunks)


def write_all(file_descriptor, value):
    encoded = value.encode("utf-8")
    offset = 0
    while offset < len(encoded):
        offset += os.write(file_descriptor, encoded[offset:])


def main():
    result_file_descriptor = os.dup(sys.stdout.fileno())
    null_output = os.open(os.devnull, os.O_WRONLY)
    os.dup2(null_output, sys.stdout.fileno())
    os.close(null_output)
    captured_stdout = BoundedTextWriter(MAX_CAPTURED_STDOUT_CHARS)
    try:
        specification = importlib.util.spec_from_file_location("student_solution", "/workspace/solution.py")
        module = importlib.util.module_from_spec(specification)
        with contextlib.redirect_stdout(captured_stdout):
            specification.loader.exec_module(module)
            if not hasattr(module, "solve") or not callable(module.solve):
                raise ValueError("Define a callable solve(train_path, test_path) function.")
            result = module.solve("/workspace/train.csv", "/workspace/test.csv")
        payload = {"success": True, "result": result, "stdout": captured_stdout.getvalue(), "error": ""}
    except BaseException as exception:
        payload = {
            "success": False,
            "result": None,
            "stdout": captured_stdout.getvalue(),
            "error": ("".join(traceback.format_exception_only(type(exception), exception))).strip()[:MAX_ERROR_CHARS],
        }

    try:
        encoded = encode_bounded(payload)
    except BaseException as exception:
        encoded = encode_bounded({
            "success": False,
            "result": None,
            "stdout": captured_stdout.getvalue(),
            "error": (
                "Result serialization failed: "
                + "".join(traceback.format_exception_only(type(exception), exception)).strip()
            )[:MAX_ERROR_CHARS],
        })
    if encoded is None:
        encoded = encode_bounded({
            "success": False,
            "result": None,
            "stdout": captured_stdout.getvalue(),
            "error": "Submission result exceeds the structured output limit.",
        })
    try:
        write_all(result_file_descriptor, "MENTORA_RESULT=" + encoded + "\n")
    finally:
        os.close(result_file_descriptor)


if __name__ == "__main__":
    main()
