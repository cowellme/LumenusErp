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
    Layout/, Tools/             # layout, меню, MarkdownViewer, BpmnEditor, SeoMeta (мета/OG/JSON-LD), NoIndex
  SiteInfo.cs                   # BaseUrl (https://lumenustech.ru), e-mail, Telegram — единое место
  wwwroot/                      # llms.txt, llms-full.txt, robots.txt, sitemap.xml
  Controllers/LumenusController.cs
  Controllers/TasksController.cs, ApiTokenAttribute.cs   # api/tasks + Bearer-фильтр
  Data/
    ApplicationDbContext.cs, ApplicationUser.cs
    TaskItem.cs                 # задачи api/tasks (таблица TaskItems)
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
- ИИ-функции (`/calculator`, `/faq`) публичные и платные: `AiRateLimiter` (в памяти, скользящие окна) считает
  вызовы по IP клиента. IP берётся из `HttpContext` при пререндере (после `UseForwardedHeaders`) и переносится в
  интерактивную фазу через `PersistentComponentState` (`Components/Tools/AiPageBase.cs`). Лимиты задаются в `AiLimits`.
- Системные промпты ИИ лежат в таблице `AiPrompts` (ключи `estimate`, `faq`, `call-tasks`), правятся в `/admin/prompts`;
  читаются через `AiPromptStore` (кэш 30 с). Тексты по умолчанию — `Services/DefaultPrompts.cs`: сидятся только
  при отсутствии ключа и служат запасным вариантом.
- Секреты только из конфигурации/переменных окружения, никогда не в коде.
  `MySec.Configure` и `AiModule.Configure` вызываются в `Program.cs` после `Build()`.
  Пустой `Api:Token` = API закрыт; пустой ключ LLM = метод возвращает сообщение без вызова API.

## API задач (`api/tasks`) и трекер

Задачи принадлежат пользователю (`TaskItem.OwnerId`, FK на `AspNetUsers`, cascade): каждый видит только свои. Их присылает
внешний сервис myasi (распознавание речи) по docker-сети (`http://app:8080`) и создают вручную в трекере. Контракт api/tasks
зафиксирован на стороне myasi — имена полей, коды и пути не менять; поле владельца в ответах не отдаётся.
Модель — `Data/TaskItem.cs` (таблица `TaskItems`, `ApplicationDbContext`), контроллер — `Controllers/TasksController.cs`;
валидация, нормализация, идемпотентность и мягкое удаление — в `Services/TaskService.cs`, его используют и контроллер, и страница `/tasks`.

- Авторизация: `Authorization: Bearer <токен>`, фильтр `ApiTokenAttribute`. Владелец запроса кладётся в `HttpContext.Items`
  (`ApiOwner.Get`). Токен — либо **личный** (`lmn_…`, `UserApiTokenService.ValidateAsync`: поиск по SHA-256 хэшу в
  `UserApiTokens`, не отозван; `LastUsedAt` обновляется не чаще раза в минуту) — владелец = его пользователь; либо
  **переходный режим**: общий `Api:Token` (`MySec.IsValidToken`) — владелец = администратор (пользователь с e-mail из
  `Admin:Email`, иначе первый по e-mail в роли Admin; нет такого — 401). Нет/неверный токен или пустой `Api:Token` — **401**
  (не 400: myasi считает 400 окончательным отказом и теряет задачу). Фильтр срабатывает раньше валидации тела; редиректа на логин нет.
  `api/lumenus` по-прежнему только на общем токене. Общий токен для api/tasks планируется отключить (см. `TODO.md`).
- Чужая задача для GET/PATCH/DELETE = **404**; список, поиск дублей и `externalId` — только среди задач владельца
  (уникальный индекс `(OwnerId, Source, ExternalId)`, индекс дублей `(OwnerId, Source, Status, TitleNormalized)`).
- `GET /api/me` (тот же `[ApiToken]`) → `{"userId": "<Id пользователя>"}` — владелец токена, одинаков для всех его токенов;
  myasi по нему привязывает задачи в очереди к пользователю. Общий токен → Id администратора.
- `POST /api/tasks` (`title`, `source` обязательны; `sourceText`, `externalId`, `createdAt` нет) → 201 + `Location`;
  `GET /api/tasks?source=&status=open|done`; `GET|PATCH|DELETE /api/tasks/{id}` (PATCH: `{"status":"open|done"}`).
  DELETE мягкий (`DeletedAt`) → 204; удалённая задача для GET/PATCH/DELETE = 404 и в списках не видна.
- Идемпотентность POST (вместо 201 возвращается 200 и существующая задача): та же пара (`source`, `externalId`) у того же владельца,
  **включая мягко удалённую** (не воскрешается); либо неудалённая открытая задача того же владельца и `source` с тем же названием
  без учёта регистра и крайних пробелов (`TitleNormalized`). Гонка по уникальному индексу (23505) разруливается перечитыванием.
- Время — UTC с суффиксом `Z` (при отдаче `DateTime.SpecifyKind(..., Utc)` из-за legacy timestamp); `createdAt` с офсетом приводится к UTC.

Трекер (Blazor, `[Authorize]` — любой вошедший): `/tasks` (`Components/Pages/TaskManager.razor`) — свои задачи, фильтр
Открытые/Выполненные/Все, отметка выполнения, правка названия, мягкое удаление, ручное создание (`Source = "web"`),
исходный текст myasi по клику, время в часовом поясе браузера (смещение берётся через `wwwroot/js/tasks.js`).
`/tasks/tokens` (`TaskTokens.razor`) — личные токены: создать (полный токен показывается один раз, в БД только хэш), отозвать;
токен прописывается в myasi как `Authorization: Bearer <токен>`. Оба пути под `noindex` (`PrivatePrefixes` в `MainLayout`).

Поддомен: `Tasks:Host` (на проде `app.lumenustech.ru`, env `TASKS_HOST`). Если задан: на этом хосте `GET /` → 302 `/tasks`, на любом другом
`/tasks` и `/tasks/*` → 404 (middleware в `Program.cs` после `UseForwardedHeaders`; `UseAuthentication/UseAuthorization` вызваны
явно после него, иначе автоматические отдали бы анониму редирект на логин раньше 404). `/api/*`, `/Account/*`, статика и `/_blazor`
не затрагиваются, поэтому myasi работает с `app:8080`. Пусто — без ограничений (локальная разработка). В `deploy/Caddyfile` — блок `app.{$SITE_DOMAIN}`
(нужна DNS-запись) и HTTP→HTTPS редирект на тот же хост.

## Вкладка «Созвоны» (`/tasks/calls`)

Вкладки трекера (`Components/Tools/TasksTabs.razor`): Задачи, Созвоны, API-токены и (только Admin) «Промпты ИИ» → `/admin/prompts`
(`/admin` на хосте `Tasks:Host` не блокируется, middleware трогает только `/tasks*`).
Поток: пользователь грузит видео/аудио → ffmpeg (в образе) извлекает аудио → myasi распознаёт речь → LLM (OpenRouter) выделяет
задачи → на `/tasks/calls/{id}` пользователь отмечает найденные и добавляет в свой трекер.
- Загрузка: `POST /tasks/calls/upload` (`Services/CallEndpoints.cs`, cookie-авторизация, путь под `/tasks` — действует `Tasks:Host`).
  `multipart/form-data`, поле `file`; тело читается потоком `MultipartReader` прямо на диск (в память не буферизуется), лимит Kestrel
  поднимается только для этого запроса (`IHttpMaxRequestBodySizeFeature` = `Calls:MaxBytes` + 1 МБ), глобальный остаётся 30 МБ.
  Расширение из белого списка (webm, mp4, mkv, mov, avi, m4a, mp3, ogg, oga, opus, wav, flac, aac, wma, 3gp), остальное проверяет ffmpeg.
  Antiforgery: страница без пререндера не имеет `HttpContext`, поэтому `calls.js` (XMLHttpRequest, нужен прогресс) сначала берёт токен у
  `GET /tasks/calls/antiforgery` и шлёт его в заголовке `X-CSRF-TOKEN` (`AddAntiforgery(HeaderName)`, проверка `IAntiforgery.ValidateRequestAsync`).
  Не больше 2 записей пользователя в `queued/processing` (иначе 429). Ответ 201 `{"id"}`.
- Обработка (`Services/CallProcessor.cs`, `BackgroundService` + `CallQueue` на `Channel<Guid>`) — по одной записи за раз. ffmpeg режет аудио
  на WAV 16 кГц моно s16 по 10 минут (лимит myasi ~60 МБ на запрос); куски по очереди идут в служебный `POST {Myasi:BaseUrl}/api/transcribe` с `Authorization: Bearer {Myasi:Token}` (= `TRANSCRIBE_TOKEN` myasi; только текст, без задач и без отправки в Lumenus)
  (таймаут 15 минут на кусок), тексты склеиваются пробелом. Транскрипт сохраняется сразу после распознавания (виден и при сбое LLM).
  Задачи — промпт `call-tasks` (`/admin/prompts`), ответ — JSON-массив `[{"title","quote"}]`, разбор в `CallTaskParser`; транскрипт длиннее
  120 000 символов обрезается для модели. Нет ключа OpenRouter → статус `done` с `Error` «ИИ не настроен — задачи не выделены».
- Рабочий каталог — `Calls:WorkPath` (по умолчанию `<Media:Path>/calls-tmp`, то есть том `uploads`; в нём подкаталог на запись). Исходный файл
  удаляется после конвертации, каталог записи — всегда в `finally`.
- Рестарт: очередь в памяти, поэтому при старте все `queued/processing` помечаются `failed` («Обработка прервана перезапуском сервера»),
  а содержимое рабочего каталога удаляется.
- Модель: `CallRecording` (статусы `queued/processing/done/failed`, `Stage` — шаг для UI) и `CallTaskSuggestion` (`TaskItemId` — созданная задача,
  FK SetNull). Удаление записи физическое (подсказки каскадом, задачи трекера остаются); `processing` удалить нельзя.
- Добавление в трекер (`CallService.AddToTrackerAsync`): `Source = "call"` (`TaskService.CallSource`), `ExternalId = "<callId>:<suggestionId>"`
  (повтор не плодит дубли), `SourceText` = цитата + пустая строка + «Созвон: <имя файла>».
- Caddy: лимитов тела запроса (`request_body`) и таймаутов в `deploy/Caddyfile` нет, загрузка 2 ГБ проходит.

## English Studio (`/english`)

Образовательная платформа преподавателя английского и учеников: тот же стек (Blazor Server, Identity, PostgreSQL).
Подробности — `docs/english-studio/README.md`.

- Роли Identity `Teacher` и `Student` (сидятся в `Program.cs`). Ученик привязан к одному преподавателю
  (`EnglishProfile.TeacherId`) и видит только опубликованный контент этого преподавателя.
- Регистрация и вход — `/english/login` (static SSR, нужен `HttpContext`), публичный лендинг с заявкой — `/english`.
  `English:AllowTeacherSignup` (по умолчанию `true`) закрывает регистрацию преподавателей.
- Кабинет: уроки и конструктор, тесты и конструктор, домашние задания и проверка, календарь, прогресс и достижения,
  словарь и практика, библиотека материалов, чат, ученики, группы, программы, права доступа, профиль. Все пути под `noindex`.
- Модели — `Data/English/Models.cs`, конфигурация в `ConfigureEnglish` (`ApplicationDbContext`), миграция `EnglishStudio`.
  Сервисы — `Services/English/*` (scoped, через `IDbContextFactory`), страницы наследуют `Components/English/EnglishPageBase`.
- Стили — `wwwroot/english.css` (префикс `es-`), озвучивание слов — `wwwroot/js/english.js`.
  Фото уроков хранятся как `MediaFile` и отдаются через `/media/{id}` с проверкой прав.
- Папки для материалов: `wwwroot/english/images/`, `wwwroot/english/backgrounds/`.

## Конфигурация

| Ключ | Назначение |
|---|---|
| `ConnectionStrings:DefaultConnection` | БД Identity |
| `ConnectionStrings:AosConnection` | БД AOS (`tracker`) |
| `Admin:Email`, `Admin:Password` | админ, создаваемый при первом старте |
| `DataProtection:KeysPath` | каталог ключей Data Protection (куки переживают рестарт) |
| `Api:Token` | Bearer-токен для `api/lumenus`; для `api/tasks` — переходный (задачи пишутся администратору) |
| `Tasks:Host` | хост трекера (`app.lumenustech.ru`); пусто = без ограничений по хосту |
| `Myasi:BaseUrl` | сервис распознавания речи myasi для «Созвонов» (по умолчанию `http://myasi:8000`; env `MYASI_URL`) |
| `Myasi:Token` | служебный токен `POST /api/transcribe` myasi, совпадает с его `TRANSCRIBE_TOKEN` (env `MYASI_TOKEN`) |
| `Calls:MaxBytes` | максимум размера записи созвона, по умолчанию 2 ГБ (env `CALLS_MAX_BYTES`) |
| `Calls:WorkPath` | каталог временных файлов созвонов, по умолчанию `<Media:Path>/calls-tmp` |
| `Ai:OpenRouterApiKey`, `Ai:DeepSeekApiKey` | ключи LLM |
| `Ai:OpenRouterModel` | модель OpenRouter для калькулятора и FAQ (пусто = `anthropic/claude-sonnet-5.5`) |
| `Ai:Yandex:AccessKeyId`, `Ai:Yandex:SecretAccessKey`, `Ai:Yandex:FolderId` | YandexGPT |
| `AiLimits:CalculatorPerHour`, `CalculatorPerDay`, `FaqPerHour`, `FaqPerDay` | лимиты ИИ на IP клиента (по умолчанию 5/15 и 20/60); админы без лимита |
| `AiLimits:CalculatorGlobalPerDay`, `FaqGlobalPerDay` | суточный потолок на весь сервис (по умолчанию 300 и 1000) |
| `English:AllowTeacherSignup` | разрешена ли самостоятельная регистрация преподавателей (по умолчанию `true`) |
| `Media:Path` | каталог загрузок (в Docker `/app/uploads`, том `uploads`; локально `<ContentRoot>/uploads`) |
| `Media:MaxBytes` | максимум размера картинки, по умолчанию 10 МБ |
| `DisableHttpsRedirection` | `true` за reverse proxy, где TLS снимает nginx |

Переменные `.env` для compose: `APP_PORT`, `APP_BIND` (адрес публикации порта приложения;
`127.0.0.1` на проде за Caddy), `SITE_DOMAIN`, `ACME_EMAIL` (пусто = `info@lumenustech.ru`),
`TASKS_HOST` (хост трекера; пусто локально, на проде `app.lumenustech.ru`), `MYASI_URL`, `MYASI_TOKEN`, `CALLS_MAX_BYTES`, `COMPOSE_PROFILES=proxy` (включает сервис `caddy`).

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

## Контентные страницы (/p/{slug})

Администратор собирает страницы из блоков (текст markdown, BPMN-диаграмма, фото) в `/admin/pages`.
Модели `ContentPage`/`ContentBlock`/`MediaFile` в `ApplicationDbContext`. Видимость: Draft (только Admin, остальным
404), Authenticated (анониму редирект на логин), Public (попадает в sitemap.xml, llms.txt, llms-full.txt).
- Картинки: JPEG/PNG/WebP/GIF, тип проверяется по сигнатуре, SVG запрещён; на диске под именем Guid+расширение.
  Отдаёт `GET /media/{id}` (nosniff, immutable-кэш для публичных; закрытые файлы по правам страницы, иначе 404).
  Удаление блока/страницы удаляет файл, если на него больше нет ссылок; забытые загрузки старше часа чистятся при сохранении.
- BPMN: bpmn-js 18.31.0 лежит в `wwwroot/lib/bpmn-js` (обновление: скачать tarball из npm, скопировать
  `dist/bpmn-navigated-viewer.production.min.js`, `bpmn-modeler.production.min.js`, `dist/assets`, поправить `VERSION`
  в `wwwroot/js/bpmn-view.js` и `bpmn-editor.js`). Просмотр — `bpmn-view.js` (NavigatedViewer, грузится лениво, работает
  после enhanced navigation), редактор — `BpmnEditor.razor` + `bpmn-editor.js` (Modeler). XML вставляется в страницу как
  JSON в `<script type="application/json">` (`<`, `>`, `&` экранированы).
  Флаг `ContentBlock.BpmnFixed` (чекбокс в редакторе) — статичная схема: `bpmn-view.js` подменяет модули zoomScroll/moveCanvas/keyboardMove заглушками, без кнопки «Вписать».

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
делается `pg_dumpall` и архив тома загрузок (`shared/backups/<ts>-uploads.tgz`, том `lumenus_uploads`; `backup-pull` качает только SQL); при провале проверки `current` не переключается, поднимается
предыдущий релиз. Миграции БД при этом не откатываются.

## Деплой на Ubuntu вручную

1. Установить Docker Engine + compose plugin (`https://docs.docker.com/engine/install/ubuntu/`).
2. `git clone` репозитория, `cp .env.example .env`, задать `POSTGRES_PASSWORD`, `ADMIN_PASSWORD`.
3. `docker compose up -d --build`. Приложение слушает `APP_PORT` (по умолчанию 8080).
4. TLS — Caddy из compose (профиль `proxy`): в `.env` задать `COMPOSE_PROFILES=proxy`,
   `APP_BIND=127.0.0.1`, `SITE_DOMAIN`, `ACME_EMAIL`; порты 80/443 должны быть свободны. Конфиг —
   `deploy/Caddyfile`: домен → приложение (Let's Encrypt, HSTS), `www.` → редирект на апекс,
   HTTP по голому IP проксируется на приложение (работает до настройки DNS). `/myasi/*` на основном домене
   проксируется (без префикса) в сервис myasi (`myasi:8000`, отдельный compose-проект в сети `lumenus_default`;
   пока он не запущен — 502). Для `make deploy`
   те же переменные задаются в `shared/.env` (compose читает `COMPOSE_PROFILES` из env-файла).
5. Бэкап: `docker compose exec db pg_dumpall -U "$POSTGRES_USER" > backup.sql`; загрузки — том `uploads` (`/app/uploads`).
