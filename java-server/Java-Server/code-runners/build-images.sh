#!/bin/sh
set -eu

SCRIPT_DIRECTORY=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT_DIRECTORY=$(dirname "$SCRIPT_DIRECTORY")

docker build -t mentora-python-runner:1 "$SCRIPT_DIRECTORY/python"
docker build -t mentora-cpp-runner:1 "$SCRIPT_DIRECTORY/cpp"
docker build -t mentora-ml-runner:1 "$PROJECT_DIRECTORY/ml-runner"

docker image inspect mentora-python-runner:1 mentora-cpp-runner:1 mentora-ml-runner:1 >/dev/null
echo "Mentora secure runner images are ready."
