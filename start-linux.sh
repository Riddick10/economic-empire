#!/usr/bin/env bash
# Startet Economic Empire auf Linux
# Nutzt das user-lokale .NET SDK unter ~/.dotnet
set -e
cd "$(dirname "$0")"

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

dotnet run -c Release "$@"
