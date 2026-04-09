#ifndef SHARPOSRM_BRIDGE_H
#define SHARPOSRM_BRIDGE_H

#include <stddef.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Cross-platform export macro */
#if defined(_WIN32) || defined(__CYGWIN__)
    #ifdef SHARPOSRM_BUILDING
        #define SHARPOSRM_EXPORT __declspec(dllexport)
    #else
        #define SHARPOSRM_EXPORT __declspec(dllimport)
    #endif
#else
    #define SHARPOSRM_EXPORT __attribute__((visibility("default")))
#endif

/* Algorithm enum matching osrm::EngineConfig::Algorithm */
typedef enum SharposrmAlgorithm
{
    SHARPOSRM_ALGORITHM_CH = 0,
    SHARPOSRM_ALGORITHM_MLD = 1
} SharposrmAlgorithm;

/* Feature dataset bitmask matching osrm::storage::FeatureDataset */
typedef enum SharposrmFeatureDataset
{
    SHARPOSRM_FEATURE_ROUTE_STEPS    = 1 << 0, /* 0x01 */
    SHARPOSRM_FEATURE_ROUTE_GEOMETRY = 1 << 1  /* 0x02 */
} SharposrmFeatureDataset;

/* C-compatible mirror of osrm::EngineConfig.
 * Strings (storage_config_path, memory_file, dataset_name) are borrowed pointers —
 * the caller must keep them alive for the duration of sharposrm_create().
 * Integer booleans use 0 = false, non-zero = true.
 */
typedef struct SharposrmEngineConfig
{
    const char* storage_config_path;       /* required: path to .osrm base file */
    SharposrmAlgorithm algorithm;          /* CH or MLD */

    int use_shared_memory;                 /* 0 or 1, default 0 */
    int use_mmap;                          /* 0 or 1, default 1 */

    /* Service limits (-1 means unlimited) */
    int max_locations_trip;
    int max_locations_viaroute;
    int max_locations_distance_table;
    int max_locations_map_matching;
    double max_radius_map_matching;
    int max_results_nearest;
    double default_radius;
    int max_alternatives;

    /* Feature dataset disable bitmask */
    unsigned int disable_feature_datasets; /* combination of SharposrmFeatureDataset flags */

    /* Optional: nullable strings */
    const char* memory_file;               /* nullptr if unused */
    const char* dataset_name;              /* nullptr if unused */
} SharposrmEngineConfig;

/**
 * Create an OSRM engine instance.
 *
 * Translates the C config struct to osrm::EngineConfig, constructs osrm::OSRM on the heap,
 * and returns an opaque handle. On failure, returns nullptr; call sharposrm_get_last_error()
 * for a descriptive message.
 *
 * @param config  Pointer to config struct. Must not be nullptr.
 * @return Opaque engine handle, or nullptr on failure.
 */
SHARPOSRM_EXPORT void* sharposrm_create(const SharposrmEngineConfig* config);

/**
 * Destroy an OSRM engine instance.
 *
 * Safe to call with nullptr (no-op).
 *
 * @param engine  Opaque handle returned by sharposrm_create.
 */
SHARPOSRM_EXPORT void sharposrm_destroy(void* engine);

/**
 * Validate an engine config without creating an engine.
 *
 * Translates the C struct to osrm::EngineConfig and calls IsValid().
 *
 * @param config  Pointer to config struct. Must not be nullptr.
 * @return 1 if valid, 0 if invalid.
 */
SHARPOSRM_EXPORT int sharposrm_config_is_valid(const SharposrmEngineConfig* config);

/**
 * Retrieve the last error message from the current thread.
 *
 * Error messages are stored in thread-local storage and set by sharposrm_create()
 * and sharposrm_config_is_valid() on failure. The returned pointer is valid until
 * the next call to any sharposrm_* function on this thread.
 *
 * @return Error string, or nullptr if no error is stored.
 */
SHARPOSRM_EXPORT const char* sharposrm_get_last_error(void);

/**
 * Free a string previously returned by sharposrm_get_last_error() or other
 * functions that allocate C strings. Safe to call with nullptr.
 *
 * @param s  String to free.
 */
SHARPOSRM_EXPORT void sharposrm_free_string(char* s);

/* ── Route service ────────────────────────────────────────────────────── */

/* Approach type enum matching osrm::engine::Approach */
typedef enum SharposrmApproachType
{
    SHARPOSRM_APPROACH_CURB = 0,
    SHARPOSRM_APPROACH_UNRESTRICTED = 1,
    SHARPOSRM_APPROACH_OPPOSITE = 2
} SharposrmApproachType;

/* Snapping type enum matching osrm::engine::api::BaseParameters::SnappingType */
typedef enum SharposrmSnappingType
{
    SHARPOSRM_SNAPPING_DEFAULT = 0,
    SHARPOSRM_SNAPPING_ANY = 1
} SharposrmSnappingType;

/* Geometries type enum matching osrm::engine::api::RouteParameters::GeometriesType */
typedef enum SharposrmGeometriesType
{
    SHARPOSRM_GEOMETRIES_POLYLINE  = 0,
    SHARPOSRM_GEOMETRIES_POLYLINE6 = 1,
    SHARPOSRM_GEOMETRIES_GEOJSON   = 2
} SharposrmGeometriesType;

/* Overview type enum matching osrm::engine::api::RouteParameters::OverviewType */
typedef enum SharposrmOverviewType
{
    SHARPOSRM_OVERVIEW_SIMPLIFIED = 0,
    SHARPOSRM_OVERVIEW_FULL       = 1,
    SHARPOSRM_OVERVIEW_FALSE      = 2
} SharposrmOverviewType;

/* Annotations bitmask matching osrm::engine::api::RouteParameters::AnnotationsType */
typedef enum SharposrmAnnotationsType
{
    SHARPOSRM_ANNOTATIONS_NONE        = 0,
    SHARPOSRM_ANNOTATIONS_DURATION    = 1 << 0,  /* 0x01 */
    SHARPOSRM_ANNOTATIONS_NODES       = 1 << 1,  /* 0x02 */
    SHARPOSRM_ANNOTATIONS_DISTANCE    = 1 << 2,  /* 0x04 */
    SHARPOSRM_ANNOTATIONS_WEIGHT      = 1 << 3,  /* 0x08 */
    SHARPOSRM_ANNOTATIONS_DATASOURCES = 1 << 4,  /* 0x10 */
    SHARPOSRM_ANNOTATIONS_SPEED       = 1 << 5,  /* 0x20 */
    SHARPOSRM_ANNOTATIONS_ALL         = 0x3F
} SharposrmAnnotationsType;

/*
 * Route parameters for the OSRM Route service.
 *
 * Coordinates use OSRM convention: longitude first, latitude second.
 * Integer booleans use 0 = false, non-zero = true.
 * Optional arrays (radiuses) use nullable pointers; when null, OSRM defaults apply.
 * The continue_straight field uses -1 for "not set" (no preference), 0 = false, 1 = true.
 *
 * Field order must be exactly reproducible in the C# blittable struct.
 */
typedef struct SharposrmRouteParams
{
    const double* longitudes;              /* required: array of longitude values */
    const double* latitudes;               /* required: array of latitude values */
    int coordinate_count;                  /* number of coordinate pairs (min 2) */

    int steps;                             /* 0 or 1: return route steps per leg */
    int alternatives;                      /* 0 or 1: try to find alternative routes */
    unsigned int number_of_alternatives;   /* max number of alternative routes */

    int annotations;                       /* 0 or 1: enable annotations */
    unsigned int annotations_type;         /* SharposrmAnnotationsType bitmask */

    SharposrmGeometriesType geometries_type; /* polyline, polyline6, or geojson */
    SharposrmOverviewType overview_type;      /* simplified, full, false, or by_legs */

    int continue_straight;                 /* -1 = not set, 0 = false, 1 = true */

    const double* radiuses;                /* nullable: per-coordinate search radius in meters */
    int radius_count;                      /* number of radius values (0 if radiuses is null) */

    /* Bearings: interleaved [bearing, range, bearing, range, ...] pairs as shorts.
     * bearing_count is the number of coordinate pairs (each coordinate has 2 shorts).
     * bearing_count == 0 means no bearings (OSRM defaults apply).
     * Individual bearing entries can be {-1, -1} to mean "no bearing for this coordinate". */
    const short* bearings;                 /* nullable: interleaved bearing/range pairs */
    int bearing_count;                     /* number of bearing entries (0 if bearings is null) */

    /* Hints: per-coordinate base64-encoded hint strings for faster subsequent queries.
     * hints[i] can be nullptr for coordinates without a hint.
     * hint_count == 0 means no hints provided. */
    const char** hints;                    /* nullable: array of nullable hint strings */
    int hint_count;                        /* number of hint entries (0 if hints is null) */

    int generate_hints;                    /* 0 or 1: add hints to response (default 1) */
    int skip_waypoints;                    /* 0 or 1: remove waypoints array from response */

    const char* approaches;                /* nullable: byte array, one byte per coordinate, -1/0xFF = not set */
    int approach_count;                    /* 0 if approaches is null */
    const char** exclude;                  /* nullable: array of road class name strings */
    int exclude_count;                     /* 0 if exclude is null */
    int snapping;                          /* SharposrmSnappingType value, default 0 */

    const size_t* waypoints;               /* nullable array of waypoint indices */
    int waypoint_count;                    /* 0 if waypoints is null */
} SharposrmRouteParams;

/**
 * Run the OSRM Route service.
 *
 * Translates SharposrmRouteParams to osrm::RouteParameters, calls engine->Route(),
 * renders the JSON result to a heap-allocated string, and stores the pointer in
 * *result_json. The caller owns the string and must free it with sharposrm_free_result().
 *
 * On both Ok and Error status, the JSON string is populated — OSRM includes error
 * details in the json::Object even on failure.
 *
 * @param engine       Opaque engine handle from sharposrm_create(). Must not be nullptr.
 * @param params       Pointer to route parameters. Must not be nullptr.
 * @param result_json  [out] Pointer to receive the heap-allocated JSON string.
 *                     Caller must free with sharposrm_free_result().
 * @return 0 on Ok, 1 on Error (matching osrm::Status).
 */
SHARPOSRM_EXPORT int sharposrm_route(void* engine,
                                     const SharposrmRouteParams* params,
                                     char** result_json);

/**
 * Free a JSON result string previously returned by sharposrm_route() etc.
 * Also frees raw byte buffers returned by sharposrm_tile().
 * Safe to call with nullptr.
 *
 * @param result  Buffer to free.
 */
SHARPOSRM_EXPORT void sharposrm_free_result(char* result);

/* ── Table service enums ──────────────────────────────────────────────── */

/* Annotations bitmask for the Table service (Duration + Distance only). */
typedef enum SharposrmTableAnnotationsType
{
    SHARPOSRM_TABLE_ANNOTATIONS_NONE     = 0,
    SHARPOSRM_TABLE_ANNOTATIONS_DURATION  = 0x01,
    SHARPOSRM_TABLE_ANNOTATIONS_DISTANCE  = 0x02,
    SHARPOSRM_TABLE_ANNOTATIONS_ALL       = 0x03
} SharposrmTableAnnotationsType;

/* Fallback coordinate type for the Table service. */
typedef enum SharposrmFallbackCoordinateType
{
    SHARPOSRM_FALLBACK_COORDINATE_INPUT   = 0,
    SHARPOSRM_FALLBACK_COORDINATE_SNAPPED = 1
} SharposrmFallbackCoordinateType;

/* Source/destination type for the Trip service. */
typedef enum SharposrmSourceType
{
    SHARPOSRM_SOURCE_ANY   = 0,
    SHARPOSRM_SOURCE_FIRST = 1
} SharposrmSourceType;

typedef enum SharposrmDestinationType
{
    SHARPOSRM_DESTINATION_ANY  = 0,
    SHARPOSRM_DESTINATION_LAST = 1
} SharposrmDestinationType;

/* Gaps type for the Match service. */
typedef enum SharposrmGapsType
{
    SHARPOSRM_GAPS_SPLIT  = 0,
    SHARPOSRM_GAPS_IGNORE = 1
} SharposrmGapsType;

/* ── Table service ────────────────────────────────────────────────────── */

/*
 * Table parameters for the OSRM Table (distance matrix) service.
 *
 * Coordinates use OSRM convention: longitude first, latitude second.
 * Integer booleans use 0 = false, non-zero = true.
 * Optional arrays (sources, destinations, radiuses) use nullable pointers;
 * when null, OSRM defaults apply (use all coordinates).
 *
 * Field order must be exactly reproducible in the C# blittable struct.
 */
typedef struct SharposrmTableParams
{
    const double* longitudes;              /* required: array of longitude values */
    const double* latitudes;               /* required: array of latitude values */
    int coordinate_count;                  /* number of coordinate pairs (min 2) */

    const size_t* sources;                 /* nullable: indices of source coordinates */
    int source_count;                      /* number of source indices (0 if sources is null) */

    const size_t* destinations;            /* nullable: indices of destination coordinates */
    int destination_count;                 /* number of destination indices (0 if destinations is null) */

    double fallback_speed;                 /* fallback speed in m/s (> 0), use INVALID_FALLBACK_SPEED to disable */
    SharposrmFallbackCoordinateType fallback_coordinate_type; /* input or snapped */

    SharposrmTableAnnotationsType annotations_type; /* which annotations to compute */

    double scale_factor;                   /* scale factor for table values (> 0, default 1) */

    const double* radiuses;                /* nullable: per-coordinate search radius in meters */
    int radius_count;                      /* number of radius values (0 if radiuses is null) */

    const short* bearings;                 /* nullable: interleaved bearing/range pairs */
    int bearing_count;                     /* number of bearing entries (0 if bearings is null) */

    const char** hints;                    /* nullable: array of nullable hint strings */
    int hint_count;                        /* number of hint entries (0 if hints is null) */

    int generate_hints;                    /* 0 or 1: add hints to response */
    int skip_waypoints;                    /* 0 or 1: remove waypoints from response */

    const char* approaches;                /* nullable: byte array, one byte per coordinate, -1/0xFF = not set */
    int approach_count;                    /* 0 if approaches is null */
    const char** exclude;                  /* nullable: array of road class name strings */
    int exclude_count;                     /* 0 if exclude is null */
    int snapping;                          /* SharposrmSnappingType value, default 0 */
} SharposrmTableParams;

SHARPOSRM_EXPORT int sharposrm_table(void* engine,
                                     const SharposrmTableParams* params,
                                     char** result_json);

/* ── Nearest service ──────────────────────────────────────────────────── */

/*
 * Nearest parameters for the OSRM Nearest service.
 *
 * Field order must be exactly reproducible in the C# blittable struct.
 */
typedef struct SharposrmNearestParams
{
    const double* longitudes;              /* required: array of longitude values */
    const double* latitudes;               /* required: array of latitude values */
    int coordinate_count;                  /* number of coordinate pairs (min 1) */

    unsigned int number_of_results;        /* number of nearest results (min 1, default 1) */

    const double* radiuses;                /* nullable: per-coordinate search radius in meters */
    int radius_count;                      /* number of radius values (0 if radiuses is null) */

    const short* bearings;                 /* nullable: interleaved bearing/range pairs */
    int bearing_count;                     /* number of bearing entries (0 if bearings is null) */

    const char** hints;                    /* nullable: array of nullable hint strings */
    int hint_count;                        /* number of hint entries (0 if hints is null) */

    int generate_hints;                    /* 0 or 1: add hints to response */
    int skip_waypoints;                    /* 0 or 1: remove waypoints from response */

    const char* approaches;                /* nullable: byte array, one byte per coordinate, -1/0xFF = not set */
    int approach_count;                    /* 0 if approaches is null */
    const char** exclude;                  /* nullable: array of road class name strings */
    int exclude_count;                     /* 0 if exclude is null */
    int snapping;                          /* SharposrmSnappingType value, default 0 */
} SharposrmNearestParams;

SHARPOSRM_EXPORT int sharposrm_nearest(void* engine,
                                       const SharposrmNearestParams* params,
                                       char** result_json);

/* ── Trip service ─────────────────────────────────────────────────────── */

/*
 * Trip parameters for the OSRM Trip service.
 *
 * Contains all Route fields (flat layout, no inheritance in C) plus
 * source_type, destination_type, and roundtrip.
 *
 * Field order must be exactly reproducible in the C# blittable struct.
 */
typedef struct SharposrmTripParams
{
    /* ─ Route base fields ─ */
    const double* longitudes;
    const double* latitudes;
    int coordinate_count;

    int steps;
    int alternatives;
    unsigned int number_of_alternatives;

    int annotations;
    unsigned int annotations_type;         /* SharposrmAnnotationsType bitmask */

    SharposrmGeometriesType geometries_type;
    SharposrmOverviewType overview_type;

    int continue_straight;                 /* -1 = not set, 0 = false, 1 = true */

    const double* radiuses;
    int radius_count;

    const short* bearings;                 /* nullable: interleaved bearing/range pairs */
    int bearing_count;                     /* number of bearing entries (0 if bearings is null) */

    const char** hints;                    /* nullable: array of nullable hint strings */
    int hint_count;                        /* number of hint entries (0 if hints is null) */

    int generate_hints;
    int skip_waypoints;

    const char* approaches;                /* nullable: byte array, one byte per coordinate, -1/0xFF = not set */
    int approach_count;                    /* 0 if approaches is null */
    const char** exclude;                  /* nullable: array of road class name strings */
    int exclude_count;                     /* 0 if exclude is null */
    int snapping;                          /* SharposrmSnappingType value, default 0 */

    /* ─ Trip-specific fields ─ */
    SharposrmSourceType source_type;
    SharposrmDestinationType destination_type;
    int roundtrip;                         /* 0 or 1, default 1 */
} SharposrmTripParams;

SHARPOSRM_EXPORT int sharposrm_trip(void* engine,
                                    const SharposrmTripParams* params,
                                    char** result_json);

/* ── Match service ─────────────────────────────────────────────────────── */

/*
 * Match parameters for the OSRM Map Matching service.
 *
 * Contains all Route fields (flat layout, no inheritance in C) plus
 * timestamps, gaps, and tidy.
 *
 * Field order must be exactly reproducible in the C# blittable struct.
 */
typedef struct SharposrmMatchParams
{
    /* ─ Route base fields ─ */
    const double* longitudes;
    const double* latitudes;
    int coordinate_count;

    int steps;
    int alternatives;
    unsigned int number_of_alternatives;

    int annotations;
    unsigned int annotations_type;         /* SharposrmAnnotationsType bitmask */

    SharposrmGeometriesType geometries_type;
    SharposrmOverviewType overview_type;

    int continue_straight;                 /* -1 = not set, 0 = false, 1 = true */

    const double* radiuses;
    int radius_count;

    const short* bearings;                 /* nullable: interleaved bearing/range pairs */
    int bearing_count;                     /* number of bearing entries (0 if bearings is null) */

    const char** hints;                    /* nullable: array of nullable hint strings */
    int hint_count;                        /* number of hint entries (0 if hints is null) */

    int generate_hints;
    int skip_waypoints;

    const char* approaches;                /* nullable: byte array, one byte per coordinate, -1/0xFF = not set */
    int approach_count;                    /* 0 if approaches is null */
    const char** exclude;                  /* nullable: array of road class name strings */
    int exclude_count;                     /* 0 if exclude is null */
    int snapping;                          /* SharposrmSnappingType value, default 0 */

    /* ─ Match-specific fields ─ */
    const unsigned int* timestamps;        /* nullable: per-coordinate Unix timestamps */
    int timestamp_count;                   /* number of timestamps (0 if timestamps is null) */

    SharposrmGapsType gaps;                /* split or ignore */
    int tidy;                              /* 0 or 1: tidy input coordinates */
} SharposrmMatchParams;

SHARPOSRM_EXPORT int sharposrm_match(void* engine,
                                     const SharposrmMatchParams* params,
                                     char** result_json);

/* ── Tile service ─────────────────────────────────────────────────────── */

/*
 * Tile parameters for the OSRM Tile (MVT) service.
 *
 * Returns raw binary MVT data (not JSON). Use sharposrm_free_result() to free
 * the returned buffer.
 */
typedef struct SharposrmTileParams
{
    unsigned int x;                        /* tile x coordinate */
    unsigned int y;                        /* tile y coordinate */
    unsigned int z;                        /* zoom level (12-19) */
} SharposrmTileParams;

/**
 * Run the OSRM Tile service.
 *
 * Returns raw binary MVT data (not JSON). The caller owns the buffer
 * and must free it with sharposrm_free_result().
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to tile parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated binary data.
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_tile(void* engine,
                                    const SharposrmTileParams* params,
                                    char** result_data,
                                    int* result_length);

/* ── Data pipeline: extract, partition, customize, contract ───────────── */

/*
 * Extractor config for the OSRM data extraction pipeline stage.
 *
 * input_path:   required path to .osm.pbf or .osm.xml input file
 * profile_path: required path to the Lua profile script (e.g. car.lua)
 * output_path:  optional base path for output files; if null, derived from input_path
 *
 * Strings are borrowed pointers — the caller must keep them alive for the
 * duration of the sharposrm_extract() call.
 */
typedef struct SharposrmExtractorConfig
{
    const char* input_path;                /* required: .osm.pbf / .osm.xml */
    const char* profile_path;              /* required: .lua profile script */
    const char* output_path;               /* nullable: base path for outputs; null = derive from input */

    unsigned int requested_num_threads;     /* 0 = hardware concurrency */
    unsigned int small_component_size;      /* default 1000 */
    int use_metadata;                       /* 0 or 1 */
    int parse_conditionals;                 /* 0 or 1 */
    int use_locations_cache;                /* 0 or 1, default 1 */
    int dump_nbg_graph;                     /* 0 or 1 */
} SharposrmExtractorConfig;

/*
 * Partitioner config for the OSRM graph partitioning pipeline stage.
 *
 * base_path: required path to the .osrm base file (produced by extract)
 * max_cell_sizes: fixed-size array of 4 cell sizes for the 4-level MLD partition
 */
typedef struct SharposrmPartitionerConfig
{
    const char* base_path;                 /* required: .osrm base file path */

    unsigned int requested_num_threads;     /* 0 = hardware concurrency */
    double balance;                        /* default 1.2 */
    double boundary_factor;                /* default 0.25 */
    size_t num_optimizing_cuts;            /* default 10 */
    size_t small_component_size;           /* default 1000 */
    size_t max_cell_sizes[4];              /* 4-level partition cell sizes */
} SharposrmPartitionerConfig;

/*
 * Customizer config for the OSRM MLD customization pipeline stage.
 *
 * base_path: required path to the .osrm base file
 * segment_speed_lookup_paths: nullable array of segment speed CSV file paths
 * turn_penalty_lookup_paths: nullable array of turn penalty CSV file paths
 */
typedef struct SharposrmCustomizerConfig
{
    const char* base_path;                 /* required: .osrm base file path */

    unsigned int requested_num_threads;     /* 0 = hardware concurrency */

    /* UpdaterConfig fields — nullable string arrays for dynamic speed updates */
    const char** segment_speed_lookup_paths; /* nullable: array of file paths */
    int segment_speed_lookup_count;          /* count (0 if array is null) */
    const char** turn_penalty_lookup_paths;  /* nullable: array of file paths */
    int turn_penalty_lookup_count;           /* count (0 if array is null) */
} SharposrmCustomizerConfig;

/*
 * Contractor config for the OSRM CH contraction pipeline stage.
 *
 * base_path: required path to the .osrm base file
 * segment_speed_lookup_paths: nullable array of segment speed CSV file paths
 * turn_penalty_lookup_paths: nullable array of turn penalty CSV file paths
 */
typedef struct SharposrmContractorConfig
{
    const char* base_path;                 /* required: .osrm base file path */

    unsigned int requested_num_threads;     /* 0 = hardware concurrency */

    /* UpdaterConfig fields — nullable string arrays for dynamic speed updates */
    const char** segment_speed_lookup_paths; /* nullable: array of file paths */
    int segment_speed_lookup_count;          /* count (0 if array is null) */
    const char** turn_penalty_lookup_paths;  /* nullable: array of file paths */
    int turn_penalty_lookup_count;           /* count (0 if array is null) */
} SharposrmContractorConfig;

/**
 * Run the OSRM extraction pipeline stage.
 *
 * Translates the C config struct, calls osrm::extract(), catches exceptions.
 * On success returns 0; on failure returns 1 and sets thread-local error.
 *
 * @param config  Pointer to extractor config. Must not be nullptr.
 * @return 0 on success, 1 on failure.
 */
SHARPOSRM_EXPORT int sharposrm_extract(const SharposrmExtractorConfig* config);

/**
 * Run the OSRM graph partitioning pipeline stage.
 *
 * @param config  Pointer to partitioner config. Must not be nullptr.
 * @return 0 on success, 1 on failure.
 */
SHARPOSRM_EXPORT int sharposrm_partition(const SharposrmPartitionerConfig* config);

/**
 * Run the OSRM MLD customization pipeline stage.
 *
 * @param config  Pointer to customizer config. Must not be nullptr.
 * @return 0 on success, 1 on failure.
 */
SHARPOSRM_EXPORT int sharposrm_customize(const SharposrmCustomizerConfig* config);

/**
 * Run the OSRM CH contraction pipeline stage.
 *
 * @param config  Pointer to contractor config. Must not be nullptr.
 * @return 0 on success, 1 on failure.
 */
SHARPOSRM_EXPORT int sharposrm_contract(const SharposrmContractorConfig* config);

/* ── Flatbuffer-encoded output variants ───────────────────────────────── */

/**
 * Run the OSRM Route service and return a flatbuffer-encoded byte array.
 *
 * Same as sharposrm_route() but returns raw flatbuffer bytes instead of JSON.
 * The caller owns the buffer and must free it with sharposrm_free_result().
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to route parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated flatbuffer data.
 *                      Caller must free with sharposrm_free_result().
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_route_fb(void* engine,
                                        const SharposrmRouteParams* params,
                                        char** result_data,
                                        int* result_length);

/**
 * Run the OSRM Table service and return a flatbuffer-encoded byte array.
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to table parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated flatbuffer data.
 *                      Caller must free with sharposrm_free_result().
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_table_fb(void* engine,
                                        const SharposrmTableParams* params,
                                        char** result_data,
                                        int* result_length);

/**
 * Run the OSRM Nearest service and return a flatbuffer-encoded byte array.
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to nearest parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated flatbuffer data.
 *                      Caller must free with sharposrm_free_result().
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_nearest_fb(void* engine,
                                          const SharposrmNearestParams* params,
                                          char** result_data,
                                          int* result_length);

/**
 * Run the OSRM Trip service and return a flatbuffer-encoded byte array.
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to trip parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated flatbuffer data.
 *                      Caller must free with sharposrm_free_result().
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_trip_fb(void* engine,
                                       const SharposrmTripParams* params,
                                       char** result_data,
                                       int* result_length);

/**
 * Run the OSRM Match service and return a flatbuffer-encoded byte array.
 *
 * @param engine        Opaque engine handle from sharposrm_create().
 * @param params        Pointer to match parameters. Must not be nullptr.
 * @param result_data   [out] Pointer to receive the heap-allocated flatbuffer data.
 *                      Caller must free with sharposrm_free_result().
 * @param result_length [out] Length of the returned data in bytes.
 * @return 0 on Ok, 1 on Error.
 */
SHARPOSRM_EXPORT int sharposrm_match_fb(void* engine,
                                        const SharposrmMatchParams* params,
                                        char** result_data,
                                        int* result_length);

#ifdef __cplusplus
}
#endif

#endif /* SHARPOSRM_BRIDGE_H */
