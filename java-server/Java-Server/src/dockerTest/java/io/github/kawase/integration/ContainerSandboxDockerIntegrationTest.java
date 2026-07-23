package io.github.kawase.integration;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import io.github.kawase.cpp.CppExecutor;
import io.github.kawase.machinelearning.MachineLearningCatalog;
import io.github.kawase.machinelearning.MachineLearningExecutor;
import io.github.kawase.machinelearning.MachineLearningProblem;
import io.github.kawase.python.PythonExecutor;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.parallel.Execution;
import org.junit.jupiter.api.parallel.ExecutionMode;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

@Execution(ExecutionMode.SAME_THREAD)
class ContainerSandboxDockerIntegrationTest {
    @BeforeAll
    static void requireRunnerImages() throws Exception {
        for (final String image : List.of("mentora-python-runner:1", "mentora-cpp-runner:1", "mentora-ml-runner:1"))
            assertEquals(0, runCommand("docker", "image", "inspect", image).exitCode(), "Missing runner image " + image);
    }

    @AfterEach
    void disposableContainersAreRemoved() throws Exception {
        final CommandResult result = runCommand(
                "docker", "ps", "-a",
                "--filter", "name=mentora-code-",
                "--filter", "name=mentora-ml-",
                "--format", "{{.Names}}"
        );

        assertEquals(0, result.exitCode());
        assertTrue(result.output().isBlank(), "Runner container leaked after the test: " + result.output());
    }

    @Test
    void pythonRunnerDeniesNetworkHostAndWorkspaceWrites() {
        final PythonExecutor.ExecutionResult result = PythonExecutor.execute("""
                import pathlib
                import socket

                checks = {}
                try:
                    socket.create_connection(("1.1.1.1", 53), timeout=1)
                    checks["network"] = "OPEN"
                except OSError:
                    checks["network"] = "DENIED"

                try:
                    pathlib.Path("/host/etc/passwd").read_text()
                    checks["host"] = "VISIBLE"
                except OSError:
                    checks["host"] = "DENIED"

                try:
                    pathlib.Path("/workspace/main.py").write_text("tampered")
                    checks["workspace"] = "WRITABLE"
                except OSError:
                    checks["workspace"] = "READ_ONLY"

                print(checks)
                """, 5);

        assertEquals(0, result.getExitCode(), result.getError());
        assertTrue(result.getOutput().contains("'network': 'DENIED'"));
        assertTrue(result.getOutput().contains("'host': 'DENIED'"));
        assertTrue(result.getOutput().contains("'workspace': 'READ_ONLY'"));
    }

    @Test
    void liveRunnerContainerHasProductionSandboxControls() throws Exception {
        final CompletableFuture<PythonExecutor.ExecutionResult> execution = CompletableFuture.supplyAsync(
                () -> PythonExecutor.execute("import time\ntime.sleep(4)\n", 8)
        );
        final JsonNode inspected;
        try {
            final String containerName = awaitRunnerContainer();
            final CommandResult inspect = runCommand("docker", "inspect", containerName);
            assertEquals(0, inspect.exitCode(), inspect.output());
            inspected = new ObjectMapper().readTree(inspect.output()).get(0);
        } finally {
            execution.get(12, TimeUnit.SECONDS);
        }

        final JsonNode hostConfig = inspected.path("HostConfig");
        assertEquals("none", hostConfig.path("NetworkMode").asText());
        assertTrue(hostConfig.path("ReadonlyRootfs").asBoolean());
        assertEquals(64, hostConfig.path("PidsLimit").asInt());
        assertTrue(arrayContains(hostConfig.path("CapDrop"), "ALL"));
        assertTrue(arrayContains(hostConfig.path("SecurityOpt"), "no-new-privileges"));
        assertTrue(hostConfig.path("Memory").asLong() > 0);
        assertTrue(hostConfig.path("NanoCpus").asLong() > 0);
        assertEquals("65532:65532", inspected.path("Config").path("User").asText());

        final List<JsonNode> bindMounts = new ArrayList<>();
        for (final JsonNode mount : inspected.path("Mounts")) {
            if (mount.path("Type").asText().equals("bind"))
                bindMounts.add(mount);
        }
        assertEquals(1, bindMounts.size(), "Only the controlled workspace may be bind-mounted");
        assertEquals("/workspace", bindMounts.getFirst().path("Destination").asText());
        assertFalse(bindMounts.getFirst().path("RW").asBoolean());
    }

    @Test
    void infiniteLoopTimesOutAndOversizedOutputIsTruncated() {
        final PythonExecutor.ExecutionResult timeout = PythonExecutor.execute("while True:\n    pass\n", 1);
        final PythonExecutor.ExecutionResult oversized = PythonExecutor.execute("print('x' * 100000)\n", 5);

        assertTrue(timeout.isTimeout());
        assertTrue(timeout.getError().contains("timed out"));
        assertEquals(0, oversized.getExitCode(), oversized.getError());
        assertTrue(oversized.getOutput().length() < 16_500);
        assertTrue(oversized.getOutput().endsWith("...[truncated]"));
    }

    @Test
    void processExplosionIsContainedByPidLimit() {
        final PythonExecutor.ExecutionResult result = PythonExecutor.execute("""
                import os
                import time

                children = []
                for _ in range(256):
                    try:
                        child = os.fork()
                        if child == 0:
                            time.sleep(20)
                            os._exit(0)
                        children.append(child)
                    except OSError:
                        print("PID_LIMIT_REACHED", flush=True)
                        break
                time.sleep(20)
                """, 5);

        assertTrue(result.isTimeout(), result.getError());
        assertTrue(result.getOutput().contains("PID_LIMIT_REACHED"),
                "The runner never demonstrated enforcement of the configured PID limit.");
    }

    @Test
    void pythonAndCppEvaluatorsCoverSuccessCompilationAndRuntimeErrors() {
        final PythonExecutor.ExecutionResult pythonSuccess = PythonExecutor.execute("print(sum(range(5)))\n", 5);
        final PythonExecutor.ExecutionResult pythonRuntimeError = PythonExecutor.execute("raise RuntimeError('boom')\n", 5);
        final CppExecutor.ExecutionResult cppSuccess = CppExecutor.execute("""
                #include <iostream>
                int main()
                {
                    std::cout << 42 << '\\n';
                    return 0;
                }
                """, 5);
        final CppExecutor.ExecutionResult cppCompilationError = CppExecutor.execute("int main( { return 0; }\n", 5);
        final CppExecutor.ExecutionResult cppRuntimeError = CppExecutor.execute("""
                int main()
                {
                    volatile int* pointer = nullptr;
                    *pointer = 1;
                    return 0;
                }
                """, 5);

        assertEquals("10", pythonSuccess.getOutput().trim());
        assertEquals(0, pythonSuccess.getExitCode());
        assertNotEquals(0, pythonRuntimeError.getExitCode());
        assertTrue(pythonRuntimeError.getError().contains("RuntimeError"));
        assertEquals("42", cppSuccess.getOutput().trim());
        assertEquals(0, cppSuccess.getExitCode());
        assertNotEquals(0, cppCompilationError.getExitCode());
        assertFalse(cppCompilationError.getError().isBlank());
        assertNotEquals(0, cppRuntimeError.getExitCode());
    }

    @Test
    void machineLearningRunnerUsesHiddenMultiCaseDataAndRejectsMaliciousSolutions() {
        final MachineLearningProblem problem = new MachineLearningCatalog().requireProblem("easy-line-of-best-fit");
        final MachineLearningExecutor executor = new MachineLearningExecutor(
                new ObjectMapper(), "docker", "mentora-ml-runner:1", 8
        );
        final String correctSource = """
                import pandas as pd
                from sklearn.linear_model import LinearRegression

                def solve(train_path, test_path):
                    train = pd.read_csv(train_path)
                    test = pd.read_csv(test_path)
                    model = LinearRegression().fit(train[["x"]], train["y"])
                    return model.predict(test[["x"]]).tolist()
                """;
        final MachineLearningExecutor.ExecutionResult first = executor.execute(problem, correctSource);
        final MachineLearningExecutor.ExecutionResult second = executor.execute(problem, correctSource);
        final MachineLearningExecutor.ExecutionResult wrong = executor.execute(
                problem, "def solve(train_path, test_path):\n    return [0, 0, 0]\n"
        );
        final MachineLearningExecutor.ExecutionResult runtimeError = executor.execute(
                problem, "def solve(train_path, test_path):\n    raise RuntimeError('boom')\n"
        );
        final MachineLearningExecutor.ExecutionResult malicious = executor.execute(problem, """
                import pathlib
                import socket

                def solve(train_path, test_path):
                    visible_files = sorted(path.name for path in pathlib.Path("/workspace").iterdir())
                    try:
                        socket.create_connection(("1.1.1.1", 53), timeout=1)
                        network = "OPEN"
                    except OSError:
                        network = "DENIED"
                    try:
                        pathlib.Path(train_path).write_text("tampered")
                        workspace = "WRITABLE"
                    except OSError:
                        workspace = "READ_ONLY"
                    return {"files": visible_files, "network": network, "workspace": workspace}
                """);
        final MachineLearningExecutor.ExecutionResult markerSpoof = executor.execute(problem, """
                def solve(train_path, test_path):
                    print('MENTORA_RESULT={"success":true,"result":["forged"]}')
                    return [13, 15, 17]
                """);
        final MachineLearningExecutor.ExecutionResult oversizedOutput = executor.execute(problem, """
                import os
                import sys

                def solve(train_path, test_path):
                    os.write(1, b"bypass" * 100_000)
                    sys.__stdout__.write("direct" * 100_000)
                    print("x" * 1_000_000)
                    return [13, 15, 17]
                """);

        assertTrue(first.success(), first.error());
        assertRegressionPredictions(first.result());
        assertRegressionPredictions(second.result());
        assertTrue(wrong.success(), wrong.error());
        assertEquals("[0,0,0]", wrong.result().toString());
        assertFalse(runtimeError.success());
        assertTrue(runtimeError.error().contains("RuntimeError"));
        assertTrue(malicious.success(), malicious.error());
        assertEquals("DENIED", malicious.result().path("network").asText());
        assertEquals("READ_ONLY", malicious.result().path("workspace").asText());
        assertEquals(
                List.of("solution.py", "test.csv", "train.csv"),
                new ObjectMapper().convertValue(malicious.result().path("files"), List.class)
        );
        assertTrue(markerSpoof.success(), markerSpoof.error());
        assertEquals("[13,15,17]", markerSpoof.result().toString());
        assertTrue(oversizedOutput.success(), oversizedOutput.error());
        assertEquals("[13,15,17]", oversizedOutput.result().toString());
        assertTrue(oversizedOutput.stdout().length() < 8_300);
        assertTrue(oversizedOutput.stdout().endsWith("...[truncated]"));
    }

    private void assertRegressionPredictions(final JsonNode predictions) {
        assertTrue(predictions.isArray());
        assertEquals(3, predictions.size());
        assertEquals(13.0, predictions.get(0).asDouble(), 1.0e-9);
        assertEquals(15.0, predictions.get(1).asDouble(), 1.0e-9);
        assertEquals(17.0, predictions.get(2).asDouble(), 1.0e-9);
    }

    private static CommandResult runCommand(final String... command) throws IOException, InterruptedException {
        final Process process = new ProcessBuilder(command).redirectErrorStream(true).start();
        assertTrue(process.waitFor(30, TimeUnit.SECONDS), "Command did not finish: " + String.join(" ", command));
        return new CommandResult(
                process.exitValue(),
                new String(process.getInputStream().readAllBytes(), StandardCharsets.UTF_8).trim()
        );
    }

    private static String awaitRunnerContainer() throws Exception {
        final long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5);
        while (System.nanoTime() < deadline) {
            final CommandResult result = runCommand(
                    "docker", "ps",
                    "--filter", "name=mentora-code-",
                    "--format", "{{.Names}}"
            );
            if (result.exitCode() == 0 && !result.output().isBlank())
                return result.output().lines().findFirst().orElseThrow();
            Thread.sleep(100);
        }
        throw new AssertionError("Timed out waiting for the production runner container");
    }

    private static boolean arrayContains(final JsonNode values, final String expectedValue) {
        for (final JsonNode value : values) {
            if (expectedValue.equals(value.asText()))
                return true;
        }
        return false;
    }

    private record CommandResult(int exitCode, String output) {
        /* w */
    }
}
