package io.github.kawase.python;

import io.github.kawase.utility.ContainerExecution;
import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.Setter;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

public class PythonExecutor {
    @Getter
    @Setter
    @AllArgsConstructor
    public static class ExecutionResult {
        public String output, error;
        public int exitCode;
        public boolean isTimeout;
    }

    public static ExecutionResult execute(final String pythonCode, final int timeoutSeconds) {
        Path workspace = null;
        try {
            final String source = pythonCode == null ? "" : pythonCode;
            if (source.getBytes(StandardCharsets.UTF_8).length > 65_536)
                return new ExecutionResult("", "Python source exceeds the 64 KB limit.", -1, false);

            workspace = Files.createTempDirectory("mentora_python_");
            Files.writeString(workspace.resolve("main.py"), source, StandardCharsets.UTF_8);
            ContainerExecution.makeWorkspaceReadable(workspace);
            final ContainerExecution.Result result = ContainerExecution.run(
                    workspace,
                    System.getenv().getOrDefault("MENTORA_PYTHON_RUNNER_IMAGE", "mentora-python-runner:1"),
                    List.of("python", "-I", "-B", "-S", "/workspace/main.py"),
                    Math.clamp(timeoutSeconds, 1, 30),
                    "256m",
                    false
            );
            return new ExecutionResult(result.output(), result.error(), result.exitCode(), result.timeout());
        } catch (IOException exception) {
            return new ExecutionResult("", "System Exception: " + exception.getMessage(), -1, false);
        } finally {
            ContainerExecution.deleteDirectory(workspace);
        }
    }
}
