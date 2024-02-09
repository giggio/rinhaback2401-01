#!/usr/bin/env bash

IMAGE=giggio/dotnet-monitor:latest
if [ "`docker images $IMAGE -q`" == '' ]; then
  docker build -f Dockerfile.monitor -t $IMAGE .
fi

CONTAINER="${1:-rinhaapi1}"
echo "Monitoring container $CONTAINER..."
dotnet run --rm -ti --pid=container:$CONTAINER $IMAGE
