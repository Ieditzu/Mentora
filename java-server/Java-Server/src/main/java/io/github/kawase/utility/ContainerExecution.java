package io.github.kawase.utility;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.attribute.PosixFilePermission;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

public final class ContainerExecution {
    private ContainerExecution() {
        /* w */
    }

    public static Result run(final Path workspace, final String image, final List<String> runtimeCommand, final int timeoutSeconds, final String memoryLimit, final boolean executableTemp) {
        final String containerCommand = System.getenv().getOrDefault("MENTORA_CONTAINER_COMMAND", "docker");
        if (!isImageReady(containerCommand, image))
            return new Result("", "Secure runner image unavailable: " + image, -1, false, true);
        final String containerName = "mentora-code-" + UUID.randomUUID();

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
                "--memory", memoryLimit,
                "--cpus", "1",
                "--user", "65532:65532",
                "--tmpfs", executableTemp ? "/tmp:rw,exec,nosuid,size=128m" : "/tmp:rw,noexec,nosuid,size=64m",
                "--mount", "type=bind,source=" + workspace.toAbsolutePath() + ",target=/workspace,readonly",
                "--workdir", "/tmp",
                image
        ));
        command.addAll(runtimeCommand);

        try {
            final Process process = new ProcessBuilder(command).start();
            final CompletableFuture<String> output = CompletableFuture.supplyAsync(() -> readStream(process.getInputStream()));
            final CompletableFuture<String> error = CompletableFuture.supplyAsync(() -> readStream(process.getErrorStream()));
            if (!process.waitFor(timeoutSeconds, TimeUnit.SECONDS)) {
                process.destroyForcibly();
                forceRemove(containerCommand, containerName);
                process.waitFor(2, TimeUnit.SECONDS);
                return new Result(output.join(), "Execution timed out or exhausted resources.", -1, true, false);
            }
            return new Result(output.join(), sanitizeContainerError(error.join()), process.exitValue(), false, false);
        } catch (IOException exception) {
            return new Result("", "Unable to start secure runner: " + exception.getMessage(), -1, false, true);
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            return new Result("", "Secure execution was interrupted.", -1, false, true);
        }
    }

    public static void logRunnerHealth() {
        final String containerCommand = System.getenv().getOrDefault("MENTORA_CONTAINER_COMMAND", "docker");
        for (final String image : List.of(
                System.getenv().getOrDefault("MENTORA_PYTHON_RUNNER_IMAGE", "mentora-python-runner:1"),
                System.getenv().getOrDefault("MENTORA_CPP_RUNNER_IMAGE", "mentora-cpp-runner:1"))) {
            if (isImageReady(containerCommand, image))
                System.out.println("Secure code runner ready: " + image);
            else
                System.err.println("Secure code runner unavailable: " + image);
        }
    }

    public static void deleteDirectory(final Path directory) {
        if (directory == null) return;
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

    public static void makeWorkspaceReadable(final Path directory) throws IOException {
        try {
            Files.setPosixFilePermissions(directory, Set.of(
                    PosixFilePermission.OWNER_READ,
                    PosixFilePermission.OWNER_WRITE,
                    PosixFilePermission.OWNER_EXECUTE,
                    PosixFilePermission.GROUP_READ,
                    PosixFilePermission.GROUP_EXECUTE,
                    PosixFilePermission.OTHERS_READ,
                    PosixFilePermission.OTHERS_EXECUTE
            ));
            try (var paths = Files.list(directory)) {
                for (final Path path : paths.toList())
                    Files.setPosixFilePermissions(path, Set.of(
                            PosixFilePermission.OWNER_READ,
                            PosixFilePermission.OWNER_WRITE,
                            PosixFilePermission.GROUP_READ,
                            PosixFilePermission.OTHERS_READ
                    ));
            }
        } catch (UnsupportedOperationException ignored) {
            /* w */
        }
    }

    private static boolean isImageReady(final String containerCommand, final String image) {
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

    private static void forceRemove(final String containerCommand, final String containerName) {
        try {
            final Process cleanup = new ProcessBuilder(containerCommand, "rm", "-f", containerName).start();
            cleanup.waitFor(5, TimeUnit.SECONDS);
        } catch (IOException ignored) {
            /* w */
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    private static String readStream(final InputStream stream) {
        try (stream; ByteArrayOutputStream stored = new ByteArrayOutputStream()) {
            final byte[] buffer = new byte[4096];
            int count;
            while ((count = stream.read(buffer)) >= 0) {
                if (stored.size() < 16_384)
                    stored.write(buffer, 0, Math.min(count, 16_384 - stored.size()));
            }
            final String value = stored.toString(StandardCharsets.UTF_8);
            return value.length() < 16_384 ? value : value + "\n...[truncated]";
        } catch (IOException exception) {
            return "";
        }
    }

    private static String sanitizeContainerError(final String error) {
        return error
                .replace("WARNING: Your kernel does not support swap limit capabilities or the cgroup is not mounted. Memory limited without swap.\n", "")
                .replace("WARNING: Your kernel does not support swap limit capabilities or the cgroup is not mounted. Memory limited without swap.\r\n", "")
                .trim();
    }

    public record Result(String output, String error, int exitCode, boolean timeout, boolean infrastructureError) {
        /* w */
    }
}
