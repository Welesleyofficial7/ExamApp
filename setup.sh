#!/bin/bash

set -e

# Путь к проектам
GATEWAY_PROJECT="ExamApp"
DOMAIN_PROJECT="DomainService"

# Папка публикации
GATEWAY_OUTPUT="./publish/$GATEWAY_PROJECT"
DOMAIN_OUTPUT="./publish/$DOMAIN_PROJECT"

# Очистка предыдущих сборок
echo "Очистка предыдущих сборок..."
rm -rf ./publish

# Сборка и публикация Gateway (ExamApp)
echo "Сборка и публикация Gateway (ExamApp)..."
dotnet publish $GATEWAY_PROJECT -o $GATEWAY_OUTPUT

# Сборка и публикация DomainService
echo "Сборка и публикация DomainService..."
dotnet publish $DOMAIN_PROJECT -o $DOMAIN_OUTPUT

# Сборка Docker-образов
echo "Создание Docker-образов..."
docker build -t examapp:latest ./ExamApp  # Указываем директорию с Dockerfile
docker build -t domainservice:latest ./DomainService  # Указываем директорию с Dockerfile

# Запуск контейнеров
echo "Запуск контейнеров через docker-compose..."
docker-compose up -d
