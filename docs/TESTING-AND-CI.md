# Testing, Building, and Publishing — Developer Guide

This document covers:
1. [Building the native library locally](#1-building-the-native-library-locally)
2. [Running the test suite locally](#2-running-the-test-suite-locally)
3. [How the CI/CD pipeline works](#3-how-the-cicd-pipeline-works)
4. [Packing and publishing the NuGet package](#4-packing-and-publishing-the-nuget-package)

---

## 1. Building the native library locally

The C# library delegates all routing work to `libsharposrm` — a shared library compiled from `src/Native/sharposrm_bridge.cpp` against the OSRM C++ engine. You must build this before running any tests.

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| CMake | ≥ 3.18 | |
| Python | ≥ 3.9 | For Conan |
| Conan | 2.x | `pip install conan` |
| Ninja | any | `brew install ninja` / `apt install ninja-build` |
| C++20 compiler | Clang/GCC/MSVC | Xcode CLT on macOS, build-essential on Linux |
| Git submodules | — | `git submodule update --init --recursive` |

### Steps

```bash
# 1. Clone with submodules (the osrm-backend submodule is required)
git clone --recurse-submodules https://github.com/your-org/sharposrm
cd sharposrm

# 2. Set up Conan profile (once per machine)
pip install conan
conan profile detect --force

# 3. Configure with the correct preset for your platform:
#   macos-arm64  — Apple Silicon
#   macos-x64    — Intel Mac
#   linux-x64    — Linux x86_64
#   win-x64      — Windows x64 (uses Visual Studio generator)

cmake --preset macos-arm64   # adjust as needed

# 4. Build
cmake --build --preset macos-arm64

# Output:
#   build/macos-arm64/libsharposrm.dylib   (macOS)
#   build/linux-x64/libsharposrm.so        (Linux)
#   build/win-x64/Release/sharposrm.dll    (Windows)
```

The build uses `add_subdirectory(osrm-backend)` to compile OSRM from the submodule. Conan downloads and builds all C++ dependencies (Boost, TBB, Lua, etc.). **The first build takes 30–60 minutes** because Boost is compiled from source. Subsequent builds use Conan's package cache and are much faster.

### Verifying the build

```bash
bash scripts/verify-build.sh build/macos-arm64/libsharposrm.dylib
```

This script checks that the library exists and that the exported OSRM symbols are present.

---

## 2. Running the test suite locally

There are three test projects, each with different requirements.

### 2.1 Unit/service tests — `Sharposrm.Tests`

These tests exercise all seven routing services (Route, Table, Nearest, Match, Trip, Tile, FlatBuffers) and the pipeline API. They are split into two groups:

- **Negative tests** — parameter validation guards, disposed-engine checks, cancellation. No native data needed. Always runnable.
- **Positive tests** — real routing results. Require the Monaco OSM PBF (included in the submodule) and the built native library on `PATH`/`LD_LIBRARY_PATH`/`DYLD_LIBRARY_PATH`.

The fixture (`MonacoDataFixture`) runs the full CH and MLD pipeline automatically before any positive test. This takes roughly 30–60 seconds on first run.

#### Running tests

```bash
# From repo root — make sure the native lib is on the search path first:

# macOS:
export DYLD_LIBRARY_PATH="$(pwd)/build/macos-arm64:$DYLD_LIBRARY_PATH"

# Linux:
export LD_LIBRARY_PATH="$(pwd)/build/linux-x64:$LD_LIBRARY_PATH"

# Windows (PowerShell):
$env:PATH = "$(Resolve-Path build\win-x64\Release);$env:PATH"

# Run all tests:
dotnet test test/Sharposrm.Tests/ -c Debug --logger "console;verbosity=normal"

# Run only negative (non-data-dependent) tests:
dotnet test test/Sharposrm.Tests/ --filter "Category!=Monaco"

# Run a specific test class:
dotnet test test/Sharposrm.Tests/ --filter "FullyQualifiedName~RouteServicePositiveTests"
```

#### xUnit collection fixture behaviour

All positive test classes belong to the `"MonacoDataSet"` xUnit collection backed by `MonacoDataFixture`. The fixture runs the pipeline once and is shared across all positive test classes. If fixture initialisation fails (e.g. native lib not found, submodule missing), **all positive tests fail** — the negative tests (which are in separate classes outside the collection) are unaffected.

### 2.2 Integration tests — `Sharposrm.IntegrationTests`

These tests prove the full NuGet package consumption path: the managed assembly loads correctly, and the native library resolves from `runtimes/{rid}/native/` (as it would after `dotnet add package`). They use `PackageReference`, not `ProjectReference`.

You cannot run these against the local source tree directly — they require a locally packed `.nupkg`. See [section 4.1](#41-pack-locally) below.

```bash
# After packing locally (see section 4.1):
dotnet nuget add source "$(pwd)/nupkg-output" --name local-sharposrm
dotnet test test/Sharposrm.IntegrationTests/ -c Release --logger "console;verbosity=detailed"
```

---

## 3. How the CI/CD pipeline works

The GitHub Actions workflow (`.github/workflows/build.yml`) runs on every push and pull request to `main`. It has four sequential jobs:

```
build-native (4 platforms, parallel)
       │
       ▼
collect-artifacts
       │
       ▼
pack-nuget
       │
       ▼
integration-test
```

### Job 1: `build-native` (matrix, runs in parallel)

Runs on four runners simultaneously:

| Matrix entry | Runner | Output |
|---|---|---|
| `linux-x64` | `ubuntu-24.04` | `build/linux-x64/libsharposrm.so` |
| `macos-x64` | `macos-13` | `build/macos-x64/libsharposrm.dylib` |
| `macos-arm64` | `macos-15` | `build/macos-arm64/libsharposrm.dylib` |
| `win-x64` | `windows-2025` | `build/win-x64/Release/sharposrm.dll` |

Each runner:
1. Checks out with `--recurse-submodules`
2. Creates a Python venv and installs Conan 2.x
3. Detects/restores the Conan profile
4. Restores Conan package cache (`~/.conan2`) keyed on `conanfile.py` hash
5. Runs `cmake --preset <platform>` (triggers Conan install + CMake configure)
6. Runs `cmake --build --preset <platform>`
7. Verifies the library with `scripts/verify-build.sh`
8. Uploads the library as a GitHub Actions artifact (retained 7 days)

The matrix uses `fail-fast: false` so one platform failure does not cancel the others.

### Job 2: `collect-artifacts`

Downloads all four artifacts and asserts that every expected directory is present. This is a gate: `pack-nuget` does not start unless all four platform builds succeeded.

### Job 3: `pack-nuget`

1. Downloads all four native artifacts
2. Reorganises them from artifact-named directories (`sharposrm-linux-x64/`, `sharposrm-macos-*`) into NuGet RID directories (`linux-x64/`, `osx-x64/`, `osx-arm64/`, `win-x64/`)  
   *(Note: artifact names use `macos-*`; NuGet RIDs use `osx-*`)*
3. Runs `dotnet pack src/Sharposrm/Sharposrm.csproj -c Release -p:NativeLibsDir=native-libs/ -o nupkg-output/`  
   The `NativeLibsDir` property activates the `<ItemGroup>` in the `.csproj` that bundles the native libs into `runtimes/{rid}/native/`
4. Validates the `.nupkg` contents — checks that all four `runtimes/*/native/` paths are present, plus `LICENSE` and `THIRD-PARTY-NOTICES.txt`
5. Uploads `Sharposrm.0.2.0.nupkg` as an artifact

### Job 4: `integration-test`

1. Downloads the packed `.nupkg`
2. Adds it as a local NuGet source
3. Runs `dotnet test test/Sharposrm.IntegrationTests/`

The integration test constructor runs the full CH pipeline on the Monaco OSM PBF (included in the submodule), then the single `Route_ReturnsValidResponse` test exercises the whole stack end-to-end.

---

## 4. Packing and publishing the NuGet package

### 4.1 Pack locally

To pack locally you need the four native libraries. The easiest way is to build for your local platform and stub the others, or download the CI artifacts.

**Option A: Build for your platform only, stub the rest (for local testing only):**

```bash
# After building locally (e.g. macos-arm64):
mkdir -p native-libs/{linux-x64,osx-x64,osx-arm64,win-x64}

# Copy your local build to the matching RID:
cp build/macos-arm64/libsharposrm.dylib native-libs/osx-arm64/

# Create placeholder files for the others (they won't load, but pack succeeds):
touch native-libs/linux-x64/libsharposrm.so
touch native-libs/osx-x64/libsharposrm.dylib
touch native-libs/win-x64/sharposrm.dll

dotnet pack src/Sharposrm/Sharposrm.csproj -c Release \
  -p:NativeLibsDir=native-libs/ \
  -o nupkg-output/
```

**Option B: Download CI artifacts** (recommended for a real publishable package):

From a successful CI run, download all four `sharposrm-*` artifacts from the GitHub Actions workflow summary page, then reorganise:

```bash
mkdir -p native-libs/{linux-x64,osx-x64,osx-arm64,win-x64}
mv sharposrm-linux-x64/libsharposrm.so       native-libs/linux-x64/
mv sharposrm-macos-x64/libsharposrm.dylib    native-libs/osx-x64/
mv sharposrm-macos-arm64/libsharposrm.dylib  native-libs/osx-arm64/
mv sharposrm-win-x64/sharposrm.dll           native-libs/win-x64/

dotnet pack src/Sharposrm/Sharposrm.csproj -c Release \
  -p:NativeLibsDir=native-libs/ \
  -o nupkg-output/
```

The output is `nupkg-output/Sharposrm.<version>.nupkg`.

### 4.2 Validate the package contents

```bash
# Inspect the package structure:
unzip -l nupkg-output/Sharposrm.0.2.0.nupkg

# Verify all runtime directories are present:
for rid in linux-x64 osx-x64 osx-arm64 win-x64; do
  unzip -l nupkg-output/Sharposrm.0.2.0.nupkg | grep "runtimes/$rid/native/" \
    && echo "✅ $rid" || echo "❌ $rid MISSING"
done
```

### 4.3 Bump the version

The version is set in `src/Sharposrm/Sharposrm.csproj`:

```xml
<Version>0.2.0</Version>
```

Also update the hardcoded version reference in the CI `pack-nuget` validation step (`NUPKG="nupkg-output/Sharposrm.0.2.0.nupkg"`) when bumping.

### 4.4 Publish to NuGet.org

```bash
dotnet nuget push nupkg-output/Sharposrm.0.2.0.nupkg \
  --api-key <YOUR_NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

To automate this in CI, add a `publish` job that triggers only on a version tag (e.g. `v*`):

```yaml
publish-nuget:
  name: Publish to NuGet.org
  runs-on: ubuntu-24.04
  needs: [integration-test]
  if: startsWith(github.ref, 'refs/tags/v')
  steps:
    - uses: actions/download-artifact@v4
      with:
        name: sharposrm-nupkg
        path: nupkg-output
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    - name: Push to NuGet.org
      run: |
        dotnet nuget push nupkg-output/*.nupkg \
          --api-key ${{ secrets.NUGET_API_KEY }} \
          --source https://api.nuget.org/v3/index.json
```

You will need to add `NUGET_API_KEY` as a repository secret in GitHub Settings → Secrets and variables → Actions.

---

## Common issues

### Native library not found at test time

```
System.DllNotFoundException: Unable to load shared library 'sharposrm'
```

The .NET runtime looks for the library in:
- `DYLD_LIBRARY_PATH` (macOS)
- `LD_LIBRARY_PATH` (Linux)
- `PATH` (Windows)
- The directory containing the test assembly

Set the appropriate environment variable to point at your build output directory before running tests.

### Submodule not initialised

```
MonacoDataFixture: Monaco OSM PBF not found at 'osrm-backend/test/data/monaco.osm.pbf'
```

Run: `git submodule update --init --recursive`

### CH contraction stack overflow

```
Process terminated. Stack overflow.
```

The OSRM CH contractor is deeply recursive. On .NET the default thread stack (512 KB–1 MB) is too small. Always run `OsrmPipeline.Contract` on a dedicated thread with at least 8 MB of stack. See the fixture code in `test/Sharposrm.Tests/Fixtures/MonacoDataFixture.cs` for a working pattern.

### All pipeline `RequestedThreads` must be ≥ 1

Setting `RequestedThreads = 0` on any pipeline config (`ExtractorConfig`, `PartitionerConfig`, `CustomizerConfig`, `ContractorConfig`) causes a crash in OSRM's TBB scheduler. This is different from `EngineConfig`, where 0 means "use hardware concurrency".

### Conan first-build time

The first CMake configure triggers a full Conan install that downloads and compiles Boost 1.85 from source (~600 MB, 20–40 min). Subsequent runs with a populated `~/.conan2` cache are fast. In CI, the cache is keyed on `conanfile.py` so it persists across runs unless dependencies change.
