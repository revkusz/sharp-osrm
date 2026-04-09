#!/usr/bin/env bash
#
# test-integration-local.sh — Simulate the CI integration test locally.
#
# Mirrors what the CI does:
#   1. Stage native library + Conan deps into a layout matching NuGet runtimes/
#   2. Patch RPATHs so deps resolve from the same directory
#   3. Pack a NuGet package
#   4. Run the integration test against it
#
# Usage: ./scripts/test-integration-local.sh [--skip-pack]
#
# Prerequisites:
#   - Conan build exists at build-conan/libsharposrm.dylib (or libsharposrm.so on Linux)
#   - dotnet 8 SDK installed
#   - Conan cache populated (run cmake --preset <preset> first)
#
set -euo pipefail

OS="$(uname -s)"
ARCH="$(uname -m)"
CONAN2="$HOME/.conan2"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
STAGING="$REPO_ROOT/staging-local"
NUGET_LOCAL="$REPO_ROOT/nupkg-local"

# Determine the native library path
if [ "$OS" == "Darwin" ]; then
  NATIVE_LIB="$REPO_ROOT/build-conan/libsharposrm.dylib"
  RID="osx-arm64"
  MAIN_LIB_NAME="sharposrm.dylib"
  DEP_EXT=".dylib"
elif [ "$OS" == "Linux" ]; then
  NATIVE_LIB="$REPO_ROOT/build-conan/libsharposrm.so"
  RID="linux-x64"
  MAIN_LIB_NAME="sharposrm.so"
  DEP_EXT=".so*"
else
  echo "Unsupported OS: $OS"
  exit 1
fi

if [ ! -f "$NATIVE_LIB" ]; then
  echo "ERROR: Native library not found at $NATIVE_LIB"
  echo "Build it first: cmake --preset conan-release && cmake --build --preset conan-release"
  exit 1
fi

echo "=== Local Integration Test ==="
echo "OS: $OS  ARCH: $ARCH  RID: $RID"
echo "Native lib: $NATIVE_LIB"
echo ""

# ── Step 1: Stage files ─────────────────────────────────────────────────
echo "── Step 1: Staging native library + deps ──"
rm -rf "$STAGING" "$NUGET_LOCAL"
STAGE_DIR="$STAGING/$RID"
mkdir -p "$STAGE_DIR"

# Copy and rename main library (remove lib prefix for .NET resolver)
cp "$NATIVE_LIB" "$STAGE_DIR/$MAIN_LIB_NAME"
echo "  Staged: $MAIN_LIB_NAME"

if [ "$OS" == "Darwin" ]; then
  # Find Conan deps by matching the RPATH entries to actual files
  # Read all @rpath deps from otool output
  DEP_NAMES=$(otool -L "$NATIVE_LIB" | awk '/^\t@rpath\//{print $1}' | sed 's|@rpath/||')
  
  for dep_name in $DEP_NAMES; do
    # Skip the library itself
    [[ "$dep_name" == libsharposrm* ]] && continue
    
    # Find the actual file in the Conan cache using the RPATHs
    found=""
    while IFS= read -r rpath; do
      candidate="$rpath/$dep_name"
      if [ -f "$candidate" ]; then
        found="$candidate"
        break
      fi
    done < <(otool -l "$NATIVE_LIB" | grep -A2 LC_RPATH | grep "path " | awk '{print $2}')
    
    if [ -n "$found" ] && [ -f "$found" ]; then
      cp "$found" "$STAGE_DIR/"
      echo "  Bundled: $(basename $found)"
    else
      echo "  WARNING: Could not find $dep_name in Conan cache"
    fi
  done

  # Patch RPATHs: add @loader_path, remove all absolute paths
  for dylib in "$STAGE_DIR"/*.dylib; do
    [ -f "$dylib" ] || continue
    install_name_tool -add_rpath @loader_path "$dylib" 2>/dev/null || true
    # Collect old rpaths into an array, then delete them
    old_rpaths=()
    while IFS= read -r rpath; do
      [[ "$rpath" != "@loader_path" ]] && old_rpaths+=("$rpath")
    done < <(otool -l "$dylib" | grep -A2 LC_RPATH | grep "path " | awk '{print $2}')
    for old in ${old_rpaths[@]+"${old_rpaths[@]}"}; do
      install_name_tool -delete_rpath "$old" "$dylib" 2>/dev/null || true
    done
  done
  echo "  RPATHs cleaned to @loader_path only"

elif [ "$OS" == "Linux" ]; then
  # Use ldd to find all Conan deps
  for dep in $(ldd "$NATIVE_LIB" | awk '/\.conan2/{print $3}'); do
    cp "$dep" "$STAGE_DIR/"
    echo "  Bundled: $(basename $dep)"
  done

  # Also grab libtbbmalloc.so.2 from the same TBB dir
  TBB_LIB=$(ldd "$NATIVE_LIB" | awk '/libtbb\.so/{print $3}' | head -1)
  if [ -n "$TBB_LIB" ] && [ -f "$TBB_LIB" ]; then
    TBB_DIR=$(dirname "$TBB_LIB")
    for lib in "$TBB_DIR/libtbbmalloc.so.2" "$TBB_DIR/libtbbmalloc_proxy.so.2"; do
      if [ -f "$lib" ] && [ ! -f "$STAGE_DIR/$(basename $lib)" ]; then
        cp "$lib" "$STAGE_DIR/"
        echo "  Bundled: $(basename $lib)"
      fi
    done
  fi

  # Patch RPATHs
  for so in "$STAGE_DIR"/*.so*; do
    [ -f "$so" ] || continue
    patchelf --set-rpath '$ORIGIN' "$so" 2>/dev/null || true
  done
  echo "  RPATHs set to \$ORIGIN"
fi

echo "  Staging contents:"
ls -lh "$STAGE_DIR/"
echo ""

# ── Step 2: Verify the library loads without Conan ──────────────────────
echo "── Step 2: Verifying library loads without Conan ──"
if [ "$OS" == "Darwin" ]; then
  # Test with a small dylib load
  cat > /tmp/test_load.c << 'CEOF'
#include <dlfcn.h>
#include <stdio.h>
int main(int argc, char** argv) {
    void* h = dlopen(argv[1], RTLD_NOW);
    if (!h) { printf("FAIL: %s\n", dlerror()); return 1; }
    printf("OK: loaded\n");
    dlclose(h);
    return 0;
}
CEOF
  clang -o /tmp/test_load /tmp/test_load.c -ldl 2>/dev/null
  if [ -f /tmp/test_load ]; then
    result=$(/tmp/test_load "$STAGE_DIR/$MAIN_LIB_NAME" 2>&1) || true
    if echo "$result" | grep -q "OK"; then
      echo "  ✅ Library loads successfully"
    else
      echo "  ❌ Library failed to load:"
      echo "     $result"
      echo ""
      echo "  This is the same error CI would hit. Fix it here first."
      exit 1
    fi
  fi
elif [ "$OS" == "Linux" ]; then
  if command -v patchelf >/dev/null 2>&1; then
    # Quick check with ldd
    ldd_output=$(ldd "$STAGE_DIR/$MAIN_LIB_NAME" 2>&1)
    if echo "$ldd_output" | grep -q "not found"; then
      echo "  ❌ Missing deps:"
      echo "$ldd_output" | grep "not found"
      exit 1
    else
      echo "  ✅ All deps resolved"
    fi
  fi
fi
echo ""

# ── Step 3: Pack NuGet ─────────────────────────────────────────────────
if [[ "${1:-}" == "--skip-pack" ]]; then
  echo "── Step 3: Skipping NuGet pack (--skip-pack) ──"
else
  echo "── Step 3: Packing NuGet package ──"
  CSPROJ_VERSION=$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' "$REPO_ROOT/src/Sharposrm/Sharposrm.csproj" | head -1)
  VERSION="${CSPROJ_VERSION}-local.$(date +%s)"
  
  dotnet pack "$REPO_ROOT/src/Sharposrm/Sharposrm.csproj" \
    -c Release \
    -p:Version="$VERSION" \
    -p:NativeLibsDir="$STAGING" \
    -o "$NUGET_LOCAL"
  
  NUPKG=$(ls "$NUGET_LOCAL"/*.nupkg | head -1)
  echo "  Packed: $(basename $NUPKG)"
  echo ""
  
  # Verify package contents
  echo "  Native files in package:"
  unzip -l "$NUPKG" | grep "runtimes/" | awk '{print "    "$4}'
  echo ""
fi

# ── Step 4: Run integration test ────────────────────────────────────────
echo "── Step 4: Running integration test ──"
# Remove any stale local sources, add our local source
dotnet nuget remove source local-nupkg 2>/dev/null || true
dotnet nuget remove source local-verify 2>/dev/null || true
dotnet nuget add source "$NUGET_LOCAL" \
  --name local-nupkg \
  --store-password-in-clear-text

# Run the test
dotnet test "$REPO_ROOT/test/Sharposrm.IntegrationTests/" \
  -c Release \
  --logger "console;verbosity=detailed"

# Cleanup
dotnet nuget remove source local-nupkg 2>/dev/null || true
echo ""
echo "=== Integration test passed ✅ ==="
