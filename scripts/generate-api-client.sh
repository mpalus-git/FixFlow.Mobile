#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."
dotnet tool restore
dotnet refitter --settings-file openapi/fixflow-api.refitter --no-logging --no-banner --silent
dotnet run --project tools/FixFlow.Tools -- strip-comments src/FixFlow.Mobile.Core/Api/FixFlowApi.g.cs
