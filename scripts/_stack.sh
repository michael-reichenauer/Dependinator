#!/bin/bash

# Shared helpers for the local dev stack (Azurite + Azure Functions host).
# Sourced by ./run, ./watch and ./e2e — not meant to be executed directly.
#
# The start helpers set STACK_AZURITE_PID / STACK_FUNC_PID so callers can stop
# only the processes they started in their own cleanup trap.

# Try opening a TCP connection to a local port. A successful connection means
# something is already listening there.
stack_port_is_open() {
    bash -c "exec 3<>/dev/tcp/127.0.0.1/$1" 2>/dev/null
}

# Kill any process listening on the given TCP ports. Used at startup so a previous
# run that did not shut down cleanly (leftover app, Functions host or Azurite) does
# not block a fresh start by holding onto a port.
stack_free_ports() {
    local port pids
    for port in "$@"; do
        pids="$(lsof -ti "tcp:$port" -sTCP:LISTEN 2>/dev/null || true)"
        [[ -z "$pids" ]] && continue
        echo "Port $port is in use by a lingering process (pid: $pids); stopping it." >&2
        kill $pids 2>/dev/null || true
        # Wait briefly for a graceful exit before force-killing survivors. This also
        # catches the `func` worker child (Api.dll), which outlives its parent and
        # keeps holding the Functions port after the parent is killed.
        for _ in $(seq 1 20); do
            stack_port_is_open "$port" || break
            sleep 0.2
        done
        pids="$(lsof -ti "tcp:$port" -sTCP:LISTEN 2>/dev/null || true)"
        [[ -n "$pids" ]] && kill -9 $pids 2>/dev/null || true
    done
}

# Seed src/Api/local.settings.json from the checked-in example when missing.
stack_seed_local_settings() {
    local api_dir="$1"
    local local_settings="$api_dir/local.settings.json"
    local example="$api_dir/local.settings.example.json"
    if [[ ! -f "$local_settings" && -f "$example" ]]; then
        cp "$example" "$local_settings"
    fi
}

# Fail with guidance if the Azure Functions Core Tools are not installed.
stack_require_func() {
    if ! command -v func >/dev/null 2>&1; then
        echo "Azure Functions Core Tools ('func') is required." >&2
        echo "Install it from https://learn.microsoft.com/azure/azure-functions/functions-run-local" >&2
        exit 1
    fi
}

# Start Azurite in the background, preferring a global binary over npx.
# Sets STACK_AZURITE_PID to the started process.
stack_start_azurite() {
    local azurite_dir="$1"
    mkdir -p "$azurite_dir"
    if command -v azurite >/dev/null 2>&1; then
        azurite --silent --skipApiVersionCheck --location "$azurite_dir" --debug "$azurite_dir/debug.log" &
    else
        npx --yes azurite --silent --skipApiVersionCheck --location "$azurite_dir" --debug "$azurite_dir/debug.log" &
    fi
    # `$!` is the PID of the most recent background command.
    STACK_AZURITE_PID="$!"
}

# Start the Azure Functions host in the background and wait until it accepts
# connections. Sets STACK_FUNC_PID. Exits the script if the host never comes up.
stack_start_func() {
    local api_dir="$1"
    local port="${2:-7071}"
    # Parentheses create a subshell so the `cd` does not affect the caller.
    (
        cd "$api_dir"
        func start --port "$port"
    ) &
    STACK_FUNC_PID="$!"

    # Wait up to 60 seconds (120 * 0.5s) for the host to accept connections.
    for _ in $(seq 1 120); do
        if stack_port_is_open "$port"; then
            break
        fi
        sleep 0.5
    done
    if ! stack_port_is_open "$port"; then
        echo "Azure Functions host did not start on port $port." >&2
        exit 1
    fi
}
