# LumenusErp

ERP/сайт компании Lumenus (lumenustech.ru): лендинг, FAQ и калькулятор смет с LLM,
админка ролей, модуль анализа соцсетей блогеров (AOS).

## Стек

- .NET 9, ASP.NET Core, **Blazor Server** (Interactive Server render mode, не WASM)
- ASP.NET Core Identity (cookie), роли: `Admin`, `Manager`, `User`, `Ghost`, `Aos`
- EF Core 9 + **PostgreSQL** (Npgsql), две БД:
  - `lumenuserp` — `ApplicationDbContext` (Identity)
  - `tracker` — `AosDbContext` (блогеры, настройки AOS, пользователи бота)
- Swagger (`/swagger`), один API-контроллер `api/lumenus` с Bearer-токеном
- Docker + docker compose (приложение + postgres)

## Структура

```
LumenusErp/                     # проект (LumenusErp.csproj)
  Program.cs                    # DI, миграции при старте, сид ролей и админа
  Components/
    Pages/                      # Home, Faq, Calculator, Projects, ProjectDetails, Auth
    Pages/Admin/                # /admin/roles, /admin/projects — ролями и проектами (Admin)
    Pages/AnalysisOfSocial/     # /aos-panel, /aos-settings (Admin, Aos)
    Account/                    # шаблонные страницы Identity
    Layout/, Tools/             # layout, меню, MarkdownViewer, SeoMeta (мета/OG/JSON-LD), NoIndex
  SiteInfo.cs                   # BaseUrl (https://lumenustech.ru), e-mail, Telegram — единое место
  wwwroot/                      # llms.txt, llms-full.txt, robots.txt, sitemap.xml
  Controllers/LumenusController.cs
  Data/
    ApplicationDbContext.cs, ApplicationUser.cs
    AosDbContext.cs
    Aos/                        # модели Blogger, Setting, TUser, Social (namespace Shared.Models)
    Migrations/App, Migrations/Aos
  Content/                      # шаблоны llms.txt / llms-full.txt
  Services/                     # SeoEndpoints, клиенты LLM: OpenRouter, DeepSeek, YandexGPT (AiModule)
Dockerfile, docker-compose.yml, .env.example
deploy/                         # deploy.sh, remote.sh, Caddyfile
```

## Важное

- Модели AOS (`Data/Aos`) **восстановлены по использованию в коде**: исходный проект
  `Lumenus/Shared` в репозиторий не попал. БД `tracker` рассчитана на общий доступ с
  внешним сервисом (бот), поэтому при правке моделей сверять схему с ним.
- Схема меняется только через EF-миграции; при старте вызывается `Database.MigrateAsync()`
  для обоих контекстов. `EnsureDeleted/EnsureCreated` не использовать.
- `Npgsql.EnableLegacyTimestampBehavior` включён: `DateTime` пишется как `timestamp`
  без требования `Kind=Utc`.
- Секреты только из конфигурации/переменных окружения, никогда не в коде.
  `MySec.Configure` и `AiModule.Configure` вызываются в `Program.cs` после `Build()`.
  Пустой `Api:Token` = API закрыт; пустой ключ LLM = метод возвращает сообщение без вызова API.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings:DefaultConnection` | БД Identity |
| `ConnectionStrings:AosConnection` | БД AOS (`tracker`) |
| `Admin:Email`, `Admin:Password` | админ, создаваемый при первом старте |
| `DataProtection:KeysPath` | каталог ключей Data Protection (куки переживают рестарт) |
| `Api:Token` | Bearer-токен для `api/lumenus` |
| `Ai:OpenRouterApiKey`, `Ai:DeepSeekApiKey` | ключи LLM |
| `Ai:Yandex:AccessKeyId`, `Ai:Yandex:SecretAccessKey`, `Ai:Yandex:FolderId` | YandexGPT |
| `DisableHttpsRedirection` | `true` за reverse proxy, где TLS снимает nginx |

Переменные `.env` для compose: `APP_PORT`, `APP_BIND` (адрес публикации порта приложения;
`127.0.0.1` на проде за Caddy), `SITE_DOMAIN`, `ACME_EMAIL` (пусто = `info@lumenustech.ru`),
`COMPOSE_PROFILES=proxy` (включает сервис `caddy`).

## SEO

- Базовый URL сайта — `SiteInfo.BaseUrl`; на публичных страницах (`/`, `/projects`, `/faq`)
  `<PageTitle>` + `<SeoMeta Path=... />` (description, canonical, OG, Twitter, JSON-LD).
  Контент JSON-LD строится из тех же данных, что рисует страница.
- `MainLayout` добавляет `noindex` для `Account*`, `admin*`, `aos-*`, `auth`, `calculator`, `Error`.
- `/sitemap.xml`, `/llms.txt`, `/llms-full.txt` — **динамические** эндпоинты (`Services/SeoEndpoints.cs`):
  sitemap строится из БД (/, /projects, /faq + опубликованные проекты, lastmod = `UpdatedAt`);
  llms-файлы = шаблон `Content/llms.txt`, `Content/llms-full.txt` (копируются в publish через csproj)
  + сгенерированный раздел «Projects». Статические тексты страниц в шаблонах правятся вручную.
  `wwwroot/robots.txt` статический; `.txt` из статики отдаётся с `charset=utf-8` (middleware в `Program.cs`).
- Проекты реестра — таблица `Projects` (`Data/Project.cs`), первичное наполнение — `Data/ProjectSeed.cs`
  (только в пустую таблицу, правки админа не перезаписываются). Публично: `/projects`,
  `/projects/{slug}` (404 с реальным статусом для неизвестных/неопубликованных); редактор — `/admin/projects`
  (роль Admin). Markdown проектов рендерится с отключённым сырым HTML (`MarkdownViewer Safe="true"`).
- `ApplicationDbContext` регистрируется через `AddDbContextFactory` + scoped-обёртку: компоненты Blazor
  берут `IDbContextFactory`, Identity — обычный контекст.

В Docker ключи задаются переменными окружения (`ConnectionStrings__DefaultConnection` и т.д.)
из `.env`.

## Команды

Локального SDK может не быть — всё через Docker.

```bash
# запуск стека
cp .env.example .env            # задать пароли
docker compose up -d --build
docker compose logs -f app

# сборка без запуска
docker compose build

# новая миграция (пример для Identity-контекста)
docker run --rm --user $(id -u):$(id -g) -e HOME=/tmp -v "$PWD":/src -w /src/LumenusErp \
  mcr.microsoft.com/dotnet/sdk:9.0 sh -c \
  "dotnet tool restore && dotnet ef migrations add <Name> --context ApplicationDbContext -o Data/Migrations/App"
# для AOS: --context AosDbContext -o Data/Migrations/Aos

# psql
docker compose exec db psql -U "$POSTGRES_USER" -d lumenuserp
```

## Деплой на прод (make)

Параметры сервера — в `.deploy.env` (шаблон `.deploy.env.example`, в git не попадает).
Логика: `deploy/deploy.sh` (локально) + `deploy/remote.sh` (стримится на сервер по SSH).

```bash
make setup      # первый раз: каталоги, Docker, шаблон shared/.env (потом отредактировать!)
make deploy     # выкатить закоммиченный HEAD (REF=<ref>, FORCE=1 при грязном дереве)
make rollback   # на предыдущий релиз
make releases | backup | backup-pull | prod-logs | prod-ps | ssh
```

На сервере: `$DEPLOY_PATH/{shared/.env, shared/backups, releases/<ts>-<sha>, current}`.
Compose всегда с `-p lumenus`, поэтому тома общие для всех релизов. Перед деплоем
делается `pg_dumpall`; при провале проверки `current` не переключается, поднимается
предыдущий релиз. Миграции БД при этом не откатываются.

## Деплой на Ubuntu вручную

1. Установить Docker Engine + compose plugin (`https://docs.docker.com/engine/install/ubuntu/`).
2. `git clone` репозитория, `cp .env.example .env`, задать `POSTGRES_PASSWORD`, `ADMIN_PASSWORD`.
3. `docker compose up -d --build`. Приложение слушает `APP_PORT` (по умолчанию 8080).
4. TLS — Caddy из compose (профиль `proxy`): в `.env` задать `COMPOSE_PROFILES=proxy`,
   `APP_BIND=127.0.0.1`, `SITE_DOMAIN`, `ACME_EMAIL`; порты 80/443 должны быть свободны. Конфиг —
   `deploy/Caddyfile`: домен → приложение (Let's Encrypt, HSTS), `www.` → редирект на апекс,
   HTTP по голому IP проксируется на приложение (работает до настройки DNS). Для `make deploy`
   те же переменные задаются в `shared/.env` (compose читает `COMPOSE_PROFILES` из env-файла).
5. Бэкап: `docker compose exec db pg_dumpall -U "$POSTGRES_USER" > backup.sql`.
