package io.github.kawase.python;

import io.github.kawase.utility.ContainerExecution;
import lombok.AllArgsConstructor;
import lombok.Getter;

import org.json.JSONArray;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;

public class CodeWorldPythonExecutor {
    private static final int MAX_COMMANDS = 512;
    private static final int MAX_OUTPUT_CHARS = 16_000;
    private static final int MAX_ERROR_CHARS = 16_000;

    @Getter
    @AllArgsConstructor
    public static class ExecutionResult {
        private final String commandsText;
        private final String output;
        private final String error;
        private final int exitCode;
        private final boolean timeout;
    }

    public static ExecutionResult execute(final String pythonCode, final int timeoutSeconds) {
        Path workspace = null;
        try {
            final String source = pythonCode == null ? "" : pythonCode;
            if (source.getBytes(StandardCharsets.UTF_8).length > 65_536)
                return new ExecutionResult("", "", "Code World source exceeds the 64 KB limit.", -1, false);

            workspace = Files.createTempDirectory("mentora_code_world_");
            Files.writeString(workspace.resolve("mentora_world.py"), buildLibrarySource(), StandardCharsets.UTF_8);
            Files.writeString(workspace.resolve("user_code.py"), source, StandardCharsets.UTF_8);
            Files.writeString(workspace.resolve("main.py"), buildWrapperSource(), StandardCharsets.UTF_8);
            ContainerExecution.makeWorkspaceReadable(workspace);

            final ContainerExecution.Result result = ContainerExecution.run(
                    workspace,
                    System.getenv().getOrDefault("MENTORA_PYTHON_RUNNER_IMAGE", "mentora-python-runner:1"),
                    List.of("python", "-B", "-S", "/workspace/main.py"),
                    timeoutSeconds,
                    "256m",
                    false
            );
            final int markerIndex = result.output().lastIndexOf("MENTORA_COMMANDS=");
            final String commandsText = markerIndex < 0 ? "" : readCommands(result.output().substring(markerIndex + "MENTORA_COMMANDS=".length()).trim());
            final String rawOutput = markerIndex < 0 ? result.output() : result.output().substring(0, markerIndex);

            return new ExecutionResult(
                    commandsText,
                    trimTo(rawOutput.trim(), MAX_OUTPUT_CHARS),
                    trimTo(result.error(), MAX_ERROR_CHARS),
                    result.exitCode(),
                    result.timeout()
            );
        } catch (IOException exception) {
            return new ExecutionResult("", "", "System Exception: " + exception.getMessage(), -1, false);
        } finally {
            ContainerExecution.deleteDirectory(workspace);
        }
    }

    private static String readCommands(final String commandsJson) {
        final List<String> commands = new ArrayList<>();
        try {
            final JSONArray array = new JSONArray(commandsJson);
            for (int i = 0; i < array.length() && commands.size() < MAX_COMMANDS; i++) {
                final String command = array.optString(i, "").trim();
                if (!command.isBlank())
                    commands.add(command);
            }
        } catch (Exception ignored) {
            return "";
        }

        return String.join("\n", commands);
    }

    private static String trimTo(final String value, final int maxChars) {
        if (value == null || value.length() <= maxChars) {
            return value == null ? "" : value;
        }

        return value.substring(0, maxChars) + "\n...[truncated]";
    }

    private static String buildWrapperSource() {
        return """
                import json
                import runpy
                import sys
                import traceback
                import mentora_world

                exit_code = 0
                try:
                    runpy.run_path("/workspace/user_code.py", init_globals=mentora_world._exports(), run_name="__main__")
                except BaseException:
                    traceback.print_exc()
                    exit_code = 1

                print("MENTORA_COMMANDS=" + json.dumps(mentora_world._commands, separators=(",", ":")))
                sys.exit(exit_code)
                """;
    }

    private static String buildLibrarySource() {
        return """
                import re

                _commands = []
                _MAX_COMMANDS = 512
                _NAME_RE = re.compile(r"^[A-Za-z0-9_-]{1,40}$")

                class Vec3:
                    def __init__(self, x=0, y=0, z=0):
                        self.x = _number(x, "x")
                        self.y = _number(y, "y")
                        self.z = _number(z, "z")

                    def __iter__(self):
                        yield self.x
                        yield self.y
                        yield self.z

                    def __repr__(self):
                        return f"Vec3({self.x}, {self.y}, {self.z})"

                def _number(value, label="number"):
                    if isinstance(value, bool):
                        raise ValueError(f"{label} must be a number")
                    try:
                        return float(value)
                    except Exception as exc:
                        raise ValueError(f"{label} must be a number") from exc

                def _fmt_number(value):
                    value = _number(value)
                    if abs(value - int(value)) < 0.000001:
                        return str(int(value))
                    return f"{value:.4f}".rstrip("0").rstrip(".")

                def _vec3(value=None, y=None, z=None, *, x=None):
                    if isinstance(value, Vec3):
                        return value
                    if x is not None:
                        return Vec3(x, y, z)
                    if value is None and y is None and z is None:
                        return Vec3(0, 0, 0)
                    if y is not None or z is not None:
                        return Vec3(value, y, z)
                    if isinstance(value, (list, tuple)) and len(value) == 3:
                        return Vec3(value[0], value[1], value[2])
                    raise ValueError("Expected a Vec3, tuple/list of 3 numbers, or x, y, z")

                def _vec_text(value=None, y=None, z=None, *, x=None):
                    vec = _vec3(value, y, z, x=x)
                    return f"{_fmt_number(vec.x)} {_fmt_number(vec.y)} {_fmt_number(vec.z)}"

                def _name(value):
                    text = str(value).strip()
                    if not _NAME_RE.match(text):
                        raise ValueError("Object names must use 1-40 letters, numbers, _ or -")
                    return text

                def _emit(command):
                    if len(_commands) >= _MAX_COMMANDS:
                        raise RuntimeError("Too many world commands; limit is 512")
                    _commands.append(command)

                def vector(x=0, y=0, z=0):
                    return Vec3(x, y, z)

                vec3 = vector
                Vector3 = Vec3

                def _spawn(shape, name, position=None, scale=None, *, pos=None, size=None):
                    if pos is not None:
                        position = pos
                    if size is not None:
                        scale = size
                    parts = [shape, _name(name)]
                    if position is not None:
                        parts.append(_vec_text(position))
                    if scale is not None:
                        if position is None:
                            parts.append(_vec_text((0, 0, 0)))
                        parts.append(_vec_text(scale))
                    _emit(" ".join(parts))

                def cube(name, position=None, scale=None, **kwargs): _spawn("cube", name, position, scale, **kwargs)
                def box(name, position=None, scale=None, **kwargs): _spawn("box", name, position, scale, **kwargs)
                def sphere(name, position=None, scale=None, **kwargs): _spawn("sphere", name, position, scale, **kwargs)
                def ball(name, position=None, scale=None, **kwargs): _spawn("ball", name, position, scale, **kwargs)
                def orb(name, position=None, scale=None, **kwargs): _spawn("orb", name, position, scale, **kwargs)
                def ellipsoid(name, position=None, scale=None, **kwargs): _spawn("ellipsoid", name, position, scale, **kwargs)
                def oval(name, position=None, scale=None, **kwargs): _spawn("oval", name, position, scale, **kwargs)
                def capsule(name, position=None, scale=None, **kwargs): _spawn("capsule", name, position, scale, **kwargs)
                def cylinder(name, position=None, scale=None, **kwargs): _spawn("cylinder", name, position, scale, **kwargs)
                def rectangle(name, position=None, scale=None, **kwargs): _spawn("rectangle", name, position, scale, **kwargs)
                def rect(name, position=None, scale=None, **kwargs): _spawn("rect", name, position, scale, **kwargs)
                def panel(name, position=None, scale=None, **kwargs): _spawn("panel", name, position, scale, **kwargs)
                def plane(name, position=None, scale=None, **kwargs): _spawn("plane", name, position, scale, **kwargs)
                def circle(name, position=None, scale=None, **kwargs): _spawn("circle", name, position, scale, **kwargs)
                def disc(name, position=None, scale=None, **kwargs): _spawn("disc", name, position, scale, **kwargs)

                def _transform(command, name, value=None, y=None, z=None, *, x=None):
                    _emit(f"{command} {_name(name)} {_vec_text(value, y, z, x=x)}")

                def move(name, position=None, y=None, z=None, *, x=None): _transform("move", name, position, y, z, x=x)
                def rotate(name, rotation=None, y=None, z=None, *, x=None): _transform("rotate", name, rotation, y, z, x=x)
                def resize(name, size=None, y=None, z=None, *, x=None): _transform("scale", name, size, y, z, x=x)
                def scale(name, size=None, y=None, z=None, *, x=None): resize(name, size, y, z, x=x)
                def translate(name, delta=None, y=None, z=None, *, x=None): _transform("translate", name, delta, y, z, x=x)
                def turn(name, delta=None, y=None, z=None, *, x=None): _transform("turn", name, delta, y, z, x=x)

                def color(name, value, g=None, b=None):
                    if g is not None or b is not None:
                        _emit(f"color {_name(name)} {_fmt_number(value)} {_fmt_number(g)} {_fmt_number(b)}")
                    elif isinstance(value, (list, tuple, Vec3)):
                        _emit(f"color {_name(name)} {_vec_text(value)}")
                    else:
                        _emit(f"color {_name(name)} {str(value).strip()}")

                def delete(name): _emit(f"delete {_name(name)}")
                def destroy(name): delete(name)
                def clear(): _emit("clear")
                def help(): _emit("help")
                def list_objects(): _emit("list")

                Cube = cube
                Box = box
                Sphere = sphere
                Ball = ball
                Orb = orb
                Ellipsoid = ellipsoid
                Oval = oval
                Capsule = capsule
                Cylinder = cylinder
                Rectangle = rectangle
                Rect = rect
                Panel = panel
                Plane = plane
                Circle = circle
                Disc = disc
                Move = move
                Rotate = rotate
                Resize = resize
                Scale = scale
                Translate = translate
                Turn = turn
                Color = color
                Delete = delete
                Destroy = destroy
                Clear = clear

                def _exports():
                    return {name: value for name, value in globals().items() if not name.startswith("_")}
                """;
    }
}
