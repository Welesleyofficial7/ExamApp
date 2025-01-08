#!/bin/bash

docker-compose down

docker builder prune -f
docker image prune -f

dotnet publish -c Release -o ./publish/ExamApp
dotnet publish -c Release -o ./publish/DomainService

docker-compose build --no-cache

docker-compose up -d
