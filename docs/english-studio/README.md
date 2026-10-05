# English Studio — база знаний модуля

Образовательная платформа преподавателя английского и его учеников внутри LumenusErp.
Тот же стек, что и у остального проекта: .NET 9, Blazor Server (Interactive Server), ASP.NET Core Identity,
PostgreSQL через EF Core, деплой в Docker.

## Роли и доступ

| Роль Identity | Кто это | Что видит |
|---|---|---|
| `Teacher` | преподаватель | свои уроки, тесты, задания, материалы, своих учеников и группы |
| `Student` | ученик | опубликованный контент **своего** преподавателя, свой прогресс, словарь и чат |

- Ученик привязан к одному преподавателю: `EnglishProfile.TeacherId`.
- Ученик регистрируется сам на `/english/login`, преподаватель добавляет его по e-mail на `/english/students`.
- Регистрацию преподавателей можно закрыть: `English:AllowTeacherSignup = false` (по умолчанию `true`).
- Пользователь без роли English, зашедший на `/english/*`, выбирает роль на той же странице входа («режим присоединения»).

## Маршруты

Публичные:

| Путь | Назначение |
|---|---|
| `/english` | лендинг: о платформе, форматы, FAQ, заявка на пробный урок |
| `/english/login` | вход и регистрация с выбором роли (static SSR — нужен `HttpContext`) |

Кабинет (`EnglishLayout`, `noindex`):

| Путь | Роль | Назначение |
|---|---|---|
| `/english/home` | обе | дашборд: прогресс ученика / сводка преподавателя |
| `/english/lessons`, `/english/lessons/{id}` | обе | библиотека уроков и сам урок (теория, видео, практика, заметки) |
| `/english/lessons/new`, `/english/lessons/{id}/edit` | Teacher | конструктор уроков: блоки, фото, публикация |
| `/english/tests`, `/english/tests/{id}` | обе | тесты; ученик проходит, преподаватель смотрит предпросмотр |
| `/english/tests/new`, `/english/tests/{id}/edit` | Teacher | конструктор тестов |
| `/english/homework` | обе | назначение заданий / сдача работ |
| `/english/review` | Teacher | проверка работ и результаты тестов |
| `/english/vocabulary`, `/english/practice` | Student | словарь с карточками, упражнения и диктант |
| `/english/calendar` | обе | месяц и список: события и сроки заданий |
| `/english/progress` | обе | прогресс ученика (отчёт скачивается) / аналитика по всем ученикам |
| `/english/achievements` | Student | XP, уровень, достижения |
| `/english/materials` | обе | библиотека материалов, избранное |
| `/english/messages` | обе | чат «ученик ↔ преподаватель» |
| `/english/students`, `/english/students/{id}` | Teacher | ученики, карточка ученика (работы, навыки, заметки) |
| `/english/groups`, `/english/programs`, `/english/access` | Teacher | группы, учебные программы, права учеников |
| `/english/profile` | обе | профиль и настройки |

## Данные

Модели — `LumenusErp/Data/English/Models.cs`, все в `ApplicationDbContext` (БД `lumenuserp`),
конфигурация — `ConfigureEnglish` в `ApplicationDbContext.OnModelCreating`, миграция `EnglishStudio`.

- `EnglishProfile` — профиль (PK = `UserId`), преподаватель ученика, навыки 0–100, права
  `AllowRecordings` / `AllowDownloads` / `AllowRetakes`, недельная цель.
- `EnglishProgram` → `EnglishLesson` → `EnglishLessonBlock`; прогресс ученика — `EnglishLessonProgress`.
- `EnglishTest` → `EnglishQuestion`, результаты — `EnglishTestResult` (ответы и баллы).
- `EnglishHomework` (всем или группе) → `EnglishSubmission` (статусы `submitted` / `reviewed`).
- `EnglishGroup` + `EnglishGroupMember`, `EnglishEvent` (расписание), `EnglishMessage` (чат).
- `EnglishWord` (словарь), `EnglishMaterial` + `EnglishFavorite` (библиотека), `EnglishBooking` (заявки с лендинга).
- `EnglishActivity` — журнал активности: из него считаются XP, серия дней, тепловая карта и время занятий.

Фото урока хранится как `MediaFile` (`Media:Path`) и отдаётся через `GET /media/{id}`:
доступ есть у автора урока и у его учеников, если урок опубликован.

## Сервисы

`LumenusErp/Services/English/`: `EnglishProfileService` (профили, ученики, группы, заявки),
`EnglishLessonService`, `EnglishTestService`, `EnglishHomeworkService`, `EnglishLibraryService`
(словарь и материалы), `EnglishScheduleService`, `EnglishMessageService`, `EnglishProgressService`
(XP и статистика), `EnglishSeed` (демо-контент новому преподавателю), `EnglishRoles` и `EnglishText` (форматирование по-русски).

Все сервисы scoped и работают через `IDbContextFactory<ApplicationDbContext>` — как `TaskService` в основном проекте.

## Страницы

Интерактивные страницы наследуют `Components/English/EnglishPageBase` (пользователь, роль, профиль,
часовой пояс, `LoadAsync`, `Report`), общие элементы — `EsHeading`, `EsPanel`-классы в `wwwroot/english.css`,
`EsModal`, `EsProgress`, `EsEmpty`, `EsLessonCard`, `EsHeatmap`. Стили — один файл `wwwroot/english.css`
с префиксом `es-`; озвучивание слов — `wwwroot/js/english.js` (`speechSynthesis`, en-GB).

## Начисление XP

| Действие | Минуты | XP |
|---|---|---|
| Завершённый урок | 30 | 100 |
| Пройденный тест | 5 | 50 |
| Домашняя работа (первая сдача) | 20 | 30 |
| Повтор слова карточкой | 1 | 5 |

Уровень = XP / 250 + 1. Серия дней и тепловая карта считаются в часовом поясе профиля ученика.

## Папки для материалов

- `LumenusErp/wwwroot/english/images/` — фото для лендинга и кабинета.
- `LumenusErp/wwwroot/english/backgrounds/` — фоны.

Файлы кладутся туда и подключаются в разметке как `english/images/<имя>`.

## Как запустить

Как и весь проект: `docker compose up -d --build` (миграции применяются при старте).
Локально без Docker нужен PostgreSQL и строки подключения `ConnectionStrings__DefaultConnection`
и `ConnectionStrings__AosConnection`.
