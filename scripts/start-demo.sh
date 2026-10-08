#!/usr/bin/env bash
# Start a persistent, loopback-only Development demo. Never replaces existing containers.
set -euo pipefail
cd "$(dirname "$0")/.."
environment_file=${1:-.env.demo}
sql_image=mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090
for name in serveflow-demo-sql serveflow-demo-web; do
    if podman container exists "$name"; then
        echo "Container $name already exists. Start it with podman start; this script will not replace it." >&2
        exit 1
    fi
done
if curl --silent --max-time 2 http://127.0.0.1:5080/ >/dev/null; then
    echo 'Port 5080 already serves an application. Stop that application or use the manual runbook with another port.' >&2
    exit 1
fi
podman image exists localhost/serveflow:phase8
podman image exists localhost/serveflow-migrations:phase8
if [[ ! -f "$environment_file" ]]; then python3 scripts/create-demo-env.py "$environment_file"; fi
if ! grep -qx 'ASPNETCORE_ENVIRONMENT=Development' "$environment_file"; then
    echo 'This script requires a generated Development demo environment file.' >&2; exit 1
fi
sql_id=''; web_id=''
cleanup_on_failure() {
    result=$?
    if [[ "$result" != 0 ]]; then
        [[ -z "$web_id" ]] || podman rm -f "$web_id" >/dev/null 2>&1 || true
        [[ -z "$sql_id" ]] || podman rm -f "$sql_id" >/dev/null 2>&1 || true
        echo 'Demo startup failed. The private environment file and named data volumes were retained for diagnosis/retry.' >&2
    fi
}
trap cleanup_on_failure EXIT
podman network exists serveflow-demo || podman network create serveflow-demo >/dev/null
for volume in serveflow-demo-db serveflow-demo-keys serveflow-demo-images; do
    podman volume exists "$volume" || podman volume create "$volume" >/dev/null
done
sql_id=$(podman run -d --name serveflow-demo-sql --network serveflow-demo --network-alias sql --env-file "$environment_file" -v serveflow-demo-db:/var/opt/mssql:U,Z "$sql_image")
ready=false
for attempt in $(seq 1 90); do
    if [[ $(podman inspect --format '{{.State.Running}}' "$sql_id") != true ]]; then break; fi
    if podman exec "$sql_id" sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1" >/dev/null' >/dev/null 2>&1; then ready=true; break; fi
    sleep 2
done
if [[ "$ready" != true ]]; then podman logs --tail 50 "$sql_id" >&2; exit 1; fi
podman run --rm --network serveflow-demo --env-file "$environment_file" localhost/serveflow-migrations:phase8
podman run --rm --network serveflow-demo --env-file "$environment_file" localhost/serveflow:phase8 --provision-demo
web_id=$(podman run -d --name serveflow-demo-web --network serveflow-demo --env-file "$environment_file" -p 127.0.0.1:5080:8080 -v serveflow-demo-keys:/data/keys -v serveflow-demo-images:/data/images localhost/serveflow:phase8)
for attempt in $(seq 1 60); do
    if curl --silent --fail http://127.0.0.1:5080/health/database >/dev/null; then
        echo "Development demo ready: http://localhost:5080/Account/Login. Credentials are in $environment_file; keep it private."
        exit 0
    fi
    if [[ $(podman inspect --format '{{.State.Running}}' "$web_id") != true ]]; then break; fi
    sleep 1
done
podman logs --tail 50 "$web_id" >&2
exit 1
