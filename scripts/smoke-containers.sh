#!/usr/bin/env bash
# Disposable containers, database, certificates and volumes; never a deployment.
set -euo pipefail
cd "$(dirname "$0")/.."
engine=${CONTAINER_ENGINE:-podman}
sql_image=${SQLSERVER_IMAGE:-mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090}
web_image=${SERVEFLOW_IMAGE:-localhost/serveflow:phase8}
migration_image=${SERVEFLOW_MIGRATION_IMAGE:-localhost/serveflow-migrations:phase8}
task_dir=$(mktemp -d)
suffix=$(python3 -c 'import secrets; print(secrets.token_hex(6))')
network="serveflow-smoke-$suffix"
sql="serveflow-smoke-sql-$suffix"
web="serveflow-smoke-web-$suffix"
keys="serveflow-smoke-keys-$suffix"
images="serveflow-smoke-images-$suffix"
cleanup() {
    result=$?
    if [[ "$result" != 0 ]]; then "$engine" logs "$sql" >&2 || true; fi
    "$engine" rm -f "$web" "$sql" >/dev/null 2>&1 || true
    "$engine" network rm "$network" >/dev/null 2>&1 || true
    "$engine" volume rm "$keys" "$images" >/dev/null 2>&1 || true
    rm -rf "$task_dir"
}
trap cleanup EXIT
umask 077
mkdir "$task_dir/certs" "$task_dir/sql-certs"
password=$(python3 -c 'import secrets; print("SF!"+secrets.token_hex(24)+"aZ9")')
cert_password=$(python3 -c 'import secrets; print(secrets.token_hex(24))')
export sf_cert_password="$cert_password"
openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj '/CN=ServeFlow disposable smoke CA' -keyout "$task_dir/ca.key" -out "$task_dir/ca.crt" >/dev/null 2>&1
openssl req -new -newkey rsa:2048 -nodes -subj /CN=sql -keyout "$task_dir/sql-certs/sql.key" -out "$task_dir/sql.csr" >/dev/null 2>&1
printf 'subjectAltName=DNS:sql\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\nbasicConstraints=critical,CA:FALSE\n' > "$task_dir/sql.extensions"
openssl x509 -req -in "$task_dir/sql.csr" -CA "$task_dir/ca.crt" -CAkey "$task_dir/ca.key" -CAcreateserial -days 2 -extfile "$task_dir/sql.extensions" -out "$task_dir/certs/sql.crt" >/dev/null 2>&1
cp "$task_dir/certs/sql.crt" "$task_dir/sql-certs/sql.crt"
cp "$task_dir/ca.crt" "$task_dir/certs/ca.crt"
openssl req -x509 -newkey rsa:2048 -nodes -days 2 -subj /CN=localhost -addext subjectAltName=DNS:localhost -keyout "$task_dir/web.key" -out "$task_dir/certs/web.crt" >/dev/null 2>&1
openssl pkcs12 -export -inkey "$task_dir/web.key" -in "$task_dir/certs/web.crt" -out "$task_dir/certs/web.pfx" -passout env:sf_cert_password >/dev/null 2>&1
printf '[network]\ntlscert = /run/test-certs/sql.crt\ntlskey = /run/test-certs/sql.key\ntlsprotocols = 1.2\nforceencryption = 1\n' > "$task_dir/sql-certs/mssql.conf"
# Parent directory stays private; these disposable cert files must be readable by container UIDs.
chmod 755 "$task_dir/certs" "$task_dir/sql-certs"
chmod 444 "$task_dir/certs/"* "$task_dir/sql-certs/"*
printf 'ACCEPT_EULA=Y\nMSSQL_PID=Developer\nMSSQL_MEMORY_LIMIT_MB=4096\nMSSQL_SA_PASSWORD=%s\n' "$password" > "$task_dir/sql.env"
connection="Server=sql,1433;Database=ServeFlowSmoke;User ID=sa;Password=$password;Encrypt=true;TrustServerCertificate=false;Connect Timeout=15"
printf 'ASPNETCORE_ENVIRONMENT=Production\nSSL_CERT_FILE=/run/test-certs/ca.crt\nConnectionStrings__ServeFlow=%s\n' "$connection" > "$task_dir/migration.env"
cp "$task_dir/migration.env" "$task_dir/web.env"
printf 'AllowedHosts=localhost\nDataProtection__CertificatePath=/run/test-certs/web.pfx\nDataProtection__CertificatePassword=%s\nASPNETCORE_URLS=https://+:8443\nKestrel__Certificates__Default__Path=/run/test-certs/web.pfx\nKestrel__Certificates__Default__Password=%s\nOrdering__PublicOrigin=https://localhost\n' "$cert_password" "$cert_password" >> "$task_dir/web.env"
"$engine" network create "$network" >/dev/null
"$engine" volume create "$keys" >/dev/null
"$engine" volume create "$images" >/dev/null
"$engine" run -d --name "$sql" --hostname sql --network "$network" --network-alias sql --env-file "$task_dir/sql.env" \
    -v "$task_dir/sql-certs:/run/test-certs:ro,Z" -v "$task_dir/sql-certs/mssql.conf:/var/opt/mssql/mssql.conf:ro,Z" "$sql_image" >/dev/null
ready=false
for attempt in $(seq 1 90); do
    if [[ $("$engine" inspect --format '{{.State.Running}}' "$sql") != true ]]; then break; fi
    if "$engine" exec "$sql" sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b -Q "SELECT 1" >/dev/null' >/dev/null 2>&1; then ready=true; break; fi
    sleep 2
done
if [[ "$ready" != true ]]; then "$engine" logs --tail 50 "$sql" >&2; exit 1; fi
"$engine" run --rm --network "$network" --env-file "$task_dir/migration.env" -v "$task_dir/certs:/run/test-certs:ro,Z" "$migration_image"
# Re-apply to prove the bundle is idempotent.
"$engine" run --rm --network "$network" --env-file "$task_dir/migration.env" -v "$task_dir/certs:/run/test-certs:ro,Z" "$migration_image"
python3 scripts/create-demo-env.py "$task_dir/demo.env" >/dev/null
printf 'SSL_CERT_FILE=/run/test-certs/ca.crt\nConnectionStrings__ServeFlow=%s\n' "$connection" >> "$task_dir/demo.env"
"$engine" run --rm --network "$network" --env-file "$task_dir/demo.env" -v "$task_dir/certs:/run/test-certs:ro,Z" "$web_image" --provision-demo
if "$engine" run --rm --env-file "$task_dir/web.env" "$web_image" --provision-demo > "$task_dir/rejected-demo.log" 2>&1; then
    echo 'Production unexpectedly accepted demo provisioning.' >&2; exit 1
fi
grep -q 'only in Development' "$task_dir/rejected-demo.log"
"$engine" run -d --name "$web" --network "$network" --env-file "$task_dir/web.env" --read-only \
    --tmpfs /tmp:rw,nosuid,nodev,size=64m --cap-drop ALL --security-opt no-new-privileges \
    -v "$keys:/data/keys" -v "$images:/data/images" -v "$task_dir/certs:/run/test-certs:ro,Z" \
    -p 127.0.0.1::8443 "$web_image" >/dev/null
port=$("$engine" port "$web" 8443/tcp); port=${port##*:}
ready=false
for attempt in $(seq 1 60); do
    if curl --silent --fail --cacert "$task_dir/certs/web.crt" "https://localhost:$port/health/live" >/dev/null; then ready=true; break; fi
    if [[ $("$engine" inspect --format '{{.State.Running}}' "$web") != true ]]; then break; fi
    sleep 1
done
if [[ "$ready" != true ]]; then "$engine" logs --tail 60 "$web" >&2; exit 1; fi
curl --silent --show-error --fail --cacert "$task_dir/certs/web.crt" "https://localhost:$port/health/database" >/dev/null
curl --silent --show-error --fail --cacert "$task_dir/certs/web.crt" "https://localhost:$port/Account/Login" > "$task_dir/login.html"
grep -q '__RequestVerificationToken' "$task_dir/login.html"
python3 scripts/verify-container-http.py "https://localhost:$port" "$task_dir/certs/web.crt" "$task_dir/demo.env" "$task_dir"
[[ $("$engine" exec "$web" id -u) != 0 ]]
# A login GET creates Data Protection keys; encrypted keys must survive container restart.
"$engine" exec "$web" sh -c 'test -n "$(find /data/keys -name "key-*.xml" -print -quit)" && grep -q encryptedSecret /data/keys/key-*.xml'
"$engine" restart "$web" >/dev/null
for attempt in $(seq 1 30); do
    if curl --silent --fail --cacert "$task_dir/certs/web.crt" "https://localhost:$port/health/database" >/dev/null; then
        python3 scripts/verify-container-http.py "https://localhost:$port" "$task_dir/certs/web.crt" "$task_dir/demo.env" "$task_dir" --resume
        echo 'Container smoke passed: migrations, idempotent re-apply, verified SQL TLS, production HTTPS, non-root read-only runtime, encrypted persistent keys and restart.'
        exit 0
    fi
    sleep 1
done
exit 1
