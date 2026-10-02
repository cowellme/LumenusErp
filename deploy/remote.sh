#!/usr/bin/env bash
# Серверная часть деплоя. Запускается по SSH: bash -s -- <DEPLOY_PATH> <команда> [аргументы...]
set -euo pipefail

BASE="$1"; CMD="$2"; shift 2
SHARED="$BASE/shared"
RELEASES="$BASE/releases"
CURRENT="$BASE/current"
KEEP_RELEASES=5
KEEP_BACKUPS=14

die() { echo "ОШИБКА: $*" >&2; exit 1; }

# docker compose с фиксированным именем проекта (тома переживают смену каталога релиза)
# и общим env-файлом. Первый аргумент — каталог релиза.
dc() {
  local rel="$1"; shift
  docker compose -p lumenus --env-file "$SHARED/.env" -f "$rel/docker-compose.yml" "$@"
}

# Имена записей каталога по возрастанию (имена релизов и бэкапов начинаются с UTC-времени)
list_names() {
  find "$1" -mindepth 1 -maxdepth 1 -printf '%f\n' 2>/dev/null | sort || true
}

# Из stdin выводит все строки, кроме последних N
all_but_last() {
  awk -v k="$1" '{ a[NR] = $0 } END { for (i = 1; i <= NR - k; i++) print a[i] }'
}

# Имя текущего релиза (пусто, если current нет)
current_name() {
  [[ -L "$CURRENT" ]] && basename "$(readlink "$CURRENT")" || true
}

app_port() {
  local p
  p="$(grep -E '^APP_PORT=' "$SHARED/.env" | tail -n1 | cut -d= -f2- | tr -d '\r"'"'"' ' || true)"
  echo "${p:-8080}"
}

# Атомарное переключение current на релиз
switch_current() {
  ln -sfn "releases/$1" "$BASE/current.tmp"
  mv -T "$BASE/current.tmp" "$CURRENT"
}

# Поднять релиз и дождаться ответа приложения. $2 — таймаут (с), $3 — URL (по умолчанию localhost:APP_PORT)
activate() {
  local name="$1" timeout="${2:-90}" url="${3:-}" waited=0
  [[ -n "$url" ]] || url="http://127.0.0.1:$(app_port)/"
  dc "$RELEASES/$name" up -d --build --remove-orphans || return 1
  echo "Проверка доступности $url (до ${timeout}с)..."
  while ! curl -fsS -o /dev/null "$url" 2>/dev/null; do
    waited=$((waited + 2))
    [[ $waited -le $timeout ]] || return 1
    sleep 2
  done
}

cmd_setup() {
  local sudo="" codename
  [[ "$(id -u)" -eq 0 ]] || sudo="sudo"
  $sudo mkdir -p "$SHARED/backups" "$RELEASES"
  $sudo chown -R "$(id -un):$(id -gn)" "$BASE"

  if command -v docker >/dev/null 2>&1; then
    echo "Docker уже установлен: $(docker --version)"
  else
    echo "Установка Docker Engine..."
    $sudo apt-get update
    $sudo apt-get install -y ca-certificates curl
    $sudo install -m 0755 -d /etc/apt/keyrings
    $sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    $sudo chmod a+r /etc/apt/keyrings/docker.asc
    # shellcheck disable=SC1091
    codename="$(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")"
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $codename stable" \
      | $sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
    $sudo apt-get update
    $sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    if [[ -n "$sudo" ]]; then
      $sudo usermod -aG docker "$(id -un)"
      echo "Пользователь добавлен в группу docker: переподключитесь по SSH, чтобы права применились."
    fi
  fi
  echo "Каталоги готовы: $BASE"
}

cmd_preflight() {
  [[ -f "$SHARED/.env" ]] || die "нет $SHARED/.env — сначала выполните make setup"
  docker compose version >/dev/null 2>&1 || die "docker compose недоступен — выполните make setup"
}

cmd_backup() {
  local cur ts out
  cur="$(current_name)"
  [[ -n "$cur" ]] || { echo "Релизов ещё нет, бэкап пропущен"; return 0; }
  if ! dc "$RELEASES/$cur" ps --status running --services 2>/dev/null | grep -qx db; then
    echo "Сервис db не запущен, бэкап пропущен"
    return 0
  fi
  mkdir -p "$SHARED/backups"
  ts="$(date -u +%Y%m%d-%H%M%S)"
  out="$SHARED/backups/$ts.sql.gz"
  # Пишем во временный файл, чтобы неудачный дамп не оставил битый бэкап
  # $POSTGRES_USER раскрывается внутри контейнера, поэтому одинарные кавычки намеренны
  # shellcheck disable=SC2016
  if dc "$RELEASES/$cur" exec -T db sh -c 'pg_dumpall -U "$POSTGRES_USER"' | gzip > "$out.part"; then
    mv "$out.part" "$out"
    echo "Бэкап: $out ($(du -h "$out" | cut -f1))"
  else
    rm -f "$out.part"
    die "не удалось сделать бэкап БД"
  fi
  backup_uploads "$ts"
  # Оставляем только свежие KEEP_BACKUPS
  list_names "$SHARED/backups" | grep '\.sql\.gz$' | all_but_last "$KEEP_BACKUPS" \
    | while read -r b; do rm -f -- "$SHARED/backups/$b"; done
  list_names "$SHARED/backups" | grep -- '-uploads\.tgz$' | all_but_last "$KEEP_BACKUPS" \
    | while read -r b; do rm -f -- "$SHARED/backups/$b"; done
}

# Архив тома с загруженными файлами (compose-проект lumenus => том lumenus_uploads)
backup_uploads() {
  local out="$SHARED/backups/$1-uploads.tgz"
  if ! docker volume inspect lumenus_uploads >/dev/null 2>&1; then
    echo "Тома lumenus_uploads ещё нет, архив загрузок пропущен"
    return 0
  fi
  if docker run --rm -v lumenus_uploads:/u:ro alpine tar czf - -C /u . > "$out.part"; then
    mv "$out.part" "$out"
    echo "Бэкап загрузок: $out ($(du -h "$out" | cut -f1))"
  else
    rm -f "$out.part"
    die "не удалось сделать архив загрузок"
  fi
}

cmd_latest_backup() {
  list_names "$SHARED/backups" | grep '\.sql\.gz$' | tail -n1 || true
}

prune_releases() {
  local cur
  cur="$(current_name)"
  list_names "$RELEASES" | all_but_last "$KEEP_RELEASES" | while read -r r; do
    if [[ "$r" != "$cur" ]]; then rm -rf -- "${RELEASES:?}/$r"; fi
  done
}

cmd_deploy() {
  local name="$1" timeout="${2:-90}" url="${3:-}" prev
  prev="$(current_name)"
  [[ -d "$RELEASES/$name" ]] || die "релиз $name не загружен"

  cmd_backup
  if activate "$name" "$timeout" "$url"; then
    switch_current "$name"
    prune_releases
    echo "OK: релиз $name выкачен"
  else
    echo "Деплой $name не прошёл проверку. Последние логи app:" >&2
    dc "$RELEASES/$name" logs --tail 50 app >&2 || true
    if [[ -n "$prev" ]]; then
      echo "Возврат на предыдущий релиз $prev..." >&2
      activate "$prev" "$timeout" "$url" || echo "ВНИМАНИЕ: предыдущий релиз $prev тоже не отвечает" >&2
    fi
    exit 1
  fi
}

cmd_rollback() {
  local timeout="${1:-90}" url="${2:-}" cur prev
  cur="$(current_name)"
  [[ -n "$cur" ]] || die "нет текущего релиза"
  prev="$(list_names "$RELEASES" | grep -B1 -Fx "$cur" | head -n1 || true)"
  [[ -n "$prev" && "$prev" != "$cur" ]] || die "нет релиза раньше $cur, откатываться некуда"

  echo "Откат $cur -> $prev"
  if activate "$prev" "$timeout" "$url"; then
    switch_current "$prev"
    echo "OK: откат на $prev выполнен"
  else
    echo "Откат не прошёл проверку, возвращаю $cur" >&2
    dc "$RELEASES/$prev" logs --tail 50 app >&2 || true
    activate "$cur" "$timeout" "$url" || true
    exit 1
  fi
}

cmd_releases() {
  local cur r
  cur="$(current_name)"
  list_names "$RELEASES" | while read -r r; do
    if [[ "$r" == "$cur" ]]; then echo "* $r (current)"; else echo "  $r"; fi
  done
}

# Рабочий релиз для команд ps/logs
active_dir() {
  [[ -L "$CURRENT" ]] || die "нет текущего релиза"
  echo "$RELEASES/$(current_name)"
}

case "$CMD" in
  setup)         cmd_setup ;;
  preflight)     cmd_preflight ;;
  deploy)        cmd_deploy "$@" ;;
  rollback)      cmd_rollback "$@" ;;
  releases)      cmd_releases ;;
  backup)        cmd_backup ;;
  latest-backup) cmd_latest_backup ;;
  ps)            dc "$(active_dir)" ps ;;
  logs)          dc "$(active_dir)" logs -f --tail "${1:-200}" app ;;
  *)             die "неизвестная команда: $CMD" ;;
esac
