#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PROJECT_VERSION_FILE="$PROJECT_ROOT/ProjectSettings/ProjectVersion.txt"
RESULT_DIR="$PROJECT_ROOT/Logs/TestResults"
ASSEMBLY_NAME="${1:-DecalSystem.Editor.Tests}"
RESULT_FILE="$RESULT_DIR/decal-editmode-results.xml"
LOG_FILE="$RESULT_DIR/decal-editmode.log"
UNITY_LOCK_FILE="$PROJECT_ROOT/Temp/UnityLockfile"

resolve_unity_executable() {
    if [[ -n "${UNITY_EXECUTABLE:-}" && -x "$UNITY_EXECUTABLE" ]]; then
        printf '%s\n' "$UNITY_EXECUTABLE"
        return 0
    fi

    local editor_version=""
    if [[ -f "$PROJECT_VERSION_FILE" ]]; then
        editor_version="$(awk '/m_EditorVersion:/ { print $2; exit }' "$PROJECT_VERSION_FILE")"
    fi

    local candidates=()
    if [[ -n "$editor_version" ]]; then
        candidates+=("/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity")
        candidates+=("/Applications/Unity/Hub/Editor/${editor_version%c1}/Unity.app/Contents/MacOS/Unity")
    fi
    candidates+=("/Applications/Unity/Unity.app/Contents/MacOS/Unity")

    local candidate
    for candidate in "${candidates[@]}"; do
        if [[ -x "$candidate" ]]; then
            printf '%s\n' "$candidate"
            return 0
        fi
    done

    return 1
}

UNITY_BIN="$(resolve_unity_executable)" || {
    printf 'Unity executable not found.\n' >&2
    printf 'Set UNITY_EXECUTABLE=/path/to/Unity.app/Contents/MacOS/Unity and retry.\n' >&2
    exit 127
}

if [[ -f "$UNITY_LOCK_FILE" && "${UNITY_ALLOW_OPEN_PROJECT:-0}" != "1" ]]; then
    printf 'Unity project is already open: %s\n' "$PROJECT_ROOT" >&2
    printf 'Close the Unity Editor before running batchmode tests.\n' >&2
    printf 'To force a run anyway, set UNITY_ALLOW_OPEN_PROJECT=1.\n' >&2
    exit 73
fi

mkdir -p "$RESULT_DIR"
rm -f "$RESULT_FILE"

printf 'Unity: %s\n' "$UNITY_BIN"
printf 'Project: %s\n' "$PROJECT_ROOT"
printf 'Assembly: %s\n' "$ASSEMBLY_NAME"
printf 'Results: %s\n' "$RESULT_FILE"

"$UNITY_BIN" \
    -batchmode \
    -nographics \
    -projectPath "$PROJECT_ROOT" \
    -runTests \
    -testPlatform EditMode \
    -assemblyNames "$ASSEMBLY_NAME" \
    -testResults "$RESULT_FILE" \
    -logFile "$LOG_FILE"

if [[ ! -s "$RESULT_FILE" ]]; then
    printf 'Unity exited without writing test results: %s\n' "$RESULT_FILE" >&2
    printf 'See log: %s\n' "$LOG_FILE" >&2
    exit 74
fi
