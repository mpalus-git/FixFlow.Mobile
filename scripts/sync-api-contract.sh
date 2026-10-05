#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
curl -fsSL https://raw.githubusercontent.com/mpalus-git/FixFlow.Api/main/openapi/v1.json -o "$root/openapi/fixflow-api.v1.json"
echo "Updated openapi/fixflow-api.v1.json"
