#!/usr/bin/env bash
# Regenerates docs/screenshots/*.png (used by README.md) by rendering the designer headlessly.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
export AVAUIDESIGNER_SCREENSHOTS="$root/docs/screenshots"
dotnet test "$root/tests/AVAUIDesigner.Shell.Tests" --filter "FullyQualifiedName~ReadmeScreenshots" "$@"
echo "Screenshots written to $AVAUIDESIGNER_SCREENSHOTS"
