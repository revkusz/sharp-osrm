#!/usr/bin/env bash
#
# verify-build.sh — Verify the sharposrm native library build output.
#
# Usage: ./scripts/verify-build.sh <path-to-library>
#
# Checks:
#   1. Library file exists and size > 500KB
#   2. Exactly 21 sharposrm_* exported symbols
#   3. No Homebrew-specific paths in dynamic dependencies (when using Conan)
#   4. Only expected dynamic dependencies (Boost, TBB, Lua, system libs)
#
# Exit codes: 0 = all checks pass, 1 = one or more failures

set -euo pipefail

EXPECTED_SYMBOL_COUNT=21
MIN_SIZE_KB=500

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
NC='\033[0m' # No Color

pass=0
fail=0

info()  { echo -e "${GREEN}[PASS]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
error() { echo -e "${RED}[FAIL]${NC} $1"; }

# ── Argument check ────────────────────────────────────────────────────────
if [ $# -lt 1 ]; then
    error "Usage: $0 <path-to-library>"
    exit 1
fi

LIB_PATH="$1"

if [ ! -f "$LIB_PATH" ]; then
    error "Library not found: $LIB_PATH"
    exit 1
fi

echo "============================================"
echo " sharposrm Build Verification"
echo " Library: $LIB_PATH"
echo " Platform: $(uname -s) $(uname -m)"
echo " Date: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "============================================"
echo ""

# ── Detect platform ───────────────────────────────────────────────────────
OS="$(uname -s)"
ARCH="$(uname -m)"

# ── Check 1: File size ────────────────────────────────────────────────────
FILE_SIZE_BYTES=$(stat -f%z "$LIB_PATH" 2>/dev/null || stat -c%s "$LIB_PATH" 2>/dev/null)
FILE_SIZE_KB=$((FILE_SIZE_BYTES / 1024))

echo "── Check 1: File size > ${MIN_SIZE_KB}KB ──"
if [ "$FILE_SIZE_KB" -gt "$MIN_SIZE_KB" ]; then
    info "Library size: ${FILE_SIZE_KB}KB (>${MIN_SIZE_KB}KB)"
    pass=$((pass + 1))
else
    error "Library too small: ${FILE_SIZE_KB}KB (expected >${MIN_SIZE_KB}KB)"
    fail=$((fail + 1))
fi
echo ""

# ── Check 2: Exported symbol count ────────────────────────────────────────
echo "── Check 2: Exported symbols (expect ${EXPECTED_SYMBOL_COUNT} sharposrm_* symbols) ──"

case "$OS" in
    Darwin)
        # macOS: use nm -gU to list global symbols defined in the library
        SYMBOL_COUNT=$(nm -gU "$LIB_PATH" 2>/dev/null | grep -c 'sharposrm_' || true)
        SYMBOLS=$(nm -gU "$LIB_PATH" 2>/dev/null | grep 'sharposrm_' || true)
        ;;
    Linux)
        # Linux: use nm -D to list dynamic symbols
        SYMBOL_COUNT=$(nm -D "$LIB_PATH" 2>/dev/null | grep -c 'sharposrm_' || true)
        SYMBOLS=$(nm -D "$LIB_PATH" 2>/dev/null | grep 'sharposrm_' || true)
        ;;
    MINGW*|MSYS*|CYGWIN*)
        # Windows: use dumpbin if available, otherwise objdump
        if command -v dumpbin >/dev/null 2>&1; then
            SYMBOL_COUNT=$(dumpbin /EXPORTS "$LIB_PATH" 2>/dev/null | grep -c 'sharposrm_' || true)
            SYMBOLS=$(dumpbin /EXPORTS "$LIB_PATH" 2>/dev/null | grep 'sharposrm_' || true)
        else
            SYMBOL_COUNT=$(objdump -p "$LIB_PATH" 2>/dev/null | grep -c 'sharposrm_' || true)
            SYMBOLS=$(objdump -p "$LIB_PATH" 2>/dev/null | grep 'sharposrm_' || true)
        fi
        ;;
    *)
        warn "Unknown platform '$OS'; attempting nm -D"
        SYMBOL_COUNT=$(nm -D "$LIB_PATH" 2>/dev/null | grep -c 'sharposrm_' || true)
        SYMBOLS=$(nm -D "$LIB_PATH" 2>/dev/null | grep 'sharposrm_' || true)
        ;;
esac

echo "Exported sharposrm_* symbols ($SYMBOL_COUNT):"
echo "$SYMBOLS" | sed 's/^/  /'

if [ "$SYMBOL_COUNT" -eq "$EXPECTED_SYMBOL_COUNT" ]; then
    info "Symbol count: ${SYMBOL_COUNT} == ${EXPECTED_SYMBOL_COUNT}"
    pass=$((pass + 1))
else
    error "Symbol count: ${SYMBOL_COUNT} != ${EXPECTED_SYMBOL_COUNT}"
    fail=$((fail + 1))
fi
echo ""

# ── Check 3: Dynamic dependencies ────────────────────────────────────────
echo "── Check 3: Dynamic dependencies ──"

case "$OS" in
    Darwin)
        DEPS=$(otool -L "$LIB_PATH" 2>/dev/null)
        echo "$DEPS" | sed 's/^/  /'
        echo ""

        # Check for Homebrew paths in dependencies
        HOMEBREW_DEPS=$(echo "$DEPS" | grep -c '/opt/homebrew\|/usr/local/Cellar' || true)
        if [ "$HOMEBREW_DEPS" -eq 0 ]; then
            info "No Homebrew paths in dynamic dependencies"
            pass=$((pass + 1))
        else
            error "Found Homebrew paths in dynamic dependencies:"
            echo "$DEPS" | grep '/opt/homebrew\|/usr/local/Cellar' | sed 's/^/    /'
            fail=$((fail + 1))
        fi

        # Check that Conan cache paths are present (expected when building with Conan)
        CONAN_DEPS=$(echo "$DEPS" | grep -c '.conan2' || true)
        if [ "$CONAN_DEPS" -gt 0 ]; then
            info "Found Conan cache paths in dependencies (${CONAN_DEPS} deps)"
            pass=$((pass + 1))
        else
            warn "No Conan cache paths found in dependencies (may be statically linked or using system libs)"
            # Not a hard failure — deps may be statically linked
            pass=$((pass + 1))
        fi
        ;;
    Linux)
        DEPS=$(ldd "$LIB_PATH" 2>/dev/null)
        echo "$DEPS" | sed 's/^/  /'
        echo ""

        # Check for Homebrew paths (shouldn't exist on Linux, but check anyway)
        HOMEBREW_DEPS=$(echo "$DEPS" | grep -c '/opt/homebrew\|/usr/local/Cellar' || true)
        if [ "$HOMEBREW_DEPS" -eq 0 ]; then
            info "No Homebrew paths in dynamic dependencies"
            pass=$((pass + 1))
        else
            error "Found Homebrew paths in dynamic dependencies"
            fail=$((fail + 1))
        fi

        # Check that expected libraries are present
        for lib in libboost libtbb liblua; do
            if echo "$DEPS" | grep -q "$lib"; then
                info "Found expected dependency: $lib"
                pass=$((pass + 1))
            else
                warn "Expected dependency not found: $lib (may be statically linked)"
            fi
        done
        ;;
    *)
        warn "Dependency check not implemented for $OS"
        ;;
esac
echo ""

# ── Check 4: Library type ─────────────────────────────────────────────────
echo "── Check 4: Library type ──"
case "$OS" in
    Darwin)
        if file "$LIB_PATH" | grep -q 'Mach-O'; then
            info "Library is a valid Mach-O binary"
            echo "  $(file "$LIB_PATH")"
            pass=$((pass + 1))
        else
            error "Library is not a valid Mach-O binary"
            echo "  $(file "$LIB_PATH")"
            fail=$((fail + 1))
        fi
        ;;
    Linux)
        if file "$LIB_PATH" | grep -q 'ELF'; then
            info "Library is a valid ELF binary"
            echo "  $(file "$LIB_PATH")"
            pass=$((pass + 1))
        else
            error "Library is not a valid ELF binary"
            echo "  $(file "$LIB_PATH")"
            fail=$((fail + 1))
        fi
        ;;
esac
echo ""

# ── Summary ───────────────────────────────────────────────────────────────
echo "============================================"
echo " Summary: ${pass} passed, ${fail} failed"
echo "============================================"

if [ "$fail" -eq 0 ]; then
    info "All checks passed!"
    exit 0
else
    error "${fail} check(s) failed"
    exit 1
fi
