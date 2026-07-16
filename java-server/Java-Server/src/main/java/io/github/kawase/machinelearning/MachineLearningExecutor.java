package io.github.kawase.machinelearning;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.utility.ContainerExecution;
import jakarta.annotation.PostConstruct;
import org.springframework.stereotype.Component;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

@Component
public class MachineLearningExecutor {
    private final ObjectMapper objectMapper;
    private final String containerCommand, image;
    private final int timeoutSeconds;

    public MachineLearningExecutor(
            final ObjectMapper objectMapper,
            @org.springframework.beans.factory.annotation.Value("${mentora.ml.container-command:docker}") final String containerCommand,
            @org.springframework.beans.factory.annotation.Value("${mentora.ml.image:mentora-ml-runner:1}") final String image,
            @org.springframework.beans.factory.annotation.Value("${mentora.ml.timeout-seconds:15}") final int timeoutSeconds) {
        this.objectMapper = objectMapper;
        this.containerCommand = containerCommand;
        this.image = image;
        this.timeoutSeconds = timeoutSeconds;
    }

    @PostConstruct
    public void logHealth() {
        if (isRunnerReady())
            System.out.println("Machine-learning runner ready: " + image);
        else
            System.err.println("Machine-learning runner unavailable. Build `" + image + "` from ml-runner/Dockerfile before accepting submissions.");
    }

    public ExecutionResult execute(final MachineLearningProblem problem, final String sourceCode) {
        if (sourceCode == null || sourceCode.isBlank())
            return new ExecutionResult(false, false, null, "", "Submit a non-empty Python solution.");
        if (sourceCode.getBytes(StandardCharsets.UTF_8).length > 65_536)
            return new ExecutionResult(false, false, null, "", "The Python solution exceeds the 64 KB limit.");
        if (!isRunnerReady())
            return new ExecutionResult(true, false, null, "", "The machine-learning runner image is unavailable.");

        Path workDirectory = null;
        try {
            workDirectory = Files.createTempDirectory("mentora_ml_");
            Files.writeString(workDirectory.resolve("solution.py"), sourceCode, StandardCharsets.UTF_8);
            Files.writeString(workDirectory.resolve("train.csv"), problem.getTrainCsv(), StandardCharsets.UTF_8);
            Files.writeString(workDirectory.resolve("test.csv"), problem.getTestCsv(), StandardCharsets.UTF_8);
            ContainerExecution.makeWorkspaceReadable(workDirectory);
            final String containerName = "mentora-ml-" + UUID.randomUUID();

            final List<String> command = new ArrayList<>(List.of(
                    containerCommand, "run", "--rm",
                    "--name", containerName,
                    "--network", "none",
                    "--read-only",
                    "--cap-drop", "ALL",
                    "--security-opt", "no-new-privileges",
                    "--pids-limit", "64",
                    "--ulimit", "nofile=64:64",
                    "--ulimit", "fsize=2097152:2097152",
                    "--memory", "512m",
                    "--cpus", "1",
                    "--user", "65532:65532",
                    "--tmpfs", "/tmp:rw,noexec,nosuid,size=64m",
                    "--mount", "type=bind,source=" + workDirectory.toAbsolutePath() + ",target=/workspace,readonly",
                    image
            ));
            final Process process = new ProcessBuilder(command).start();
            final CompletableFuture<String> outputFuture = CompletableFuture.supplyAsync(() -> readStream(process.getInputStream()));
            final CompletableFuture<String> errorFuture = CompletableFuture.supplyAsync(() -> readStream(process.getErrorStream()));

            if (!process.waitFor(timeoutSeconds, TimeUnit.SECONDS)) {
                process.destroyForcibly();
                forceRemove(containerName);
                process.waitFor(2, TimeUnit.SECONDS);
                return new ExecutionResult(false, false, null, trimOutput(outputFuture.join()), "Execution timed out after " + timeoutSeconds + " seconds.");
            }

            final String output = outputFuture.join(), processError = errorFuture.join();
            final int markerIndex = output.lastIndexOf("MENTORA_RESULT=");
            if (markerIndex < 0)
                return new ExecutionResult(false, false, null, trimOutput(output), trimOutput(processError.isBlank() ? "The runner did not return a structured result." : processError));

            final JsonNode payload = objectMapper.readTree(output.substring(markerIndex + "MENTORA_RESULT=".length()).trim());
            return new ExecutionResult(
                    false,
                    payload.path("success").asBoolean(false),
                    payload.get("result"),
                    trimOutput(payload.path("stdout").asText("")),
                    trimOutput(payload.path("error").asText(processError))
            );
        } catch (IOException exception) {
            return new ExecutionResult(true, false, null, "", "Unable to start the machine-learning runner: " + exception.getMessage());
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            return new ExecutionResult(true, false, null, "", "Machine-learning execution was interrupted.");
        } finally {
            if (workDirectory != null)
                deleteDirectory(workDirectory);
        }
    }

    boolean isRunnerReady() {
        try {
            final Process process = new ProcessBuilder(containerCommand, "image", "inspect", image).start();
            if (!process.waitFor(5, TimeUnit.SECONDS)) {
                process.destroyForcibly();
                return false;
            }
            return process.exitValue() == 0;
        } catch (IOException exception) {
            return false;
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            return false;
        }
    }

    private String readStream(final InputStream stream) {
        try (stream; ByteArrayOutputStream stored = new ByteArrayOutputStream()) {
            final byte[] buffer = new byte[4096];
            int count;
            while ((count = stream.read(buffer)) >= 0) {
                if (stored.size() < 16_384)
                    stored.write(buffer, 0, Math.min(count, 16_384 - stored.size()));
            }
            return stored.toString(StandardCharsets.UTF_8);
        } catch (IOException exception) {
            return "";
        }
    }

    private void forceRemove(final String containerName) {
        try {
            final Process cleanup = new ProcessBuilder(containerCommand, "rm", "-f", containerName).start();
            cleanup.waitFor(5, TimeUnit.SECONDS);
        } catch (IOException ignored) {
            /* w */
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    private String trimOutput(final String value) {
        if (value == null) return "";
        return value.length() <= 16_384 ? value : value.substring(0, 16_384);
    }

    private void deleteDirectory(final Path directory) {
        try (var paths = Files.walk(directory)) {
            paths.sorted(Comparator.reverseOrder()).forEach(path -> {
                try {
                    Files.deleteIfExists(path);
                } catch (IOException ignored) {
                    /* w */
                }
            });
        } catch (IOException ignored) {
            /* w */
        }
    }

    public record ExecutionResult(boolean infrastructureError, boolean success, JsonNode result, String stdout, String error) {
        /* w */
    }
}
