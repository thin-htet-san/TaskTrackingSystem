#!/usr/bin/env bash
set -euo pipefail

: "${WebApi__BaseUrl:=http://127.0.0.1:5001/api/}"
export WebApi__BaseUrl

dotnet /app/api/TaskTrackingSystem.WebApi.dll --urls http://127.0.0.1:5001 &
api_pid=$!

dotnet /app/webapp/TaskTrackingSystem.WebApp.dll --urls http://127.0.0.1:5000 &
webapp_pid=$!

term_handler() {
  kill "$api_pid" "$webapp_pid" 2>/dev/null || true
  exit 0
}

trap term_handler SIGTERM SIGINT

nginx -g 'daemon off;' &
nginx_pid=$!

wait -n "$api_pid" "$webapp_pid" "$nginx_pid"
term_handler
