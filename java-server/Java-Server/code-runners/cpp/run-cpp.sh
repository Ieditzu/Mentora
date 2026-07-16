#!/bin/sh
set -eu

g++ -std=c++20 -O2 -pipe /workspace/main.cpp -o /tmp/program
exec /tmp/program
