#!/usr/bin/env bash
# Uses an isolated disposable Developer container. Never targets an existing database.
set -euo pipefail
cd "$(dirname "$0")/.."
engine=${CONTAINER_ENGINE:-podman}
image=${SQLSERVER_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090}
command -v "$engine" >/dev/null
command -v python3 >/dev/null
task_dir=$(mktemp -d)
container="serveflow-tests-$(python3 -c 'import secrets; print(secrets.token_hex(6))')"
cleanup() {
    "$engine" rm -f "$container" >/dev/null 2>&1 || true
    rm -rf "$task_dir"
}
trap cleanup EXIT
umask 077
python3 - "$task_dir/sql.env" <<'PY'
import secrets, sys
with open(sys.argv[1], 'w') as target:
    target.write('ACCEPT_EULA=Y\nMSSQL_PID=Developer\nMSSQL_MEMORY_LIMIT_MB=4096\nMSSQL_SA_PASSWORD=SF!'+secrets.token_hex(24)+'aZ9\n')
PY
"$engine" run -d --name "$container" --env-file "$task_dir/sql.env" -p 127.0.0.1::1433 "$image" >/dev/null
ready=false
for attempt in $(seq 1 90); do
    if [[ $("$engine" inspect --format '{{.State.Running}}' "$container") != true ]]; then break; fi
    if "$engine" exec "$container" sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1" >/dev/null' >/dev/null 2>&1; then
        ready=true; break
    fi
    sleep 2
done
if [[ "$ready" != true ]]; then
    echo 'SQL Server did not become ready; inspect its startup requirements.' >&2
    "$engine" logs --tail 40 "$container" >&2
    exit 1
fi
port=$("$engine" port "$container" 1433/tcp)
port=${port##*:}
password=$(sed -n 's/^MSSQL_SA_PASSWORD=//p' "$task_dir/sql.env")
export SERVEFLOW_TEST_SQLSERVER="Server=127.0.0.1,$port;Database=master;User ID=sa;Password=$password;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
echo 'SQL Server ready. Running the full suite against generated per-test databases.'
dotnet test --solution ServeFlow.sln -c Release "$@"
