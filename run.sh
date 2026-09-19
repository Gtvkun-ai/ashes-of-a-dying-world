#!/usr/bin/env sh
cd "$(dirname "$0")" || exit 1
if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "http://localhost:8080" >/dev/null 2>&1 &
elif command -v open >/dev/null 2>&1; then
  open "http://localhost:8080" >/dev/null 2>&1 &
fi
python3 -m http.server 8080
