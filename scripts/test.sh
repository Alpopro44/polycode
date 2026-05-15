#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "▸ PolyCode Test Suite"
echo ""

echo "─ Python Engine Tests ─"
source "$PROJECT_DIR/.venv/bin/activate"
cd "$PROJECT_DIR/src/PolyCode.Engine"
python3 -m pytest tests/ -v

echo ""
echo "─ C# Build Check ─"
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
cd "$PROJECT_DIR/src/PolyCode.UI/PolyCode.UI"
dotnet build --verbosity quiet

echo ""
echo "✓ All checks passed!"
