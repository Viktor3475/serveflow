#!/usr/bin/env bash
# Recreates the template structure in an EMPTY directory; then apply the documented configuration.
set -euo pipefail
if [ -e ServeFlow.sln ] || [ -d src ] || [ -d tests ]; then
  echo "Run this command reference in a fresh directory; ServeFlow already exists here." >&2
  exit 1
fi
dotnet new sln -n ServeFlow --format sln
for layer in Domain Application Infrastructure; do
  dotnet new classlib -n "ServeFlow.$layer" -o "src/ServeFlow.$layer" -f net10.0 --no-restore
  rm "src/ServeFlow.$layer/Class1.cs"
done
dotnet new mvc -n ServeFlow.Web -o src/ServeFlow.Web -f net10.0 --no-restore
for kind in UnitTests IntegrationTests; do
  dotnet new console -n "ServeFlow.$kind" -o "tests/ServeFlow.$kind" -f net10.0 --no-restore
  rm "tests/ServeFlow.$kind/Program.cs"
done
dotnet sln ServeFlow.sln add src/ServeFlow.Domain/ServeFlow.Domain.csproj src/ServeFlow.Application/ServeFlow.Application.csproj src/ServeFlow.Infrastructure/ServeFlow.Infrastructure.csproj src/ServeFlow.Web/ServeFlow.Web.csproj tests/ServeFlow.UnitTests/ServeFlow.UnitTests.csproj tests/ServeFlow.IntegrationTests/ServeFlow.IntegrationTests.csproj
dotnet add src/ServeFlow.Application reference src/ServeFlow.Domain
dotnet add src/ServeFlow.Infrastructure reference src/ServeFlow.Application
dotnet add src/ServeFlow.Web reference src/ServeFlow.Application src/ServeFlow.Infrastructure
dotnet add tests/ServeFlow.UnitTests reference src/ServeFlow.Infrastructure
dotnet add tests/ServeFlow.IntegrationTests reference src/ServeFlow.Web
