cmake_minimum_required(VERSION 3.18)
project(sharposrm LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ── Conan integration ────────────────────────────────────────────────────
option(ENABLE_CONAN "Use Conan for dependencies" ON)

if(ENABLE_CONAN)
    # Locate conan executable
    find_program(CONAN_EXECUTABLE NAMES conan)
    if(NOT CONAN_EXECUTABLE)
        message(FATAL_ERROR "Conan not found! Install Conan 2.x: pip install conan")
    endif()

    # Default to Release if not set (needed for Conan's build_type setting)
    if(NOT CMAKE_BUILD_TYPE)
        set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
    endif()

    # Run conan install from the build directory
    # This creates conan_toolchain.cmake and CMakeDeps files
    execute_process(
        COMMAND ${CONAN_EXECUTABLE} install ${CMAKE_SOURCE_DIR}
            --output-folder=${CMAKE_BINARY_DIR}
            --build=missing
            --settings compiler.cppstd=20
            --settings build_type=${CMAKE_BUILD_TYPE}
        WORKING_DIRECTORY ${CMAKE_BINARY_DIR}
        OUTPUT_VARIABLE _conan_stdout
        ERROR_VARIABLE _conan_stderr
        RESULT_VARIABLE _conan_result
    )
    if(NOT _conan_result EQUAL 0)
        message(FATAL_ERROR "Conan install failed (${_conan_result}):\n${_conan_stderr}\n${_conan_stdout}")
    endif()

    # Load Conan-generated toolchain (sets CMAKE_PREFIX_PATH for find_package)
    include(${CMAKE_BINARY_DIR}/conan_toolchain.cmake)

    # Conan sets CMAKE_OSX_SYSROOT to just the SDK name (e.g. "macosx") which
    # works with the Xcode generator but not Makefiles/Ninja.  Resolve to the
    # full path if it is not already one.
    if(APPLE AND CMAKE_OSX_SYSROOT AND NOT IS_ABSOLUTE "${CMAKE_OSX_SYSROOT}")
        execute_process(
            COMMAND xcrun --show-sdk-path --sdk ${CMAKE_OSX_SYSROOT}
            OUTPUT_VARIABLE _resolved_sdk
            OUTPUT_STRIP_TRAILING_WHITESPACE
            RESULT_VARIABLE _sdk_result
        )
        if(_sdk_result EQUAL 0 AND EXISTS "${_resolved_sdk}")
            set(CMAKE_OSX_SYSROOT "${_resolved_sdk}" CACHE PATH "macOS SDK path" FORCE)
            message(STATUS "Resolved CMAKE_OSX_SYSROOT: ${CMAKE_OSX_SYSROOT}")
        else()
            message(WARNING "Could not resolve SDK path for '${CMAKE_OSX_SYSROOT}': ${_resolved_sdk}")
        endif()
    endif()
endif()

# ── Build path ───────────────────────────────────────────────────────────
if(ENABLE_CONAN)
    # --- Conan path: build osrm-backend as a subproject ---
    # Disable everything we don't need in the OSRM subproject
    set(BUILD_PACKAGE OFF CACHE BOOL "" FORCE)
    set(ENABLE_NODE_BINDINGS OFF CACHE BOOL "" FORCE)
    set(ENABLE_CCACHE OFF CACHE BOOL "" FORCE)
    set(ENABLE_LTO OFF CACHE BOOL "" FORCE)
    set(ENABLE_FUZZING OFF CACHE BOOL "" FORCE)
    set(ENABLE_CLANG_TIDY OFF CACHE BOOL "" FORCE)
    set(ENABLE_SANITIZER OFF CACHE BOOL "" FORCE)
    set(ENABLE_COVERAGE OFF CACHE BOOL "" FORCE)
    # OSRM should NOT run its own Conan install — we handle it at root level.
    # Keep ENABLE_CONAN=ON for OSRM so that the pkg-config generation code
    # skips the $<TARGET_LINKER_FILE:...> generator expressions that fail
    # on INTERFACE IMPORTED targets created by Conan's CMakeDeps.
    set(ENABLE_CONAN ON CACHE BOOL "" FORCE)
    # Override OSRM's conan install: point CONAN_EXECUTABLE to a no-op script
    # so OSRM's ENABLE_CONAN block finds conan but doesn't re-install packages.
    # The real Conan install already happened at the root level.
    if(WIN32)
        file(WRITE ${CMAKE_BINARY_DIR}/conan-noop.cmd "@echo off\nexit /b 0\n")
        set(CONAN_EXECUTABLE "${CMAKE_BINARY_DIR}/conan-noop.cmd" CACHE FILEPATH "Conan executable" FORCE)
    else()
        file(WRITE ${CMAKE_BINARY_DIR}/conan-noop.sh "#!/bin/sh\nexit 0\n")
        file(CHMOD ${CMAKE_BINARY_DIR}/conan-noop.sh PERMISSIONS OWNER_READ OWNER_WRITE OWNER_EXECUTE)
        set(CONAN_EXECUTABLE "${CMAKE_BINARY_DIR}/conan-noop.sh" CACHE FILEPATH "Conan executable" FORCE)
    endif()

    # GCC 13+ emits false-positive -Warray-bounds errors in sol2 header
    # templates used by osrm-backend (scripting_environment_lua.cpp).
    # This is a known upstream issue; suppress it as an error for the subproject.
    if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
        add_compile_options(-Wno-error=array-bounds)
    endif()

    # ── Optional: Strip TBB malloc proxy from Conan-generated TBB config ───
    # Conan's onetbb package unconditionally adds tbbmalloc and tbbmalloc_proxy
    # to the transitive dependency chain of TBB::tbb.  tbbmalloc provides
    # TBB's scalable allocator (scalable_malloc/scalable_free) which OSRM uses
    # for performance.  tbbmalloc_proxy goes further — it replaces the process's
    # malloc/free/realloc with TBB's allocator globally, which can cause crashes
    # and corruption in co-loaded native libraries (e.g. SQLite).
    #
    # By default we keep both (OSRM needs tbbmalloc for performance).  Enable
    # STRIP_TBBMALLOC=ON only if you need to isolate this library from other
    # native code that cannot tolerate a replaced malloc.
    option(STRIP_TBBMALLOC "Strip tbbmalloc and tbbmalloc_proxy from the link (may degrade OSRM performance)" OFF)
    if(STRIP_TBBMALLOC)
        file(GLOB _tbb_target_files "${CMAKE_BINARY_DIR}/TBB-Target-*.cmake")
        foreach(_f ${_tbb_target_files})
            file(READ "${_f}" _content)
            string(REGEX REPLACE "TBB::tbbmalloc_proxy" "" _content "${_content}")
            string(REGEX REPLACE "TBB::tbbmalloc" "" _content "${_content}")
            string(REGEX REPLACE "libtbbmalloc_proxy\\.[a-z0-9]+\\.dylib" "" _content "${_content}")
            string(REGEX REPLACE "libtbbmalloc\\.[a-z0-9]+\\.dylib" "" _content "${_content}")
            string(REGEX REPLACE "libtbbmalloc_proxy\\.so[.*a-z0-9]*" "" _content "${_content}")
            string(REGEX REPLACE "libtbbmalloc\\.so[.*a-z0-9]*" "" _content "${_content}")
            string(REGEX REPLACE "tbbmalloc_proxy\\.lib" "" _content "${_content}")
            string(REGEX REPLACE "tbbmalloc\\.lib" "" _content "${_content}")
            file(WRITE "${_f}" "${_content}")
        endforeach()
        message(STATUS "STRIP_TBBMALLOC=ON: tbbmalloc and tbbmalloc_proxy removed from link")
    endif()

    add_subdirectory(osrm-backend)

    # After add_subdirectory, OSRM exposes these to parent scope:
    #   LibOSRM_LIBRARIES, LibOSRM_DEPENDENT_LIBRARIES, LibOSRM_INCLUDE_DIRS,
    #   LibOSRM_LIBRARY_DIR, LibOSRM_CXXFLAGS

    find_package(Threads REQUIRED)

    # Build the shared library
    add_library(sharposrm SHARED
        src/Native/sharposrm_bridge.cpp
    )

    target_include_directories(sharposrm PRIVATE
        ${LibOSRM_INCLUDE_DIRS}
        ${CMAKE_CURRENT_SOURCE_DIR}/osrm-backend/third_party/flatbuffers/include
        ${CMAKE_CURRENT_SOURCE_DIR}/osrm-backend/generated/include
    )

    # CXXFLAGS is a space-separated string; convert to a CMake list for compile options
    separate_arguments(_OSRM_CXXFLAGS NATIVE_COMMAND "${LibOSRM_CXXFLAGS}")

    target_compile_options(sharposrm PRIVATE
        ${_OSRM_CXXFLAGS}
    )

    # Link against OSRM CMake targets (not file paths).
    # The pipeline targets (extract, partition, etc.) are separate static
    # libraries that are not transitively linked by the main osrm target.
    target_link_libraries(sharposrm PRIVATE
        osrm
        osrm_extract
        osrm_partition
        osrm_customize
        osrm_contract
        osrm_update
        osrm_store
        osrm_guidance
        Threads::Threads
    )

    # Set build rpath so the library can find its dependencies at runtime
    set_target_properties(sharposrm PROPERTIES
        BUILD_RPATH "${LibOSRM_LIBRARY_DIR}"
    )

else()
    # --- Legacy Homebrew path ---
    # Add cmake/ to module path for FindLibOSRM
    list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")

    # Find dependencies via Homebrew/pkg-config
    find_package(LibOSRM REQUIRED)
    find_package(Threads REQUIRED)

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
    target_include_directories(sharposrm PRIVATE
        ${LibOSRM_INCLUDE_DIRS}
        ${CMAKE_CURRENT_SOURCE_DIR}/osrm-backend/include
    )

    # Apply OSRM compiler defines (e.g. BOOST_SPIRIT_USE_PHOENIX_V3)
    target_compile_definitions(sharposrm PRIVATE
        ${LibOSRM_CXXFLAGS}
    )

    # Derive the OSRM lib dir from the first library in LibOSRM_LINKABLE_LIBRARIES
    set(_OSRM_LIB_DIR "")
    foreach(_lib ${LibOSRM_LINKABLE_LIBRARIES})
        get_filename_component(_lib_dir "${_lib}" DIRECTORY)
        if(_lib_dir)
            set(_OSRM_LIB_DIR "${_lib_dir}")
            break()
        endif()
    endforeach()
    if(NOT _OSRM_LIB_DIR)
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
        PkgConfig::LUA
        bz2
        expat
        z
    )
endif()

# ── Common properties ────────────────────────────────────────────────────
# Hide all symbols by default; only SHARPOSRM_EXPORT ones are visible
set_target_properties(sharposrm PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    DEFINE_SYMBOL SHARPOSRM_BUILDING
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
