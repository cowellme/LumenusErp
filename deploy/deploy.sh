#!/usr/bin/env bash
# Локальная часть деплоя: готовит данные и гоняет deploy/remote.sh на сервере по SSH.
# Использование: deploy/deploy.sh <setup|deploy|rollback|releases|backup|backup-pull|prod-logs|prod-ps|ssh>
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

die() { echo "ОШИБКА: $*" >&2; exit 1; }

# --- конфигурация ---
[[ -f .deploy.env ]] || die "нет файла .deploy.env — скопируйте .deploy.env.example и заполните"
set -a
# shellcheck disable=SC1091
source .deploy.env
set +a
[[ -n "${DEPLOY_HOST:-}" ]] || die "DEPLOY_HOST не задан в .deploy.env"
[[ -n "${DEPLOY_USER:-}" ]] || die "DEPLOY_USER не задан в .deploy.env"
DEPLOY_PORT="${DEPLOY_PORT:-22}"
DEPLOY_PATH="${DEPLOY_PATH:-/opt/lumenus}"
TARGET="$DEPLOY_USER@$DEPLOY_HOST"

SSH_OPTS=(-p "$DEPLOY_PORT")
SCP_OPTS=(-P "$DEPLOY_PORT")
if [[ -n "${DEPLOY_SSH_KEY:-}" ]]; then
  SSH_OPTS+=(-i "$DEPLOY_SSH_KEY")
  SCP_OPTS+=(-i "$DEPLOY_SSH_KEY")
fi

# Неинтерактивный ssh (без запроса пароля)
ssh_batch() { ssh -o BatchMode=yes "${SSH_OPTS[@]}" "$TARGET" "$@"; }

# Выполнить на сервере функцию из remote.sh: remote <команда> [аргументы...]
# remote.sh стримится через stdin, на сервер копировать его не нужно.
remote() {
  local args
  args="$(printf '%q ' "$DEPLOY_PATH" "$@")"
  ssh_batch "bash -s -- $args" < "$ROOT/deploy/remote.sh"
}

# То же, но с TTY (для follow-логов и т.п.): скрипт передаётся через base64,
# чтобы stdin остался терминалом.
remote_tty() {
  local args script
  args="$(printf '%q ' "$DEPLOY_PATH" "$@")"
  script="$(base64 -w0 < "$ROOT/deploy/remote.sh")"
  ssh -t "${SSH_OPTS[@]}" "$TARGET" "echo $script | base64 -d | bash -s -- $args"
}

cmd_setup() {
  remote setup
  # shared/.env создаём только если его ещё нет, существующий не трогаем
  if ssh_batch "test -f $(printf '%q' "$DEPLOY_PATH/shared/.env")"; then
    echo "shared/.env уже есть, не трогаю"
  else
    ssh_batch "umask 077 && cat > $(printf '%q' "$DEPLOY_PATH/shared/.env")" < "$ROOT/.env.example"
    cat <<MSG

################################################################
#  ВНИМАНИЕ: на сервере создан шаблон $DEPLOY_PATH/shared/.env
#  с ПАРОЛЯМИ ПО УМОЛЧАНИЮ. Обязательно отредактируйте его:
#    make ssh   # затем: nano shared/.env
#  (POSTGRES_PASSWORD, ADMIN_PASSWORD и т.д.) до первого make deploy.
################################################################
MSG
  fi
}

cmd_deploy() {
  local ref="${REF:-HEAD}" sha ts release
  if [[ -n "$(git status --porcelain)" && "${FORCE:-}" != "1" ]]; then
    die "рабочее дерево не чистое; закоммитьте изменения или запустите FORCE=1 make deploy"
  fi
  sha="$(git rev-parse --short "$ref")" || die "неизвестный ref: $ref"
  ts="$(date -u +%Y%m%d-%H%M%S)"
  release="$ts-$sha"
  echo "Деплой $ref ($sha) как релиз $release"

  remote preflight

  echo "Загрузка релиза на сервер..."
  git archive --format=tar "$ref" \
    | ssh_batch "mkdir -p $(printf '%q' "$DEPLOY_PATH/releases/$release") && tar -x -C $(printf '%q' "$DEPLOY_PATH/releases/$release")"

  remote deploy "$release" "${HEALTH_TIMEOUT:-90}" "${HEALTH_URL:-}"
}

cmd_backup_pull() {
  local name
  name="$(remote latest-backup)"
  [[ -n "$name" ]] || die "на сервере нет бэкапов"
  mkdir -p backups
  scp -o BatchMode=yes "${SCP_OPTS[@]}" "$TARGET:$DEPLOY_PATH/shared/backups/$name" "backups/$name"
  echo "Скачан backups/$name"
}

case "${1:-}" in
  setup)       cmd_setup ;;
  deploy)      cmd_deploy ;;
  rollback)    remote rollback "${HEALTH_TIMEOUT:-90}" "${HEALTH_URL:-}" ;;
  releases)    remote releases ;;
  backup)      remote backup ;;
  backup-pull) cmd_backup_pull ;;
  prod-logs)   remote_tty logs "${N:-200}" ;;
  prod-ps)     remote ps ;;
  ssh)         ssh -t "${SSH_OPTS[@]}" "$TARGET" "cd $(printf '%q' "$DEPLOY_PATH") && exec \$SHELL -l" ;;
  *)           die "неизвестная команда '${1:-}'; см. make help" ;;
esac
