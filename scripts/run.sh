#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "╔══════════════════════════════════════╗"
echo "║        PolyCode Development Kit      ║"
echo "╚══════════════════════════════════════╝"

case "${1:-help}" in
  engine)
    echo "▸ Starting PolyCode Python Engine..."
    source "$PROJECT_DIR/.venv/bin/activate"
    cd "$PROJECT_DIR/src/PolyCode.Engine"
    exec python3 main.py "$@"
    ;;
  ui)
    echo "▸ Starting PolyCode C# UI..."
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    cd "$PROJECT_DIR/src/PolyCode.UI/PolyCode.UI"
    exec dotnet run "$@"
    ;;
  test)
    echo "▸ Running Python tests..."
    source "$PROJECT_DIR/.venv/bin/activate"
    cd "$PROJECT_DIR/src/PolyCode.Engine"
    exec python3 -m pytest tests/ -v "$@"
    ;;
  build)
    echo "▸ Building UI..."
    export DOTNET_ROOT="$HOME/.dotnet"
    export PATH="$DOTNET_ROOT:$PATH"
    cd "$PROJECT_DIR/src/PolyCode.UI/PolyCode.UI"
    dotnet build
    echo "▸ Python tests..."
    source "$PROJECT_DIR/.venv/bin/activate"
    cd "$PROJECT_DIR/src/PolyCode.Engine"
    python3 -m pytest tests/ -v
    ;;
  dev)
    echo "▸ Starting Engine + UI..."
    "$SCRIPT_DIR/run.sh" engine &
    ENGINE_PID=$!
    sleep 1
    "$SCRIPT_DIR/run.sh" ui
    kill $ENGINE_PID 2>/dev/null
    ;;
  help|*)
    echo ""
    echo "  run.sh engine    Start Python REPL engine (WebSocket server)"
    echo "  run.sh ui        Start C# Avalonia UI"
    echo "  run.sh test      Run Python tests"
    echo "  run.sh build     Build everything + run tests"
    echo "  run.sh dev       Start engine + UI together"
    echo ""
    ;;
esac
