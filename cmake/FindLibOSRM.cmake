# - Try to find LibOSRM
# Once done this will define
#  LibOSRM_FOUND - System has LibOSRM
#  LibOSRM_INCLUDE_DIRS - The include directories needed to use LibOSRM (including transitive deps like Boost)
#  LibOSRM_LINKABLE_LIBRARIES - The libraries needed to link against LibOSRM (deduped)
#  LibOSRM_CXXFLAGS - Compiler switches required for using LibOSRM (defines only, no -I flags)

find_package(PkgConfig)
pkg_search_module(PC_LibOSRM QUIET libosrm)

# ── Include directories ──────────────────────────────────────────────────
# Collect ALL -I paths from pkg-config (OSRM itself + transitive deps like Boost).
# Also add the osrm/ subdirectory as a separate include root because OSRM headers
# use relative includes like #include "util/json_container.hpp".
set(LibOSRM_INCLUDE_DIRS "")

# PC_LibOSRM_INCLUDE_DIRS contains all include directories from pkg-config
foreach(_dir ${PC_LibOSRM_INCLUDE_DIRS})
    list(APPEND LibOSRM_INCLUDE_DIRS "${_dir}")
endforeach()

# Also find via find_path as fallback
find_path(LibOSRM_INCLUDE_DIR
    NAMES osrm/osrm.hpp
    PATH_SUFFIXES osrm include/osrm include
    HINTS
        ${PC_LibOSRM_INCLUDEDIR}
        ${PC_LibOSRM_INCLUDE_DIRS}
        ~/Library/Frameworks
        /Library/Frameworks
        /usr/local
        /usr
        /opt/local
        /opt
        /opt/homebrew
)
list(APPEND LibOSRM_INCLUDE_DIRS "${LibOSRM_INCLUDE_DIR}")
# OSRM headers use relative includes within the osrm/ namespace
list(APPEND LibOSRM_INCLUDE_DIRS "${LibOSRM_INCLUDE_DIR}/osrm")

list(REMOVE_DUPLICATES LibOSRM_INCLUDE_DIRS)

# ── Compiler flags (defines only, no -I) ─────────────────────────────────
# Filter to keep only -D flags (defines), since we handle -I above.
set(_define_flags "")
foreach(_flag ${PC_LibOSRM_CFLAGS})
    string(FIND "${_flag}" "-D" _d_idx)
    if(_d_idx EQUAL 0)
        list(APPEND _define_flags "${_flag}")
    endif()
endforeach()
list(REMOVE_DUPLICATES _define_flags)
set(LibOSRM_CXXFLAGS ${_define_flags})

# ── Linkable libraries (deduped) ─────────────────────────────────────────
set(_LibOSRM_RAW_LIBS ${PC_LibOSRM_LDFLAGS})

# Resolve to full library paths when possible for better dedup
if(PC_LibOSRM_LIBRARY_DIRS AND PC_LibOSRM_LIBRARIES)
    set(_LibOSRM_FULL_LIBS "")
    foreach(_lib ${PC_LibOSRM_LIBRARIES})
        set(_found FALSE)
        foreach(_dir ${PC_LibOSRM_LIBRARY_DIRS})
            if(EXISTS "${_dir}/lib${_lib}.dylib")
                list(APPEND _LibOSRM_FULL_LIBS "${_dir}/lib${_lib}.dylib")
                set(_found TRUE)
                break()
            elseif(EXISTS "${_dir}/lib${_lib}.so")
                list(APPEND _LibOSRM_FULL_LIBS "${_dir}/lib${_lib}.so")
                set(_found TRUE)
                break()
            elseif(EXISTS "${_dir}/lib${_lib}.a")
                list(APPEND _LibOSRM_FULL_LIBS "${_dir}/lib${_lib}.a")
                set(_found TRUE)
                break()
            endif()
        endforeach()
        if(NOT _found)
            list(APPEND _LibOSRM_FULL_LIBS "-l${_lib}")
        endif()
    endforeach()
    if(_LibOSRM_FULL_LIBS)
        set(_LibOSRM_RAW_LIBS ${_LibOSRM_FULL_LIBS})
    endif()
endif()

list(REMOVE_DUPLICATES _LibOSRM_RAW_LIBS)
set(LibOSRM_LINKABLE_LIBRARIES ${_LibOSRM_RAW_LIBS})

# ── Finalize ─────────────────────────────────────────────────────────────
include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(LibOSRM DEFAULT_MSG
    LibOSRM_INCLUDE_DIRS
    LibOSRM_CXXFLAGS
    LibOSRM_LINKABLE_LIBRARIES
)

mark_as_advanced(LibOSRM_INCLUDE_DIRS LibOSRM_CXXFLAGS LibOSRM_LINKABLE_LIBRARIES)
