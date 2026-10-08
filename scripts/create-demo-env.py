#!/usr/bin/env python3
"""Generate local-only credentials. Never overwrite an existing environment file."""
import argparse
import os
import secrets
import uuid
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("output", nargs="?", default=".env.demo")
args = parser.parse_args()
target = Path(args.output)
password = lambda: "SF!" + secrets.token_hex(24) + "aZ9"
sql_password = password()
values = {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "ACCEPT_EULA": "Y", "MSSQL_PID": "Developer", "MSSQL_MEMORY_LIMIT_MB": "4096",
    "MSSQL_SA_PASSWORD": sql_password,
    "ConnectionStrings__ServeFlow": "Server=sql,1433;Database=ServeFlowDemo;User ID=sa;Password=" + sql_password + ";Encrypt=true;TrustServerCertificate=true",
    "AllowedHosts": "localhost;127.0.0.1", "Ordering__PublicOrigin": "http://localhost:5080",
    "Bootstrap__RestaurantId": str(uuid.uuid4()), "Bootstrap__RestaurantName": "Olive & Oak · Development demo",
    "Bootstrap__DisplayName": "Demo administrator", "Bootstrap__Email": "admin@serveflow.example.test", "Bootstrap__Password": password(),
    "Demo__WaiterEmail": "waiter@serveflow.example.test", "Demo__WaiterPassword": password(),
    "Demo__KitchenEmail": "kitchen@serveflow.example.test", "Demo__KitchenPassword": password(),
}
descriptor = os.open(target, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
with os.fdopen(descriptor, "w") as stream:
    stream.write("# LOCAL DEVELOPMENT ONLY. Keep private; never commit or use in production.\n")
    stream.writelines(key + "=" + value + "\n" for key, value in values.items())
print("Created private local demo environment file:", target)
