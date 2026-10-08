# Override these with reviewed tag@sha256 references for a release.
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317
ARG RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4
FROM ${SDK_IMAGE} AS build
WORKDIR /source
COPY . .
RUN dotnet restore ServeFlow.sln --locked-mode
RUN dotnet publish src/ServeFlow.Web/ServeFlow.Web.csproj -c Release --no-restore -o /out/web /p:UseAppHost=false

FROM build AS bundle
RUN dotnet tool restore
RUN dotnet ef migrations bundle --project src/ServeFlow.Infrastructure --startup-project src/ServeFlow.Web --configuration Release --target-runtime linux-x64 --output /out/efbundle

FROM ${RUNTIME_IMAGE} AS migrations
WORKDIR /app
COPY --from=bundle /out/efbundle ./efbundle
USER $APP_UID
ENTRYPOINT ["/app/efbundle"]

FROM ${RUNTIME_IMAGE} AS runtime
WORKDIR /app
COPY --from=build /out/web .
USER root
RUN mkdir -p /data/keys /data/images && chown -R "$APP_UID" /data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DataProtection__KeyPath=/data/keys \
    Uploads__RootPath=/data/images
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ServeFlow.Web.dll"]
