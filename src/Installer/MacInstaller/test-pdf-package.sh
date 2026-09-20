#!/bin/bash
# Test a built or extracted .app with Homebrew and installed runtimes unreadable.
# The probe and generated documents stay in a temporary directory, never the app.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP="${1:?Usage: bash test-pdf-package.sh /path/to/PurplePen.app}"
PAYLOAD="$(cd "$APP/Contents/MacOS" && pwd)"
TEST_DIR="$(mktemp -d /private/tmp/purplepen-pdf-smoke.XXXXXX)"
printf 'PDF smoke test directory: %s\n' "$TEST_DIR"

dotnet build "$SCRIPT_DIR/tests/PdfPackageSmoke.csproj" \
    --configuration Release --runtime osx-arm64 --self-contained false \
    -p:Payload="$PAYLOAD" -p:BaseIntermediateOutputPath="$TEST_DIR/obj/" \
    -p:MSBuildProjectExtensionsPath="$TEST_DIR/obj/" \
    --output "$TEST_DIR/probe" -m:1 /nr:false --nologo --verbosity quiet

mkdir "$TEST_DIR/run"
cp -a "$PAYLOAD/." "$TEST_DIR/run/"
cp "$TEST_DIR/probe/PdfPackageSmoke" "$TEST_DIR/probe/PdfPackageSmoke.dll" "$TEST_DIR/run/"
# Reuse exactly the app's dependency graph and self-contained framework config.
cp "$PAYLOAD/PurplePen.deps.json" "$TEST_DIR/run/PdfPackageSmoke.deps.json"
cp "$PAYLOAD/PurplePen.runtimeconfig.json" "$TEST_DIR/run/PdfPackageSmoke.runtimeconfig.json"

POLICY='(version 1)(allow default)(deny file-read* (subpath "/opt/homebrew") (subpath "/usr/local") (subpath "/Library/Frameworks/Mono.framework"))'
cd "$TEST_DIR/run"
/usr/bin/sandbox-exec -p "$POLICY" /usr/bin/env -i \
    PATH=/usr/bin:/bin TMPDIR="$TEST_DIR" ./PdfPackageSmoke
/usr/bin/sandbox-exec -p "$POLICY" /usr/bin/env -i \
    PATH=/usr/bin:/bin TMPDIR="$TEST_DIR" ./PdfConverter 72 single.pdf single.png
[[ "$(/usr/bin/file -b single.png)" == PNG* ]] || { echo 'PDF rasterization failed'; exit 1; }
echo 'PASS: packaged PdfConverter produced a PNG without Homebrew or installed .NET.'
