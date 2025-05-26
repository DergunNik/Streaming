#!/bin/bash

set -e

DOCKER_HUB_USER="dergunnik"
TAG="latest"

SERVICE_DIRS=(
  "AccountService"
  "AuthService"
  "EmailService"
  "ApiGateway"
  "LiveService"
  "VoDService"
)

REPO_NAMES=(
  "streaming-account-service"
  "streaming-auth-service"
  "streaming-email-service"
  "streaming-gateway"
  "streaming-live-service"
  "streaming-vod-service"
)

if [ ${#SERVICE_DIRS[@]} -ne ${#REPO_NAMES[@]} ]; then
  echo "ОШИКА: Количество директорий сервисов (${#SERVICE_DIRS[@]}) не совпадает с количеством имен репозиториев (${#REPO_NAMES[@]})."
  exit 1
fi

pids=()

echo "Начинаю параллельную сборку и отправку сервисов..."

for i in "${!SERVICE_DIRS[@]}"; do
  DIR_NAME="${SERVICE_DIRS[$i]}"
  REPO_NAME="${REPO_NAMES[$i]}"
  FULL_REPO_NAME="$DOCKER_HUB_USER/$REPO_NAME:$TAG"

  echo "--- Запускаю сборку и отправку для директории: $DIR_NAME (репозиторий: $FULL_REPO_NAME) ---"
  (
    set -e
    echo "Собираю $DIR_NAME..."
    docker build -t "$FULL_REPO_NAME" "./$DIR_NAME/"
    echo "Отправляю $REPO_NAME..."
    docker push "$FULL_REPO_NAME"
    echo "--- $DIR_NAME (репозиторий: $REPO_NAME): сборка и отправка завершены ---"
  ) & 
  pids+=($!) 
done

echo "Ожидаю завершения всех задач..."
completed_count=0
error_count=0

for pid in "${pids[@]}"; do
  if wait "$pid"; then
    echo "Задача с PID $pid успешно завершена."
    ((completed_count++))
  else
    echo "ОШИБКА: Задача с PID $pid завершилась с ошибкой."
    ((error_count++))
  fi
done

echo "----------------------------------------------------"
echo "Всего задач: ${#SERVICE_DIRS[@]}"
echo "Успешно завершено: $completed_count"
echo "Завершено с ошибками: $error_count"
echo "----------------------------------------------------"

if [ "$error_count" -gt 0 ]; then
  exit 1
fi

exit 0