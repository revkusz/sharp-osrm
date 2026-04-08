cmake_minimum_required(VERSION 3.18)
project(sharposrm LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRE ON)

# Add cmake/ to module path for FindLibOSRM
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

# Find dependencies
find_package(LibOSRM REQUIRED)
find_package(Threads REQUIRED)

# OSRM is a static library with transitive dependencies.
# We need to link Boost (thread, iostreams, date_time) and TBB.
find_package(Boost REQUIRED COMPONENTS thread iostreams date_time)
find_package(TBB REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(LUA REQUIRED IMPORTED_TARGET lua)

# Build the shared library
add_library(sharposrm SHARED
    src/Native/sharposrm_bridge.cpp
)

# OSRM include directories + transitive dependency includes
# Also add local osrm-backend/include for headers not shipped in Homebrew install
# (customizer/customizer_config.hpp, updater/updater_config.hpp)
target_include_directories(sharposrm PRIVATE
    ${LibOSRM_INCLUDE_DIRS}
    ${CMAKE_CURRENT_SOURCE_DIR}/osrm-backend/include
)

# Apply OSRM compiler defines (e.g. BOOST_SPIRIT_USE_PHOENIX_V3)
target_compile_definitions(sharposrm PRIVATE
    ${LibOSRM_CXXFLAGS}
)

# Link against OSRM, Boost, TBB, and threading
# Pipeline functions (extract, partition, customize, contract) live in separate static libraries
# that are not pulled in by the main libosrm.a. Link them explicitly from the OSRM lib directory.
# Derive the OSRM lib dir from the first library in LibOSRM_LINKABLE_LIBRARIES.
set(_OSRM_LIB_DIR "")
foreach(_lib ${LibOSRM_LINKABLE_LIBRARIES})
    get_filename_component(_lib_dir "${_lib}" DIRECTORY)
    if(_lib_dir)
        set(_OSRM_LIB_DIR "${_lib_dir}")
        break()
    endif()
endforeach()
if(NOT _OSRM_LIB_DIR)
    # Last resort: use pkg-config variable
    execute_process(
        COMMAND pkg-config --variable=libdir libosrm
        OUTPUT_VARIABLE _OSRM_LIB_DIR
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
endif()

set(OSRM_PIPELINE_LIBS
    "${_OSRM_LIB_DIR}/libosrm_extract.a"
    "${_OSRM_LIB_DIR}/libosrm_partition.a"
    "${_OSRM_LIB_DIR}/libosrm_customize.a"
    "${_OSRM_LIB_DIR}/libosrm_contract.a"
    "${_OSRM_LIB_DIR}/libosrm_guidance.a"
    "${_OSRM_LIB_DIR}/libosrm_update.a"
    "${_OSRM_LIB_DIR}/libosrm_store.a"
)

target_link_libraries(sharposrm PRIVATE
    ${LibOSRM_LINKABLE_LIBRARIES}
    ${OSRM_PIPELINE_LIBS}
    Boost::thread
    Boost::iostreams
    Boost::date_time
    TBB::tbb
    Threads::Threads
    PkgConfig::LUA                              # Lua 5.5 (needed by osrm_extract for profile scripts)
    bz2                                  # bzip2 (needed by libosmium via osrm_extract)
    expat                                # expat XML (needed by libosmium via osrm_extract)
    z                                    # zlib (needed by libosmium via osrm_extract)
)

# Hide all symbols by default; only SHARPOSRM_EXPORT ones are visible
set_target_properties(sharposrm PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    DEFINE_SYMBOL SHARPOSRM_BUILDING
    # macOS: set install_name for proper linking
    MACOSX_RPATH ON
)

# Install rules
include(GNUInstallDirs)
install(TARGETS sharposrm
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
)
install(FILES src/Native/sharposrm_bridge.h
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)
