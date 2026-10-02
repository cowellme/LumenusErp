.DEFAULT_GOAL := help
.PHONY: help build up down logs ps setup deploy rollback releases backup backup-pull prod-logs prod-ps ssh

DEPLOY := deploy/deploy.sh

help: ## Показать список целей
	@grep -E '^[a-zA-Z_-]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  %-12s %s\n", $$1, $$2}'

build: ## Локально: docker compose build
	docker compose build

up: ## Локально: поднять стек (up -d --build)
	docker compose up -d --build

down: ## Локально: остановить стек
	docker compose down

logs: ## Локально: логи app (follow)
	docker compose logs -f app

ps: ## Локально: состояние контейнеров
	docker compose ps

setup: ## Сервер: первичная подготовка (каталоги, Docker, shared/.env)
	$(DEPLOY) setup

deploy: ## Сервер: выкатить HEAD (REF=<ref>, FORCE=1 для грязного дерева)
	$(DEPLOY) deploy

rollback: ## Сервер: откатиться на предыдущий релиз
	$(DEPLOY) rollback

releases: ## Сервер: список релизов (текущий отмечен)
	$(DEPLOY) releases

backup: ## Сервер: дамп БД в shared/backups
	$(DEPLOY) backup

backup-pull: ## Скачать свежий бэкап в ./backups/
	$(DEPLOY) backup-pull

prod-logs: ## Сервер: логи app (N=200 строк, follow)
	$(DEPLOY) prod-logs

prod-ps: ## Сервер: состояние контейнеров
	$(DEPLOY) prod-ps

ssh: ## Сервер: интерактивная оболочка в DEPLOY_PATH
	$(DEPLOY) ssh
