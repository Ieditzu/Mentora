# Secure code runners

Every learner-submitted Python, C++, CodeWorld, and AI/ML program runs inside a disposable Docker container. The Java process never invokes a host compiler or interpreter.

Build all pinned images before starting the backend:

```sh
sh code-runners/build-images.sh
```

The containers run without network access, as UID/GID `65532`, with a read-only root filesystem, all Linux capabilities dropped, `no-new-privileges`, PID/file/memory/CPU limits, capped output, and a Java-side timeout. Submitted source is mounted read-only and hidden ML labels never enter the learner container.

Optional environment overrides:

- `MENTORA_CONTAINER_COMMAND`
- `MENTORA_PYTHON_RUNNER_IMAGE`
- `MENTORA_CPP_RUNNER_IMAGE`
- `MENTORA_ML_IMAGE`
- `MENTORA_ML_TIMEOUT_SECONDS`
