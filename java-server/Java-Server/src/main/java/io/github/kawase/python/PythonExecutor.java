package io.github.kawase.python;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.Setter;

import java.io.*;
import java.nio.file.*;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

public class PythonExecutor {
    @Getter
    @Setter
    @AllArgsConstructor
    public static class ExecutionResult {
        public String output;
        public String error;
        public int exitCode;
        public boolean isTimeout;
    }

    public static ExecutionResult execute(String pythonCode, int timeoutSeconds) {
        Path tempDir = null;
        try {
            tempDir = Files.createTempDirectory("game_py_exec_");
            Path sourceFile = tempDir.resolve("main.py");

            Files.writeString(sourceFile, pythonCode);

            final String execCommand = String.format(
                    "unshare --net --user --map-root-user bash -c '" +
                            "ulimit -v 262144; " + // Limit virtual memory to ~256MB
                            "ulimit -t %d; " +     // Limit CPU time
                            "ulimit -f 2048; " +   // Limit created file size to ~2MB
                            "ulimit -u 64; " +     // Limit max user processes
                            "python3 -I -B -S %s'",
                    timeoutSeconds, sourceFile.toAbsolutePath().toString()
            );

            ProcessBuilder runBuilder = new ProcessBuilder("bash", "-c", execCommand);
            runBuilder.directory(tempDir.toFile());
            Process runProcess = runBuilder.start();

            boolean finishedInTime = runProcess.waitFor(timeoutSeconds + 2, TimeUnit.SECONDS);
            if (!finishedInTime) {
                runProcess.destroyForcibly();
                return new ExecutionResult("", "Execution timed out or exhausted resources.", -1, true);
            }

            String output = readStream(runProcess.getInputStream());
            String error = readStream(runProcess.getErrorStream());

            return new ExecutionResult(output, error, runProcess.exitValue(), false);

        } catch (IOException | InterruptedException e) {
            return new ExecutionResult("", "System Exception: " + e.getMessage(), -1, false);
        } finally {
            if (tempDir != null) {
                deleteDirectory(tempDir.toFile());
            }
        }
    }

    private static String readStream(InputStream stream) throws IOException {
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(stream))) {
            return reader.lines().collect(Collectors.joining("\n"));
        }
    }

    private static void deleteDirectory(File directoryToBeDeleted) {
        File[] allContents = directoryToBeDeleted.listFiles();
        if (allContents != null) {
            for (File file : allContents) {
                deleteDirectory(file);
            }
        }
        directoryToBeDeleted.delete();
    }
}
