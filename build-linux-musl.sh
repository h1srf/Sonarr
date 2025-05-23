#!/usr/bin/env bash
set -e

# Optional: Set specific version/branch
export SONARR_VERSION=4.0.0.999
export BRANCH=custom
export FRAMEWORK=net6.0
export RID=linux-musl-x64

# Build and package Sonarr for linux-musl
./build.sh --backend --frontend --packages --runtime $RID --framework $FRAMEWORK

# Mark binaries as executable
chmod +x "_artifacts/linux-musl-x64/$FRAMEWORK/Sonarr/Sonarr"

# If you bundle ffprobe, mark it executable too
chmod +x "_artifacts/linux-musl-x64/$FRAMEWORK/Sonarr/ffprobe" || echo "No ffprobe found, skipping"
