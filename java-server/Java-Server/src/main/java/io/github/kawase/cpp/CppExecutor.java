package io.github.kawase.cpp;

import lombok.AllArgsConstructor;
import lombok.Getter;
import lombok.Setter;

import java.io.*;
import java.nio.file.*;
import java.util.concurrent.TimeUnit;
import java.util.stream.Collectors;

public class CppExecutor {
    @Getter
    @Setter
    @AllArgsConstructor
    public static class ExecutionResult {
        public String output;
        public String error;
        public int exitCode;
        public boolean isTimeout;
    }

    public static ExecutionResult execute(String cppCode, int timeoutSeconds) {
        Path tempDir = null;
        try {
            tempDir = Files.createTempDirectory("game_cpp_exec_");
            Path sourceFile = tempDir.resolve("main.cpp");
            Path exeFile = tempDir.resolve("program.out");

            Files.writeString(sourceFile, cppCode);

            // 1. Compile the Code (with a strict timeout for template infinite loops)
            ProcessBuilder compileBuilder = new ProcessBuilder(
                    "g++", "-O2", sourceFile.toString(), "-o", exeFile.toString()
            );
            Process compileProcess = compileBuilder.start();

            if (!compileProcess.waitFor(10, TimeUnit.SECONDS)) {
                compileProcess.destroyForcibly();
                return new ExecutionResult("", "Compilation timed out (Potential compiler bomb).", -1, true);
            }

            if (compileProcess.exitValue() != 0) {
                String compileError = readStream(compileProcess.getErrorStream());
                return new ExecutionResult("", "Compilation Error:\n" + compileError, compileProcess.exitValue(), false);
            }

            // 2. Build the OS-Level Security Wrapper
            // unshare: Isolates the process. --net drops network. 
            // --user --map-root-user allows non-root users to create this isolated environment.
            // ulimit: Restricts resources heavily before running the binary.
            final String execCommand = String.format(
                    "unshare --net --user --map-root-user bash -c '" +
                            "ulimit -v 262144; " + // Limit virtual memory to ~256MB
                            "ulimit -t %d; " +     // Limit CPU time to timeoutSeconds
                            "ulimit -f 2048; " +   // Limit created file size to ~2MB
                            "ulimit -u 64; " +     // Limit max user processes (prevents fork bombs)
                            "%s'",
                    timeoutSeconds, exeFile.toAbsolutePath().toString()
            );

            // 3. Execute the Wrapped Binary
            ProcessBuilder runBuilder = new ProcessBuilder("bash", "-c", execCommand);
            // Crucial: Run in the temp directory so any file writes happen there
            runBuilder.directory(tempDir.toFile());
            Process runProcess = runBuilder.start();

            // We still keep the Java-level timeout as a fallback
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