FROM mcr.microsoft.com/dotnet/runtime-deps:8.0.0-jammy as base
WORKDIR /app
ENV ASPNETCORE_URLS http://*:9999
RUN apt-get update && apt-get install curl -y
ENTRYPOINT ["/app/rinhaback2401"]

FROM mcr.microsoft.com/dotnet/sdk:8.0.100-jammy AS build
WORKDIR /app
COPY ./rinhaback2401/rinhaback2401.csproj ./rinhaback2401/
RUN cd rinhaback2401 && ls -lah && dotnet restore /property:Configuration=Release
COPY . .
RUN cd rinhaback2401 && dotnet publish -c Release -r linux-x64 -o out

FROM base
COPY --from=build /app/rinhaback2401/out .