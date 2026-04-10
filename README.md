# SharpOSRM

> **⚠️ Disclaimer: This project is entirely vibe coded.**

A .NET 8 wrapper for [OSRM](http://project-osrm.org/) (Open Source Routing Machine) using native C interop. SharpOSRM exposes OSRM's routing engine and data pipeline as idiomatic C# APIs, distributed as a cross-platform NuGet package that bundles the native `libsharposrm` shared library for Linux (x64), macOS (x64/arm64), and Windows (x64).

> **⚠️ TBB malloc proxy notice:** The native library links TBB's `tbbmalloc_proxy`, which replaces the process's `malloc`/`free`/`realloc` with TBB's scalable allocator globally. This improves OSRM's routing performance but **can cause crashes and memory corruption in other native libraries** (e.g. SQLite, custom native dependencies) loaded in the same process. If your application uses other native libraries that are incompatible with a replaced malloc, build from source with `-DSTRIP_TBBMALLOC=ON` to disable the proxy at the cost of reduced routing performance.

---

## What it does

OSRM is a high-performance routing engine that operates on OpenStreetMap data. SharpOSRM gives you:

1. **Data pipeline** — prepare raw OSM data (`.osm.pbf`) into a routable dataset using the four OSRM pipeline stages: Extract, Partition, Customize (MLD), Contract (CH).
2. **Routing engine** — load a prepared dataset and issue routing queries in-process without any HTTP server.
3. **Seven routing services** — Route, Table, Nearest, Match, Trip, Tile, and a FlatBuffers variant of each.

All native calls cross a thin C bridge (`libsharposrm`) that wraps the OSRM C++ API. The C# side uses `LibraryImport` P/Invoke with `SafeHandle`-based lifetime management. All blocking OSRM calls have corresponding `*Async` overloads that offload to the thread pool.

---

## Requirements

- .NET 8
- The `Sharposrm` NuGet package (includes native libraries for all supported platforms — no separate OSRM installation needed)

For building from source, see [CI/CD and Testing Guide](docs/TESTING-AND-CI.md).

---

## Installation

```
dotnet add package Sharposrm
```

The NuGet package includes prebuilt native libraries for:

| Runtime ID   | Library              | Platform            |
|-------------|----------------------|---------------------|
| `linux-x64`  | `libsharposrm.so`    | Ubuntu 22.04+        |
| `osx-x64`    | `libsharposrm.dylib` | macOS 13+ (Intel)   |
| `osx-arm64`  | `libsharposrm.dylib` | macOS 15+ (Apple Silicon) |
| `win-x64`    | `sharposrm.dll`      | Windows Server 2025 |

---

## Quick start

### 1. Prepare your data

OSRM cannot route until OSM data has been processed through the pipeline. You need an `.osm.pbf` file (download from [Geofabrik](https://download.geofabrik.de/) or [OSM extracts](https://extract.bbbike.org/)) and a routing profile Lua file.

**Contraction Hierarchies (CH)** — faster queries, longer upfront processing:

```csharp
using Sharposrm;
using Sharposrm.Pipeline;

// Extract
OsrmPipeline.Extract(new ExtractorConfig
{
    InputPath  = "/data/monaco.osm.pbf",
    ProfilePath = "/profiles/car.lua",
    OutputPath  = "/data/monaco",        // base path, OSRM appends extensions
    RequestedThreads = 4,
});

// Contract (CH)
// Note: run on a thread with a large stack — OSRM's contractor is deeply recursive.
var thread = new Thread(() =>
{
    OsrmPipeline.Contract(new ContractorConfig
    {
        BasePath         = "/data/monaco",
        RequestedThreads = 4,
    });
}, maxStackSize: 8 * 1024 * 1024);
thread.Start();
thread.Join();
```

**Multi-Level Dijkstra (MLD)** — more flexible, faster preprocessing:

```csharp
OsrmPipeline.Extract(new ExtractorConfig
{
    InputPath        = "/data/monaco.osm.pbf",
    ProfilePath      = "/profiles/car.lua",
    OutputPath       = "/data/monaco-mld",
    RequestedThreads = 4,
});

OsrmPipeline.Partition(new PartitionerConfig
{
    BasePath         = "/data/monaco-mld",
    RequestedThreads = 4,
});

OsrmPipeline.Customize(new CustomizerConfig
{
    BasePath         = "/data/monaco-mld",
    RequestedThreads = 4,
});
```

> **Important:** All pipeline `RequestedThreads` must be ≥ 1. Setting 0 crashes OSRM's TBB backend.

### 2. Create an engine

```csharp
await using var engine = await OsrmEngine.CreateAsync(new EngineConfig
{
    StoragePath = "/data/monaco",    // base path produced by the pipeline
    Algorithm   = Algorithm.CH,      // or Algorithm.MLD
});
```

`OsrmEngine` is `IAsyncDisposable`. Engine creation loads the dataset into memory — for large datasets this can take several seconds. `CreateAsync` offloads this to the thread pool so it doesn't block an async context.

### 3. Call routing services

#### Route

Compute a route between two or more waypoints:

```csharp
using Sharposrm.Route;

var response = engine.Route(new RouteParameters
{
    Coordinates = new[]
    {
        (7.41337, 43.72956),   // (longitude, latitude)
        (7.41983, 43.73115),
    },
    Steps       = true,
    Annotations = true,
    AnnotationTypes = AnnotationsType.Duration | AnnotationsType.Speed,
    Geometries  = GeometriesType.Polyline,
    Overview    = OverviewType.Simplified,
    Alternatives = false,
    GenerateHints = true,
});

Console.WriteLine($"Distance: {response.Routes![0].Distance}m");
Console.WriteLine($"Duration: {response.Routes[0].Duration}s");
```

#### Table

Compute a duration/distance matrix between multiple origins and destinations:

```csharp
using Sharposrm.Table;

var response = engine.Table(new TableParameters
{
    Coordinates = new[]
    {
        (7.41337, 43.72956),
        (7.41983, 43.73115),
        (7.42500, 43.73500),
    },
    // Optional: separate Sources/Destinations index lists for one-to-many queries
});

// Durations[i][j] is null when the pair cannot be routed
double? duration = response.Durations![0][1];
```

#### Nearest

Snap a coordinate to the nearest road network node:

```csharp
using Sharposrm.Nearest;

var response = engine.Nearest(new NearestParameters
{
    Coordinates = new[] { (7.41337, 43.72956) },
    Number = 3,    // return 3 nearest snapping candidates
});
```

#### Match

Map-match a GPS trace to the road network:

```csharp
using Sharposrm.Match;

var response = engine.Match(new MatchParameters
{
    Coordinates = gpsTrace,       // IReadOnlyList<(double, double)>
    Timestamps  = timestamps,     // optional, Unix epoch seconds
    Radiuses    = radiuses,       // optional, per-point search radius in metres
});
```

#### Trip

Solve a Travelling Salesman Problem (shortest round trip through all waypoints):

```csharp
using Sharposrm.Trip;

var response = engine.Trip(new TripParameters
{
    Coordinates  = waypoints,
    RoundTrip    = true,
    Source       = TripSourceType.First,
    Destination  = TripDestinationType.Last,
});
```

#### Tile

Fetch a Mapbox Vector Tile for a given tile coordinate (returns raw MVT bytes):

```csharp
using Sharposrm.Tile;

byte[] mvtData = engine.Tile(new TileParameters
{
    X    = 33660,
    Y    = 22961,
    Zoom = 15,
});
```

#### FlatBuffers variants

Every service except Tile has a `*Flatbuffers()` method that returns raw `byte[]` instead of a deserialized response object, for use cases where you need the FlatBuffers wire format directly:

```csharp
byte[] fbBytes = engine.RouteFlatbuffers(routeParameters);
byte[] fbBytes = engine.TableFlatbuffers(tableParameters);
// etc.
```

---

## EngineConfig reference

| Property | Type | Default | Description |
|---|---|---|---|
| `StoragePath` | `string` | *(required)* | Base path to the prepared `.osrm` dataset |
| `Algorithm` | `Algorithm` | `CH` | `CH` or `MLD` |
| `UseSharedMemory` | `bool` | `false` | Load from shared memory (requires `osrm-datastore`) |
| `UseMmap` | `bool` | `true` | Use memory-mapped file I/O |
| `MaxLocationsTrip` | `int` | `-1` (unlimited) | Per-engine cap for Trip service |
| `MaxLocationsViaroute` | `int` | `-1` | Per-engine cap for Route service |
| `MaxLocationsDistanceTable` | `int` | `-1` | Per-engine cap for Table service |
| `MaxLocationsMapMatching` | `int` | `-1` | Per-engine cap for Match service |
| `MaxResultsNearest` | `int` | `-1` | Per-engine cap for Nearest service |
| `MaxAlternatives` | `int` | `3` | Maximum alternative routes |
| `DisableRouteSteps` | `bool` | `false` | Strip step data from all responses |
| `DisableRouteGeometry` | `bool` | `false` | Strip geometry from all responses |

---

## Error handling

All routing services throw `OsrmException` on failure. The exception message contains OSRM's error code and message (e.g. `"OSRM error: NoRoute — No route found between input coordinates"`).

```csharp
try
{
    var response = engine.Route(parameters);
}
catch (OsrmException ex)
{
    Console.WriteLine(ex.Message);
}
```

---

## Threading

- `OsrmEngine` instances are **not thread-safe**. Use one engine per thread, or synchronise externally.
- Each `*Async` method calls `Task.Run(...)` — it offloads the blocking native call to the thread pool.
- OSRM's CH contraction is deeply recursive. Always run `OsrmPipeline.Contract` on a thread with a stack of at least 8 MB (see example above).

---

## License

SharpOSRM is licensed under the [MIT License](LICENSE).

This package statically links OSRM, which is licensed under the [BSD 2-Clause License](THIRD-PARTY-NOTICES.txt). See `THIRD-PARTY-NOTICES.txt` for the full OSRM copyright notice.
