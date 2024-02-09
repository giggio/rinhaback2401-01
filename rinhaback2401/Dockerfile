ARG CURL=with
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled as base
WORKDIR /app
ENV ASPNETCORE_URLS http://*:9999
ENTRYPOINT ["/app/rinhaback2401"]

FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
RUN apt-get update && apt-get install clang zlib1g-dev curl -y --no-install-recommends
RUN cd /; ldd /usr/bin/curl | tr -s '[:blank:]' '\n' | grep '^/' \
    | sort | uniq \
    | xargs -I % sh -c 'mkdir -p $(dirname deps%); cp % deps%;'
WORKDIR /app
COPY ./rinhaback2401.csproj ./
RUN dotnet restore -r linux-x64 /property:Configuration=Release
COPY . .
RUN dotnet publish --no-restore -p:ExtraOptimize=true -o out

FROM base as base_without_curl
COPY --from=build /app/out .

FROM base_without_curl as base_with_curl
COPY --from=build /usr/bin/curl /usr/bin/curl
COPY --from=build /deps/lib /lib
COPY --from=build /deps/lib64 /lib64

FROM base_${CURL}_curl