package io.github.kawase.cpp;

import io.github.kawase.utility.ContainerExecution;
import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.Setter;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

public class CppExecutor {
    @Getter
    @Setter
    @AllArgsConstructor
    public static class ExecutionResult {
        public String output, error;
        public int exitCode;
        public boolean isTimeout;
    }

    public static ExecutionResult execute(final String cppCode, final int timeoutSeconds) {
        Path workspace = null;
        try {
            final String source = cppCode == null ? "" : cppCode;
            if (source.getBytes(StandardCharsets.UTF_8).length > 65_536)
                return new ExecutionResult("", "C++ source exceeds the 64 KB limit.", -1, false);

            workspace = Files.createTempDirectory("mentora_cpp_");
            Files.writeString(workspace.resolve("main.cpp"), source, StandardCharsets.UTF_8);
            ContainerExecution.makeWorkspaceReadable(workspace);
            final ContainerExecution.Result result = ContainerExecution.run(
                    workspace,
                    System.getenv().getOrDefault("MENTORA_CPP_RUNNER_IMAGE", "mentora-cpp-runner:1"),
                    List.of(),
                    Math.clamp(timeoutSeconds, 1, 30),
                    "512m",
                    true
            );
            return new ExecutionResult(result.output(), result.error(), result.exitCode(), result.timeout());
        } catch (IOException exception) {
            return new ExecutionResult("", "System Exception: " + exception.getMessage(), -1, false);
        } finally {
            ContainerExecution.deleteDirectory(workspace);
        }
    }
}
