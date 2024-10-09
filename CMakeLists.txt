cmake_minimum_required(VERSION 3.29)

project(wnprainmeter)
file(READ "VERSION" PROJECT_VERSION)

function(strip_suffix VERSION OUT_STRIPPED_VERSION)
  string(FIND "${VERSION}" "-" POS)
  if(POS GREATER -1)
    string(SUBSTRING "${VERSION}" 0 ${POS} STRIPPED_VERSION)
  else()
    set(STRIPPED_VERSION "${VERSION}" PARENT_SCOPE)
  endif()
  # Set the output variable to the stripped version
  set(${OUT_STRIPPED_VERSION} "${STRIPPED_VERSION}" PARENT_SCOPE)
endfunction()

string(REPLACE "." "," RES_PROJECT_VERSION_C ${PROJECT_VERSION})
strip_suffix(${PROJECT_VERSION} RES_PROJECT_VERSION)
strip_suffix(${RES_PROJECT_VERSION_C} RES_PROJECT_VERSION_C)

set(RES_COPYRIGHT "2024 - keifufu, tjhrulz")
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
  set(LIBRARY_SUFFIX "64")
  set(RES_PRODUCT_VERSION "3.0.2.2161 (64-bit)")
elseif(CMAKE_SIZEOF_VOID_P EQUAL 4)
  set(LIBRARY_SUFFIX "32")
  set(RES_PRODUCT_VERSION "3.0.2.2161 (32-bit)")
endif()

configure_file(
  ${CMAKE_CURRENT_SOURCE_DIR}/src/resource.rc.in
  ${CMAKE_CURRENT_BINARY_DIR}/resource.rc
  @ONLY
)

find_package(libwnp REQUIRED)

set(SRC_FILES
  src/main.c
  ${CMAKE_CURRENT_BINARY_DIR}/resource.rc
)

add_compile_definitions(WNPRAINMETER_VERSION="${PROJECT_VERSION}")

set(CMAKE_STATIC_LIBRARY_PREFIX "")
add_library(${PROJECT_NAME} SHARED ${SRC_FILES})

target_link_libraries(${PROJECT_NAME} PRIVATE libwnp::libwnp)

target_link_libraries(${PROJECT_NAME}
  PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/deps/rainmeter_win${LIBRARY_SUFFIX}.lib"
)
