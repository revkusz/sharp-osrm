#include "sharposrm_bridge.h"

#include <osrm/osrm.hpp>
#include <engine/engine_config.hpp>
#include <engine/approach.hpp>
#include <engine/bearing.hpp>
#include <engine/hint.hpp>
#include <osrm/datasets.hpp>
#include <osrm/route_parameters.hpp>
#include <engine/api/route_parameters.hpp>
#include <osrm/table_parameters.hpp>
#include <engine/api/table_parameters.hpp>
#include <osrm/nearest_parameters.hpp>
#include <engine/api/nearest_parameters.hpp>
#include <osrm/trip_parameters.hpp>
#include <engine/api/trip_parameters.hpp>
#include <osrm/match_parameters.hpp>
#include <engine/api/match_parameters.hpp>
#include <osrm/tile_parameters.hpp>
#include <engine/api/tile_parameters.hpp>
#include <engine/api/base_result.hpp>

/* Pipeline headers — extract, partition, customize, contract */
#include <osrm/extractor.hpp>
#include <extractor/extractor_config.hpp>
#include <osrm/partitioner.hpp>
#include <partitioner/partitioner_config.hpp>
#include <osrm/customizer.hpp>
#include <customizer/customizer_config.hpp>
#include <osrm/contractor.hpp>
#include <contractor/contractor_config.hpp>
#include <flatbuffers/flatbuffers.h>
#include <util/json_container.hpp>

#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <memory>
#include <sstream>
#include <string>
#include <variant>
#include <vector>

namespace
{

/* Thread-local error storage — safe across FFI boundary. */
thread_local std::string last_error;

void set_last_error(const std::string& msg) { last_error = msg; }

void clear_last_error() { last_error.clear(); }

/* Render a json::Object to a std::string.
 * The json_renderer.hpp header is not installed with osrm-backend,
 * so we provide our own recursive renderer. */
std::string render_json(const osrm::util::json::Value& value);

std::string render_json_object(const osrm::util::json::Object& obj)
{
    std::ostringstream out;
    out << '{';
    bool first = true;
    for (const auto& [k, v] : obj.values)
    {
        if (!first) out << ',';
        first = false;
        out << '"';
        for (char c : k)
        {
            if (c == '"' || c == '\\') out << '\\';
            out << c;
        }
        out << "\":";
        out << render_json(v);
    }
    out << '}';
    return out.str();
}

std::string render_json_array(const osrm::util::json::Array& arr)
{
    std::ostringstream out;
    out << '[';
    bool first = true;
    for (const auto& v : arr.values)
    {
        if (!first) out << ',';
        first = false;
        out << render_json(v);
    }
    out << ']';
    return out.str();
}

std::string escape_json_string(const std::string& s)
{
    std::string out;
    out.reserve(s.size() + 4);
    for (char c : s)
    {
        switch (c)
        {
        case '"':  out += "\\\""; break;
        case '\\': out += "\\\\"; break;
        case '\b': out += "\\b";  break;
        case '\f': out += "\\f";  break;
        case '\n': out += "\\n";  break;
        case '\r': out += "\\r";  break;
        case '\t': out += "\\t";  break;
        default:
            if (static_cast<unsigned char>(c) < 0x20)
            {
                char buf[8];
                std::snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned char>(c));
                out += buf;
            }
            else
            {
                out += c;
            }
            break;
        }
    }
    return out;
}

std::string render_json(const osrm::util::json::Value& value)
{
    using namespace osrm::util::json;
    return std::visit([](auto&& arg) -> std::string
    {
        using T = std::decay_t<decltype(arg)>;
        if constexpr (std::is_same_v<T, String>)
        {
            return "\"" + escape_json_string(arg.value) + "\"";
        }
        else if constexpr (std::is_same_v<T, Number>)
        {
            if (!std::isfinite(arg.value))
                return "null";
            std::ostringstream out;
            constexpr auto max_exact_int = static_cast<double>(1ULL << std::numeric_limits<double>::digits);
            if (arg.value >= 0.0 && arg.value <= max_exact_int && std::trunc(arg.value) == arg.value)
            {
                out << static_cast<std::uint64_t>(arg.value);
            }
            else
            {
                out << arg.value;
            }
            return out.str();
        }
        else if constexpr (std::is_same_v<T, Object>)
        {
            return render_json_object(arg);
        }
        else if constexpr (std::is_same_v<T, Array>)
        {
            return render_json_array(arg);
        }
        else if constexpr (std::is_same_v<T, True>)
        {
            return "true";
        }
        else if constexpr (std::is_same_v<T, False>)
        {
            return "false";
        }
        else /* Null */
        {
            return "null";
        }
    }, value);
}

/* Translate the C config struct to osrm::EngineConfig. */
osrm::engine::EngineConfig translate_config(const SharposrmEngineConfig* config)
{
    osrm::engine::EngineConfig cfg;

    /* Storage config path — the base .osrm path used to locate all data files.
     * Pass the disabled feature datasets so StorageConfig can exclude the corresponding
     * data files from its required files list. */
    if (config->storage_config_path && config->storage_config_path[0] != '\0')
    {
        std::vector<osrm::storage::FeatureDataset> disabled_datasets;
        if (config->disable_feature_datasets & SHARPOSRM_FEATURE_ROUTE_STEPS)
            disabled_datasets.push_back(osrm::storage::FeatureDataset::ROUTE_STEPS);
        if (config->disable_feature_datasets & SHARPOSRM_FEATURE_ROUTE_GEOMETRY)
            disabled_datasets.push_back(osrm::storage::FeatureDataset::ROUTE_GEOMETRY);

        cfg.storage_config = osrm::storage::StorageConfig(
            std::filesystem::path(config->storage_config_path), disabled_datasets);
    }

    /* Build the disabled datasets list for EngineConfig too */
    cfg.disable_feature_dataset.clear();
    if (config->disable_feature_datasets & SHARPOSRM_FEATURE_ROUTE_STEPS)
    {
        cfg.disable_feature_dataset.push_back(osrm::storage::FeatureDataset::ROUTE_STEPS);
    }
    if (config->disable_feature_datasets & SHARPOSRM_FEATURE_ROUTE_GEOMETRY)
    {
        cfg.disable_feature_dataset.push_back(osrm::storage::FeatureDataset::ROUTE_GEOMETRY);
    }
    switch (config->algorithm)
    {
    case SHARPOSRM_ALGORITHM_MLD:
        cfg.algorithm = osrm::engine::EngineConfig::Algorithm::MLD;
        break;
    case SHARPOSRM_ALGORITHM_CH:
    default:
        cfg.algorithm = osrm::engine::EngineConfig::Algorithm::CH;
        break;
    }

    /* Boolean flags */
    cfg.use_shared_memory = (config->use_shared_memory != 0);
    cfg.use_mmap = (config->use_mmap != 0);

    /* Service limits */
    cfg.max_locations_trip = config->max_locations_trip;
    cfg.max_locations_viaroute = config->max_locations_viaroute;
    cfg.max_locations_distance_table = config->max_locations_distance_table;
    cfg.max_locations_map_matching = config->max_locations_map_matching;
    cfg.max_radius_map_matching = config->max_radius_map_matching;
    cfg.max_results_nearest = config->max_results_nearest;
    cfg.default_radius = config->default_radius;
    cfg.max_alternatives = config->max_alternatives;

    /* Optional strings */
    if (config->memory_file && config->memory_file[0] != '\0')
    {
        cfg.memory_file = std::filesystem::path(config->memory_file);
    }
    if (config->dataset_name && config->dataset_name[0] != '\0')
    {
        cfg.dataset_name = std::string(config->dataset_name);
    }

    return cfg;
}

} /* anonymous namespace */

/* ── Route parameter translation ──────────────────────────────────────── */

namespace
{

/* Translate bearings from interleaved short array to OSRM's vector<optional<Bearing>>.
 * Input: bearings = [bearing0, range0, bearing1, range1, ...], 2 shorts per coordinate.
 * bearing_count = number of coordinates (NOT number of shorts).
 * Shorts of {-1, -1} mean "no bearing for this coordinate". */
std::vector<std::optional<osrm::engine::Bearing>> translate_bearings(const short* bearings, int bearing_count)
{
    std::vector<std::optional<osrm::engine::Bearing>> result;
    if (!bearings || bearing_count <= 0)
        return result;

    result.reserve(static_cast<size_t>(bearing_count));
    for (int i = 0; i < bearing_count; ++i)
    {
        short b = bearings[i * 2];
        short r = bearings[i * 2 + 1];
        if (b < 0 && r < 0)
        {
            result.emplace_back(std::nullopt);
        }
        else
        {
            result.emplace_back(osrm::engine::Bearing{b, r});
        }
    }
    return result;
}

/* Translate hints from array of nullable C strings to OSRM's vector<optional<Hint>>.
 * Input: hints[i] can be nullptr for coordinates without a hint.
 * Hints are base64-encoded strings that OSRM decodes via Hint::FromBase64(). */
std::vector<std::optional<osrm::engine::Hint>> translate_hints(const char** hints, int hint_count)
{
    std::vector<std::optional<osrm::engine::Hint>> result;
    if (!hints || hint_count <= 0)
        return result;

    result.reserve(static_cast<size_t>(hint_count));
    for (int i = 0; i < hint_count; ++i)
    {
        if (hints[i] && hints[i][0] != '\0')
        {
            result.emplace_back(osrm::engine::Hint::FromBase64(hints[i]));
        }
        else
        {
            result.emplace_back(std::nullopt);
        }
    }
    return result;
}

/* Translate approaches from byte array to OSRM's vector<optional<Approach>>.
 * Input: approaches = byte array, one byte per coordinate.
 * Signed char value of -1 (0xFF) means "not set" for that coordinate.
 * 0 = CURB, 1 = UNRESTRICTED, 2 = OPPOSITE. */
std::vector<std::optional<osrm::engine::Approach>> translate_approaches(const char* approaches, int approach_count)
{
    std::vector<std::optional<osrm::engine::Approach>> result;
    if (!approaches || approach_count <= 0)
        return result;

    result.reserve(static_cast<size_t>(approach_count));
    for (int i = 0; i < approach_count; ++i)
    {
        signed char val = static_cast<signed char>(approaches[i]);
        if (val == -1)
        {
            result.emplace_back(std::nullopt);
        }
        else
        {
            result.emplace_back(static_cast<osrm::engine::Approach>(val));
        }
    }
    return result;
}

/* Translate exclude from C string array to vector<string>.
 * Input: array of road class name strings (e.g. "motorway", "ferry"). */
std::vector<std::string> translate_exclude(const char** exclude, int exclude_count)
{
    std::vector<std::string> result;
    if (!exclude || exclude_count <= 0)
        return result;

    result.reserve(static_cast<size_t>(exclude_count));
    for (int i = 0; i < exclude_count; ++i)
    {
        if (exclude[i])
            result.emplace_back(exclude[i]);
    }
    return result;
}

/* Translate snapping int to OSRM BaseParameters::SnappingType. */
osrm::engine::api::BaseParameters::SnappingType translate_snapping(int snapping)
{
    if (snapping == SHARPOSRM_SNAPPING_ANY)
        return osrm::engine::api::BaseParameters::SnappingType::Any;
    return osrm::engine::api::BaseParameters::SnappingType::Default;
}

osrm::engine::api::RouteParameters translate_route_params(const SharposrmRouteParams* params)
{
    using namespace osrm::engine::api;

    /* Build coordinates from parallel lon/lat arrays */
    std::vector<osrm::util::Coordinate> coords;
    coords.reserve(static_cast<size_t>(params->coordinate_count));
    for (int i = 0; i < params->coordinate_count; ++i)
    {
        coords.emplace_back(
            osrm::util::FloatLongitude{params->longitudes[i]},
            osrm::util::FloatLatitude{params->latitudes[i]});
    }

    /* Optional radiuses */
    std::vector<std::optional<double>> radiuses;
    if (params->radiuses && params->radius_count > 0)
    {
        radiuses.reserve(static_cast<size_t>(params->radius_count));
        for (int i = 0; i < params->radius_count; ++i)
        {
            radiuses.emplace_back(params->radiuses[i]);
        }
    }

    /* Translate geometries type */
    RouteParameters::GeometriesType geom = RouteParameters::GeometriesType::Polyline;
    switch (params->geometries_type)
    {
    case SHARPOSRM_GEOMETRIES_POLYLINE6:
        geom = RouteParameters::GeometriesType::Polyline6;
        break;
    case SHARPOSRM_GEOMETRIES_GEOJSON:
        geom = RouteParameters::GeometriesType::GeoJSON;
        break;
    case SHARPOSRM_GEOMETRIES_POLYLINE:
    default:
        geom = RouteParameters::GeometriesType::Polyline;
        break;
    }

    /* Translate overview type */
    RouteParameters::OverviewType overview = RouteParameters::OverviewType::Simplified;
    switch (params->overview_type)
    {
    case SHARPOSRM_OVERVIEW_FULL:
        overview = RouteParameters::OverviewType::Full;
        break;
    case SHARPOSRM_OVERVIEW_FALSE:
        overview = RouteParameters::OverviewType::False;
        break;
    case SHARPOSRM_OVERVIEW_SIMPLIFIED:
    default:
        overview = RouteParameters::OverviewType::Simplified;
        break;
    }

    /* Translate annotations bitmask */
    RouteParameters::AnnotationsType ann_type = RouteParameters::AnnotationsType::None;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DURATION)
        ann_type |= RouteParameters::AnnotationsType::Duration;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_NODES)
        ann_type |= RouteParameters::AnnotationsType::Nodes;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DISTANCE)
        ann_type |= RouteParameters::AnnotationsType::Distance;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_WEIGHT)
        ann_type |= RouteParameters::AnnotationsType::Weight;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DATASOURCES)
        ann_type |= RouteParameters::AnnotationsType::Datasources;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_SPEED)
        ann_type |= RouteParameters::AnnotationsType::Speed;

    /* Translate continue_straight (-1 = not set) */
    std::optional<bool> continue_straight;
    if (params->continue_straight >= 0)
    {
        continue_straight = (params->continue_straight != 0);
    }

    RouteParameters route_params{
        params->steps != 0,
        params->alternatives != 0,
        ann_type,
        geom,
        overview,
        continue_straight,
        std::move(coords),
    };

    /* Set number_of_alternatives separately (constructor sets it based on alternatives bool) */
    route_params.number_of_alternatives = params->number_of_alternatives;

    /* Annotations bool (true if any annotation type is set) */
    route_params.annotations = (ann_type != RouteParameters::AnnotationsType::None);
    route_params.annotations_type = ann_type;

    /* Radiuses */
    route_params.radiuses = std::move(radiuses);

    /* Hints and skip_waypoints */
    route_params.generate_hints = (params->generate_hints != 0);
    route_params.skip_waypoints = (params->skip_waypoints != 0);

    /* Bearings */
    route_params.bearings = translate_bearings(params->bearings, params->bearing_count);

    /* Hints (input for faster snapping) */
    route_params.hints = translate_hints(params->hints, params->hint_count);

    /* Approaches, exclude, and snapping */
    route_params.approaches = translate_approaches(params->approaches, params->approach_count);
    route_params.exclude = translate_exclude(params->exclude, params->exclude_count);
    route_params.snapping = translate_snapping(params->snapping);

    /* Waypoints — indices of actual stop coordinates */
    if (params->waypoints && params->waypoint_count > 0)
    {
        route_params.waypoints.reserve(static_cast<size_t>(params->waypoint_count));
        for (int i = 0; i < params->waypoint_count; ++i)
        {
            route_params.waypoints.push_back(params->waypoints[i]);
        }
    }

    return route_params;
}

} /* anonymous namespace */

/* ── Public API implementation ────────────────────────────────────────── */

SHARPOSRM_EXPORT void* sharposrm_create(const SharposrmEngineConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_create: config pointer is null");
        return nullptr;
    }

    try
    {
        osrm::engine::EngineConfig cfg = translate_config(config);

        if (!cfg.IsValid())
        {
            set_last_error("sharposrm_create: invalid engine configuration (check storage_config_path and algorithm)");
            return nullptr;
        }

        osrm::OSRM* engine = new osrm::OSRM(cfg);
        return static_cast<void*>(engine);
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_create: ") + e.what());
        return nullptr;
    }
    catch (...)
    {
        set_last_error("sharposrm_create: unknown exception during OSRM construction");
        return nullptr;
    }
}

SHARPOSRM_EXPORT void sharposrm_destroy(void* engine)
{
    if (!engine)
    {
        return;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        delete osrm_ptr;
    }
    catch (...)
    {
        /* Destroy must not throw across FFI. Swallow silently. */
    }
}

SHARPOSRM_EXPORT int sharposrm_config_is_valid(const SharposrmEngineConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_config_is_valid: config pointer is null");
        return 0;
    }

    try
    {
        osrm::engine::EngineConfig cfg = translate_config(config);
        return cfg.IsValid() ? 1 : 0;
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_config_is_valid: ") + e.what());
        return 0;
    }
    catch (...)
    {
        set_last_error("sharposrm_config_is_valid: unknown exception");
        return 0;
    }
}

SHARPOSRM_EXPORT const char* sharposrm_get_last_error(void)
{
    if (last_error.empty())
    {
        return nullptr;
    }
    return last_error.c_str();
}

SHARPOSRM_EXPORT void sharposrm_free_string(char* s)
{
    /* Currently, all returned strings point into thread_local storage,
     * so they must not be freed. This function exists for future-proofing
     * in case we allocate strings on the heap. */
    (void)s;
}

/* ── Route service ────────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_route(void* engine,
                                     const SharposrmRouteParams* params,
                                     char** result_json)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_route: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_route: params pointer is null");
        return 1;
    }
    if (!result_json)
    {
        set_last_error("sharposrm_route: result_json pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        osrm::engine::api::RouteParameters rp = translate_route_params(params);

        osrm::json::Object json_result;
        osrm::Status status = osrm_ptr->Route(rp, json_result);

        /* Render JSON to string — OSRM populates error info even on Status::Error */
        std::string json_str = render_json(json_result);

        /* Heap-allocate a copy for the caller to own */
        *result_json = strdup(json_str.c_str());
        if (!*result_json)
        {
            set_last_error("sharposrm_route: failed to allocate memory for JSON result");
            return 1;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_json = nullptr;
        set_last_error(std::string("sharposrm_route: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_json = nullptr;
        set_last_error("sharposrm_route: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT void sharposrm_free_result(char* result)
{
    std::free(result);
}

/* ── Table parameter translation ──────────────────────────────────────── */

namespace
{

osrm::engine::api::TableParameters translate_table_params(const SharposrmTableParams* params)
{
    using namespace osrm::engine::api;

    /* Build coordinates from parallel lon/lat arrays */
    std::vector<osrm::util::Coordinate> coords;
    coords.reserve(static_cast<size_t>(params->coordinate_count));
    for (int i = 0; i < params->coordinate_count; ++i)
    {
        coords.emplace_back(
            osrm::util::FloatLongitude{params->longitudes[i]},
            osrm::util::FloatLatitude{params->latitudes[i]});
    }

    /* Optional radiuses */
    std::vector<std::optional<double>> radiuses;
    if (params->radiuses && params->radius_count > 0)
    {
        radiuses.reserve(static_cast<size_t>(params->radius_count));
        for (int i = 0; i < params->radius_count; ++i)
        {
            radiuses.emplace_back(params->radiuses[i]);
        }
    }

    /* Sources */
    std::vector<std::size_t> sources;
    if (params->sources && params->source_count > 0)
    {
        sources.reserve(static_cast<size_t>(params->source_count));
        for (int i = 0; i < params->source_count; ++i)
        {
            sources.push_back(params->sources[i]);
        }
    }

    /* Destinations */
    std::vector<std::size_t> destinations;
    if (params->destinations && params->destination_count > 0)
    {
        destinations.reserve(static_cast<size_t>(params->destination_count));
        for (int i = 0; i < params->destination_count; ++i)
        {
            destinations.push_back(params->destinations[i]);
        }
    }

    /* Translate annotations bitmask */
    TableParameters::AnnotationsType ann_type = TableParameters::AnnotationsType::None;
    if (params->annotations_type & SHARPOSRM_TABLE_ANNOTATIONS_DURATION)
        ann_type |= TableParameters::AnnotationsType::Duration;
    if (params->annotations_type & SHARPOSRM_TABLE_ANNOTATIONS_DISTANCE)
        ann_type |= TableParameters::AnnotationsType::Distance;

    /* Translate fallback coordinate type */
    TableParameters::FallbackCoordinateType fb_coord = TableParameters::FallbackCoordinateType::Input;
    if (params->fallback_coordinate_type == SHARPOSRM_FALLBACK_COORDINATE_SNAPPED)
        fb_coord = TableParameters::FallbackCoordinateType::Snapped;

    TableParameters tp;

    /* Base parameters */
    tp.coordinates = std::move(coords);
    tp.radiuses = std::move(radiuses);
    tp.generate_hints = (params->generate_hints != 0);
    tp.skip_waypoints = (params->skip_waypoints != 0);
    tp.bearings = translate_bearings(params->bearings, params->bearing_count);
    tp.hints = translate_hints(params->hints, params->hint_count);

    /* Approaches, exclude, and snapping */
    tp.approaches = translate_approaches(params->approaches, params->approach_count);
    tp.exclude = translate_exclude(params->exclude, params->exclude_count);
    tp.snapping = translate_snapping(params->snapping);

    /* Table-specific parameters */
    tp.sources = std::move(sources);
    tp.destinations = std::move(destinations);
    tp.annotations = ann_type;
    tp.fallback_speed = params->fallback_speed;
    tp.fallback_coordinate_type = fb_coord;
    tp.scale_factor = params->scale_factor;

    return tp;
}

osrm::engine::api::NearestParameters translate_nearest_params(const SharposrmNearestParams* params)
{
    using namespace osrm::engine::api;

    /* Build coordinates from parallel lon/lat arrays */
    std::vector<osrm::util::Coordinate> coords;
    coords.reserve(static_cast<size_t>(params->coordinate_count));
    for (int i = 0; i < params->coordinate_count; ++i)
    {
        coords.emplace_back(
            osrm::util::FloatLongitude{params->longitudes[i]},
            osrm::util::FloatLatitude{params->latitudes[i]});
    }

    /* Optional radiuses */
    std::vector<std::optional<double>> radiuses;
    if (params->radiuses && params->radius_count > 0)
    {
        radiuses.reserve(static_cast<size_t>(params->radius_count));
        for (int i = 0; i < params->radius_count; ++i)
        {
            radiuses.emplace_back(params->radiuses[i]);
        }
    }

    NearestParameters np;

    /* Base parameters */
    np.coordinates = std::move(coords);
    np.radiuses = std::move(radiuses);
    np.generate_hints = (params->generate_hints != 0);
    np.skip_waypoints = (params->skip_waypoints != 0);
    np.bearings = translate_bearings(params->bearings, params->bearing_count);
    np.hints = translate_hints(params->hints, params->hint_count);

    /* Approaches, exclude, and snapping */
    np.approaches = translate_approaches(params->approaches, params->approach_count);
    np.exclude = translate_exclude(params->exclude, params->exclude_count);
    np.snapping = translate_snapping(params->snapping);

    /* Nearest-specific */
    np.number_of_results = params->number_of_results;

    return np;
}

osrm::engine::api::TripParameters translate_trip_params(const SharposrmTripParams* params)
{
    using namespace osrm::engine::api;

    /* Build coordinates from parallel lon/lat arrays */
    std::vector<osrm::util::Coordinate> coords;
    coords.reserve(static_cast<size_t>(params->coordinate_count));
    for (int i = 0; i < params->coordinate_count; ++i)
    {
        coords.emplace_back(
            osrm::util::FloatLongitude{params->longitudes[i]},
            osrm::util::FloatLatitude{params->latitudes[i]});
    }

    /* Optional radiuses */
    std::vector<std::optional<double>> radiuses;
    if (params->radiuses && params->radius_count > 0)
    {
        radiuses.reserve(static_cast<size_t>(params->radius_count));
        for (int i = 0; i < params->radius_count; ++i)
        {
            radiuses.emplace_back(params->radiuses[i]);
        }
    }

    /* Translate geometries type */
    RouteParameters::GeometriesType geom = RouteParameters::GeometriesType::Polyline;
    switch (params->geometries_type)
    {
    case SHARPOSRM_GEOMETRIES_POLYLINE6:
        geom = RouteParameters::GeometriesType::Polyline6;
        break;
    case SHARPOSRM_GEOMETRIES_GEOJSON:
        geom = RouteParameters::GeometriesType::GeoJSON;
        break;
    default:
        break;
    }

    /* Translate overview type */
    RouteParameters::OverviewType overview = RouteParameters::OverviewType::Simplified;
    switch (params->overview_type)
    {
    case SHARPOSRM_OVERVIEW_FULL:
        overview = RouteParameters::OverviewType::Full;
        break;
    case SHARPOSRM_OVERVIEW_FALSE:
        overview = RouteParameters::OverviewType::False;
        break;
    default:
        break;
    }

    /* Translate annotations bitmask */
    RouteParameters::AnnotationsType ann_type = RouteParameters::AnnotationsType::None;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DURATION)
        ann_type |= RouteParameters::AnnotationsType::Duration;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_NODES)
        ann_type |= RouteParameters::AnnotationsType::Nodes;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DISTANCE)
        ann_type |= RouteParameters::AnnotationsType::Distance;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_WEIGHT)
        ann_type |= RouteParameters::AnnotationsType::Weight;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DATASOURCES)
        ann_type |= RouteParameters::AnnotationsType::Datasources;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_SPEED)
        ann_type |= RouteParameters::AnnotationsType::Speed;

    /* Translate continue_straight (-1 = not set) */
    std::optional<bool> continue_straight;
    if (params->continue_straight >= 0)
    {
        continue_straight = (params->continue_straight != 0);
    }

    /* Translate source type */
    TripParameters::SourceType src_type = TripParameters::SourceType::Any;
    if (params->source_type == SHARPOSRM_SOURCE_FIRST)
        src_type = TripParameters::SourceType::First;

    /* Translate destination type */
    TripParameters::DestinationType dst_type = TripParameters::DestinationType::Any;
    if (params->destination_type == SHARPOSRM_DESTINATION_LAST)
        dst_type = TripParameters::DestinationType::Last;

    TripParameters tp;

    /* BaseParameters */
    tp.coordinates = std::move(coords);
    tp.radiuses = std::move(radiuses);
    tp.generate_hints = (params->generate_hints != 0);
    tp.skip_waypoints = (params->skip_waypoints != 0);
    tp.bearings = translate_bearings(params->bearings, params->bearing_count);
    tp.hints = translate_hints(params->hints, params->hint_count);

    /* Approaches, exclude, and snapping */
    tp.approaches = translate_approaches(params->approaches, params->approach_count);
    tp.exclude = translate_exclude(params->exclude, params->exclude_count);
    tp.snapping = translate_snapping(params->snapping);

    /* RouteParameters */
    tp.steps = (params->steps != 0);
    tp.alternatives = (params->alternatives != 0);
    tp.number_of_alternatives = params->number_of_alternatives;
    tp.annotations = (ann_type != RouteParameters::AnnotationsType::None);
    tp.annotations_type = ann_type;
    tp.geometries = geom;
    tp.overview = overview;
    tp.continue_straight = continue_straight;

    /* Trip-specific */
    tp.source = src_type;
    tp.destination = dst_type;
    tp.roundtrip = (params->roundtrip != 0);

    return tp;
}

osrm::engine::api::MatchParameters translate_match_params(const SharposrmMatchParams* params)
{
    using namespace osrm::engine::api;

    /* Build coordinates from parallel lon/lat arrays */
    std::vector<osrm::util::Coordinate> coords;
    coords.reserve(static_cast<size_t>(params->coordinate_count));
    for (int i = 0; i < params->coordinate_count; ++i)
    {
        coords.emplace_back(
            osrm::util::FloatLongitude{params->longitudes[i]},
            osrm::util::FloatLatitude{params->latitudes[i]});
    }

    /* Optional radiuses */
    std::vector<std::optional<double>> radiuses;
    if (params->radiuses && params->radius_count > 0)
    {
        radiuses.reserve(static_cast<size_t>(params->radius_count));
        for (int i = 0; i < params->radius_count; ++i)
        {
            radiuses.emplace_back(params->radiuses[i]);
        }
    }

    /* Optional timestamps */
    std::vector<unsigned> timestamps;
    if (params->timestamps && params->timestamp_count > 0)
    {
        timestamps.reserve(static_cast<size_t>(params->timestamp_count));
        for (int i = 0; i < params->timestamp_count; ++i)
        {
            timestamps.push_back(params->timestamps[i]);
        }
    }

    /* Translate geometries type */
    RouteParameters::GeometriesType geom = RouteParameters::GeometriesType::Polyline;
    switch (params->geometries_type)
    {
    case SHARPOSRM_GEOMETRIES_POLYLINE6:
        geom = RouteParameters::GeometriesType::Polyline6;
        break;
    case SHARPOSRM_GEOMETRIES_GEOJSON:
        geom = RouteParameters::GeometriesType::GeoJSON;
        break;
    default:
        break;
    }

    /* Translate overview type */
    RouteParameters::OverviewType overview = RouteParameters::OverviewType::Simplified;
    switch (params->overview_type)
    {
    case SHARPOSRM_OVERVIEW_FULL:
        overview = RouteParameters::OverviewType::Full;
        break;
    case SHARPOSRM_OVERVIEW_FALSE:
        overview = RouteParameters::OverviewType::False;
        break;
    default:
        break;
    }

    /* Translate annotations bitmask */
    RouteParameters::AnnotationsType ann_type = RouteParameters::AnnotationsType::None;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DURATION)
        ann_type |= RouteParameters::AnnotationsType::Duration;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_NODES)
        ann_type |= RouteParameters::AnnotationsType::Nodes;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DISTANCE)
        ann_type |= RouteParameters::AnnotationsType::Distance;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_WEIGHT)
        ann_type |= RouteParameters::AnnotationsType::Weight;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_DATASOURCES)
        ann_type |= RouteParameters::AnnotationsType::Datasources;
    if (params->annotations_type & SHARPOSRM_ANNOTATIONS_SPEED)
        ann_type |= RouteParameters::AnnotationsType::Speed;

    /* Translate continue_straight (-1 = not set) */
    std::optional<bool> continue_straight;
    if (params->continue_straight >= 0)
    {
        continue_straight = (params->continue_straight != 0);
    }

    /* Translate gaps type */
    MatchParameters::GapsType gaps = MatchParameters::GapsType::Split;
    if (params->gaps == SHARPOSRM_GAPS_IGNORE)
        gaps = MatchParameters::GapsType::Ignore;

    MatchParameters mp;

    /* BaseParameters */
    mp.coordinates = std::move(coords);
    mp.radiuses = std::move(radiuses);
    mp.generate_hints = (params->generate_hints != 0);
    mp.skip_waypoints = (params->skip_waypoints != 0);
    mp.bearings = translate_bearings(params->bearings, params->bearing_count);
    mp.hints = translate_hints(params->hints, params->hint_count);

    /* Approaches, exclude, and snapping */
    mp.approaches = translate_approaches(params->approaches, params->approach_count);
    mp.exclude = translate_exclude(params->exclude, params->exclude_count);
    mp.snapping = translate_snapping(params->snapping);

    /* RouteParameters */
    mp.steps = (params->steps != 0);
    mp.alternatives = (params->alternatives != 0);
    mp.number_of_alternatives = params->number_of_alternatives;
    mp.annotations = (ann_type != RouteParameters::AnnotationsType::None);
    mp.annotations_type = ann_type;
    mp.geometries = geom;
    mp.overview = overview;
    mp.continue_straight = continue_straight;

    /* Match-specific */
    mp.timestamps = std::move(timestamps);
    mp.gaps = gaps;
    mp.tidy = (params->tidy != 0);

    /* Waypoints — indices of actual stop coordinates */
    if (params->waypoints && params->waypoint_count > 0)
    {
        mp.waypoints.reserve(static_cast<size_t>(params->waypoint_count));
        for (int i = 0; i < params->waypoint_count; ++i)
        {
            mp.waypoints.push_back(params->waypoints[i]);
        }
    }

    return mp;
}

/* ── Pipeline config translation helpers ───────────────────────────────── */

osrm::extractor::ExtractorConfig translate_extractor_config(const SharposrmExtractorConfig* config)
{
    osrm::extractor::ExtractorConfig cfg;

    if (config->input_path && config->input_path[0] != '\0')
        cfg.input_path = std::filesystem::path(config->input_path);
    if (config->profile_path && config->profile_path[0] != '\0')
        cfg.profile_path = std::filesystem::path(config->profile_path);

    cfg.requested_num_threads = config->requested_num_threads;
    cfg.small_component_size = config->small_component_size;
    cfg.use_metadata = (config->use_metadata != 0);
    cfg.parse_conditionals = (config->parse_conditionals != 0);
    cfg.use_locations_cache = (config->use_locations_cache != 0);
    cfg.dump_nbg_graph = (config->dump_nbg_graph != 0);

    /* UseDefaultOutputNames: derive from output_path if set, else from input_path */
    if (config->output_path && config->output_path[0] != '\0')
        cfg.UseDefaultOutputNames(std::filesystem::path(config->output_path));
    else
        cfg.UseDefaultOutputNames(cfg.input_path);

    return cfg;
}

osrm::partitioner::PartitionerConfig translate_partitioner_config(const SharposrmPartitionerConfig* config)
{
    osrm::partitioner::PartitionerConfig cfg;

    cfg.requested_num_threads = config->requested_num_threads;
    cfg.balance = config->balance;
    cfg.boundary_factor = config->boundary_factor;
    cfg.num_optimizing_cuts = config->num_optimizing_cuts;
    cfg.small_component_size = config->small_component_size;

    /* max_cell_sizes: fixed 4-element array */
    cfg.max_cell_sizes.clear();
    for (int i = 0; i < 4; ++i)
        cfg.max_cell_sizes.push_back(config->max_cell_sizes[i]);

    if (config->base_path && config->base_path[0] != '\0')
        cfg.UseDefaultOutputNames(std::filesystem::path(config->base_path));

    return cfg;
}

osrm::customizer::CustomizationConfig translate_customizer_config(const SharposrmCustomizerConfig* config)
{
    osrm::customizer::CustomizationConfig cfg;

    cfg.requested_num_threads = config->requested_num_threads;

    /* UpdaterConfig fields */
    if (config->segment_speed_lookup_paths && config->segment_speed_lookup_count > 0)
    {
        cfg.updater_config.segment_speed_lookup_paths.clear();
        for (int i = 0; i < config->segment_speed_lookup_count; ++i)
            cfg.updater_config.segment_speed_lookup_paths.emplace_back(config->segment_speed_lookup_paths[i]);
    }
    if (config->turn_penalty_lookup_paths && config->turn_penalty_lookup_count > 0)
    {
        cfg.updater_config.turn_penalty_lookup_paths.clear();
        for (int i = 0; i < config->turn_penalty_lookup_count; ++i)
            cfg.updater_config.turn_penalty_lookup_paths.emplace_back(config->turn_penalty_lookup_paths[i]);
    }

    if (config->base_path && config->base_path[0] != '\0')
        cfg.UseDefaultOutputNames(std::filesystem::path(config->base_path));

    return cfg;
}

osrm::contractor::ContractorConfig translate_contractor_config(const SharposrmContractorConfig* config)
{
    osrm::contractor::ContractorConfig cfg;

    cfg.requested_num_threads = config->requested_num_threads;

    /* UpdaterConfig fields */
    if (config->segment_speed_lookup_paths && config->segment_speed_lookup_count > 0)
    {
        cfg.updater_config.segment_speed_lookup_paths.clear();
        for (int i = 0; i < config->segment_speed_lookup_count; ++i)
            cfg.updater_config.segment_speed_lookup_paths.emplace_back(config->segment_speed_lookup_paths[i]);
    }
    if (config->turn_penalty_lookup_paths && config->turn_penalty_lookup_count > 0)
    {
        cfg.updater_config.turn_penalty_lookup_paths.clear();
        for (int i = 0; i < config->turn_penalty_lookup_count; ++i)
            cfg.updater_config.turn_penalty_lookup_paths.emplace_back(config->turn_penalty_lookup_paths[i]);
    }

    if (config->base_path && config->base_path[0] != '\0')
        cfg.UseDefaultOutputNames(std::filesystem::path(config->base_path));

    return cfg;
}

/* Helper: call an OSRM service with flatbuffer output format.
 *
 * Takes a lambda that calls the appropriate OSRM service method with a ResultT.
 * Sets format=FLATBUFFERS on params, calls the service, extracts raw bytes from
 * the FlatBufferBuilder variant, and malloc+memcpy's them to a caller-owned buffer.
 */
template <typename ParamsT, typename ServiceFn>
int call_service_fb(const char* service_name,
                    void* engine,
                    ParamsT& params,
                    ServiceFn service_fn,
                    char** result_data,
                    int* result_length)
{
    /* Set flatbuffer output format */
    params.format = osrm::engine::api::BaseParameters::OutputFormatType::FLATBUFFERS;

    osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);

    /* Initialize ResultT as a FlatBufferBuilder */
    osrm::engine::api::ResultT result = flatbuffers::FlatBufferBuilder();

    osrm::Status status = service_fn(osrm_ptr, params, result);

    /* Extract the FlatBufferBuilder from the variant */
    auto& builder = std::get<flatbuffers::FlatBufferBuilder>(result);

    auto buf_size = builder.GetSize();
    const uint8_t* buf_ptr = builder.GetBufferPointer();

    /* Allocate caller-owned buffer and copy bytes */
    *result_data = static_cast<char*>(std::malloc(buf_size));
    if (!*result_data)
    {
        set_last_error(std::string(service_name) + ": failed to allocate memory for flatbuffer result");
        return 1;
    }
    std::memcpy(*result_data, buf_ptr, buf_size);
    *result_length = static_cast<int>(buf_size);

    return (status == osrm::Status::Ok) ? 0 : 1;
}

} /* anonymous namespace */

/* ── Table service ────────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_table(void* engine,
                                     const SharposrmTableParams* params,
                                     char** result_json)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_table: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_table: params pointer is null");
        return 1;
    }
    if (!result_json)
    {
        set_last_error("sharposrm_table: result_json pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        osrm::engine::api::TableParameters tp = translate_table_params(params);

        osrm::json::Object json_result;
        osrm::Status status = osrm_ptr->Table(tp, json_result);

        std::string json_str = render_json(json_result);
        *result_json = strdup(json_str.c_str());
        if (!*result_json)
        {
            set_last_error("sharposrm_table: failed to allocate memory for JSON result");
            return 1;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_json = nullptr;
        set_last_error(std::string("sharposrm_table: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_json = nullptr;
        set_last_error("sharposrm_table: unknown exception");
        return 1;
    }
}

/* ── Nearest service ──────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_nearest(void* engine,
                                       const SharposrmNearestParams* params,
                                       char** result_json)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_nearest: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_nearest: params pointer is null");
        return 1;
    }
    if (!result_json)
    {
        set_last_error("sharposrm_nearest: result_json pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        osrm::engine::api::NearestParameters np = translate_nearest_params(params);

        osrm::json::Object json_result;
        osrm::Status status = osrm_ptr->Nearest(np, json_result);

        std::string json_str = render_json(json_result);
        *result_json = strdup(json_str.c_str());
        if (!*result_json)
        {
            set_last_error("sharposrm_nearest: failed to allocate memory for JSON result");
            return 1;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_json = nullptr;
        set_last_error(std::string("sharposrm_nearest: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_json = nullptr;
        set_last_error("sharposrm_nearest: unknown exception");
        return 1;
    }
}

/* ── Trip service ─────────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_trip(void* engine,
                                    const SharposrmTripParams* params,
                                    char** result_json)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_trip: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_trip: params pointer is null");
        return 1;
    }
    if (!result_json)
    {
        set_last_error("sharposrm_trip: result_json pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        osrm::engine::api::TripParameters tp = translate_trip_params(params);

        osrm::json::Object json_result;
        osrm::Status status = osrm_ptr->Trip(tp, json_result);

        std::string json_str = render_json(json_result);
        *result_json = strdup(json_str.c_str());
        if (!*result_json)
        {
            set_last_error("sharposrm_trip: failed to allocate memory for JSON result");
            return 1;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_json = nullptr;
        set_last_error(std::string("sharposrm_trip: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_json = nullptr;
        set_last_error("sharposrm_trip: unknown exception");
        return 1;
    }
}

/* ── Match service ────────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_match(void* engine,
                                     const SharposrmMatchParams* params,
                                     char** result_json)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_match: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_match: params pointer is null");
        return 1;
    }
    if (!result_json)
    {
        set_last_error("sharposrm_match: result_json pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);
        osrm::engine::api::MatchParameters mp = translate_match_params(params);

        osrm::json::Object json_result;
        osrm::Status status = osrm_ptr->Match(mp, json_result);

        std::string json_str = render_json(json_result);
        *result_json = strdup(json_str.c_str());
        if (!*result_json)
        {
            set_last_error("sharposrm_match: failed to allocate memory for JSON result");
            return 1;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_json = nullptr;
        set_last_error(std::string("sharposrm_match: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_json = nullptr;
        set_last_error("sharposrm_match: unknown exception");
        return 1;
    }
}

/* ── Tile service ─────────────────────────────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_tile(void* engine,
                                    const SharposrmTileParams* params,
                                    char** result_data,
                                    int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_tile: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_tile: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_tile: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_tile: result_length pointer is null");
        return 1;
    }

    try
    {
        osrm::OSRM* osrm_ptr = static_cast<osrm::OSRM*>(engine);

        /* TileParameters is a simple struct — construct directly */
        osrm::engine::api::TileParameters tp{params->x, params->y, params->z};

        /* Tile() returns raw binary data (MVT), not JSON */
        std::string tile_data;
        osrm::Status status = osrm_ptr->Tile(tp, tile_data);

        if (status == osrm::Status::Ok)
        {
            /* Allocate a buffer and copy raw bytes */
            *result_length = static_cast<int>(tile_data.size());
            *result_data = static_cast<char*>(std::malloc(tile_data.size()));
            if (!*result_data)
            {
                set_last_error("sharposrm_tile: failed to allocate memory for tile data");
                return 1;
            }
            std::memcpy(*result_data, tile_data.data(), tile_data.size());
        }
        else
        {
            /* On error, return an empty buffer */
            *result_data = nullptr;
            *result_length = 0;
        }

        return (status == osrm::Status::Ok) ? 0 : 1;
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_tile: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_tile: unknown exception");
        return 1;
    }
}

/* ── Pipeline services: extract, partition, customize, contract ────────── */

SHARPOSRM_EXPORT int sharposrm_extract(const SharposrmExtractorConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_extract: config pointer is null");
        return 1;
    }

    try
    {
        osrm::extractor::ExtractorConfig cfg = translate_extractor_config(config);
        osrm::extract(cfg);
        return 0;
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_extract: ") + e.what());
        return 1;
    }
    catch (...)
    {
        set_last_error("sharposrm_extract: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_partition(const SharposrmPartitionerConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_partition: config pointer is null");
        return 1;
    }

    try
    {
        osrm::partitioner::PartitionerConfig cfg = translate_partitioner_config(config);
        osrm::partition(cfg);
        return 0;
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_partition: ") + e.what());
        return 1;
    }
    catch (...)
    {
        set_last_error("sharposrm_partition: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_customize(const SharposrmCustomizerConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_customize: config pointer is null");
        return 1;
    }

    try
    {
        osrm::customizer::CustomizationConfig cfg = translate_customizer_config(config);
        osrm::customize(cfg);
        return 0;
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_customize: ") + e.what());
        return 1;
    }
    catch (...)
    {
        set_last_error("sharposrm_customize: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_contract(const SharposrmContractorConfig* config)
{
    clear_last_error();

    if (!config)
    {
        set_last_error("sharposrm_contract: config pointer is null");
        return 1;
    }

    try
    {
        osrm::contractor::ContractorConfig cfg = translate_contractor_config(config);
        osrm::contract(cfg);
        return 0;
    }
    catch (const std::exception& e)
    {
        set_last_error(std::string("sharposrm_contract: ") + e.what());
        return 1;
    }
    catch (...)
    {
        set_last_error("sharposrm_contract: unknown exception");
        return 1;
    }
}

/* ── Flatbuffer-encoded output variants ───────────────────────────────── */

SHARPOSRM_EXPORT int sharposrm_route_fb(void* engine,
                                        const SharposrmRouteParams* params,
                                        char** result_data,
                                        int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_route_fb: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_route_fb: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_route_fb: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_route_fb: result_length pointer is null");
        return 1;
    }

    try
    {
        auto rp = translate_route_params(params);
        return call_service_fb("sharposrm_route_fb", engine, rp,
            [](osrm::OSRM* osrm, auto& p, auto& r)
            {
                return osrm->Route(p, r);
            },
            result_data, result_length);
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_route_fb: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_route_fb: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_table_fb(void* engine,
                                        const SharposrmTableParams* params,
                                        char** result_data,
                                        int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_table_fb: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_table_fb: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_table_fb: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_table_fb: result_length pointer is null");
        return 1;
    }

    try
    {
        auto tp = translate_table_params(params);
        return call_service_fb("sharposrm_table_fb", engine, tp,
            [](osrm::OSRM* osrm, auto& p, auto& r)
            {
                return osrm->Table(p, r);
            },
            result_data, result_length);
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_table_fb: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_table_fb: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_nearest_fb(void* engine,
                                          const SharposrmNearestParams* params,
                                          char** result_data,
                                          int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_nearest_fb: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_nearest_fb: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_nearest_fb: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_nearest_fb: result_length pointer is null");
        return 1;
    }

    try
    {
        auto np = translate_nearest_params(params);
        return call_service_fb("sharposrm_nearest_fb", engine, np,
            [](osrm::OSRM* osrm, auto& p, auto& r)
            {
                return osrm->Nearest(p, r);
            },
            result_data, result_length);
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_nearest_fb: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_nearest_fb: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_trip_fb(void* engine,
                                       const SharposrmTripParams* params,
                                       char** result_data,
                                       int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_trip_fb: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_trip_fb: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_trip_fb: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_trip_fb: result_length pointer is null");
        return 1;
    }

    try
    {
        auto tp = translate_trip_params(params);
        return call_service_fb("sharposrm_trip_fb", engine, tp,
            [](osrm::OSRM* osrm, auto& p, auto& r)
            {
                return osrm->Trip(p, r);
            },
            result_data, result_length);
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_trip_fb: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_trip_fb: unknown exception");
        return 1;
    }
}

SHARPOSRM_EXPORT int sharposrm_match_fb(void* engine,
                                        const SharposrmMatchParams* params,
                                        char** result_data,
                                        int* result_length)
{
    clear_last_error();

    if (!engine)
    {
        set_last_error("sharposrm_match_fb: engine pointer is null");
        return 1;
    }
    if (!params)
    {
        set_last_error("sharposrm_match_fb: params pointer is null");
        return 1;
    }
    if (!result_data)
    {
        set_last_error("sharposrm_match_fb: result_data pointer is null");
        return 1;
    }
    if (!result_length)
    {
        set_last_error("sharposrm_match_fb: result_length pointer is null");
        return 1;
    }

    try
    {
        auto mp = translate_match_params(params);
        return call_service_fb("sharposrm_match_fb", engine, mp,
            [](osrm::OSRM* osrm, auto& p, auto& r)
            {
                return osrm->Match(p, r);
            },
            result_data, result_length);
    }
    catch (const std::exception& e)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error(std::string("sharposrm_match_fb: ") + e.what());
        return 1;
    }
    catch (...)
    {
        *result_data = nullptr;
        *result_length = 0;
        set_last_error("sharposrm_match_fb: unknown exception");
        return 1;
    }
}
