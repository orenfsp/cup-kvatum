# Отклик

«Отклик» — mobile-first PWA для анонимных обращений о травле, конфликтах и давлении. Заявителю не нужно регистрироваться: после отправки обращения он получает секретный трек-номер и может вернуться к диалогу позднее. Оператор разбирает и маршрутизирует обращение, эксперт ведёт анонимный диалог и готовит рекомендации, администратор управляет конфигурацией и видит только обезличенные данные.

## Быстрый запуск

### Windows

1. Установите и запустите Docker Desktop.
2. Откройте PowerShell в корне проекта.
3. Выполните:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-demo.ps1
```

### macOS или Linux с PowerShell 7

1. Установите и запустите Docker Desktop либо Docker Engine с Compose v2.
2. Установите PowerShell 7, если команда `pwsh` ещё недоступна.
3. В корне проекта выполните:

```bash
pwsh -File ./scripts/start-demo.ps1
```

Скрипт автоматически:

1. создаст локальный файл `.env`, если его ещё нет;
2. сгенерирует случайный пароль PostgreSQL и два криптографических ключа;
3. соберёт Docker-образы;
4. запустит шесть сервисов;
5. дождётся, пока каждый сервис станет здоровым;
6. выведет адрес приложения.

Первый запуск занимает дольше повторного, потому что Docker скачивает базовые образы и собирает frontend/backend. Успешный запуск заканчивается сообщением:

```text
Demo is ready: http://localhost:3000
Readiness:    http://localhost:8080/api/health/ready
All six Compose services are healthy.
```

После этого откройте <http://localhost:3000>.

> Если PowerShell 7 на macOS/Linux устанавливать не хочется, используйте раздел [«Ручной запуск через Docker Compose»](#ручной-запуск-через-docker-compose).

## Что будет запущено

Docker Compose создаёт изолированный локальный стенд из шести сервисов:

| Сервис | Назначение | Доступ с компьютера |
|---|---|---|
| `frontend` | React PWA и Nginx, который проксирует API и SignalR | `http://localhost:3000` |
| `api` | ASP.NET Core API, авторизация, бизнес-сценарии и health checks | `http://localhost:8080` |
| `worker` | фоновые задачи, аналитика, авто-закрытие и push-уведомления | отдельного порта нет |
| `postgres` | основная база данных | наружу не опубликован |
| `redis` | rate limit, idempotency, lease и атомарные операции | наружу не опубликован |
| `kafka` | события аналитического контура | наружу не опубликован |

При стандартном запуске Compose-проект называется `otklik`. Все контейнеры, сеть и volumes получают этот префикс, поэтому стенд не смешивается с другими Compose-проектами.

## Системные требования

### Обязательно

- 64-битная Windows 10/11, актуальная macOS или Linux.
- Docker Desktop либо Docker Engine.
- Docker Compose v2 — команда должна выглядеть как `docker compose`, а не только как устаревшая `docker-compose`.
- Git, если проект ещё нужно скачать из репозитория.
- Свободные порты `3000` и `8080` либо два других выбранных порта.
- Интернет на первом запуске для скачивания Docker-образов и зависимостей.
- Рекомендуется не менее 4 ГБ свободной оперативной памяти для Docker и несколько гигабайт свободного места на диске.

### Проверка Docker

Выполните в PowerShell, Terminal или командной строке:

```text
docker --version
docker compose version
```

## Получение проекта

Чтобы скачать проект из GitHub:

```bash
git clone https://github.com/orenfsp/cup-kvatum.git
cd cup-kvatum
```

Все команды из этого README, если не сказано иначе, нужно выполнять из корня репозитория — папки, в которой находятся `docker-compose.yml`, `.env.example` и каталог `scripts`.

Проверить текущую папку можно так:

```bash
ls
```

На Windows в PowerShell также подойдёт:

```powershell
Get-ChildItem
```

В списке должен быть файл `docker-compose.yml`.

## Подробный запуск на Windows

### 1. Установите Docker Desktop

Установите Docker Desktop с поддержкой WSL 2 и запустите его. Дождитесь статуса, означающего, что Docker Engine работает.

### 2. Откройте проект в PowerShell

Например:

```powershell
Set-Location "C:\projects\cup-kvatum"
```

Если путь содержит пробелы, кавычки обязательны.

### 3. Запустите стенд

В Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-demo.ps1
```

В PowerShell 7 можно использовать:

```powershell
pwsh -File ./scripts/start-demo.ps1
```

Параметр `-ExecutionPolicy Bypass` действует только для этого запуска PowerShell и позволяет выполнить локальный скрипт без постоянного изменения системной политики выполнения.

### 4. Дождитесь готовности

Не закрывайте окно во время первой сборки. Скрипт ждёт до 240 секунд. При успехе он покажет таблицу контейнеров и строку `Demo is ready`.

### 5. Откройте приложение

Откройте в браузере:

```text
http://localhost:3000
```

### Если порты 3000 или 8080 заняты

Выберите свободные порты, например 3100 и 8180:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-demo.ps1 `
  -FrontendPort 3100 `
  -ApiPort 8180
```

Тогда приложение будет доступно по адресу <http://localhost:3100>, а readiness API — по адресу <http://localhost:8180/api/health/ready>.

## Подробный запуск на macOS и Linux

### Вариант A — через PowerShell 7

Из корня проекта выполните:

```bash
pwsh -File ./scripts/start-demo.ps1
```

Для других портов:

```bash
pwsh -File ./scripts/start-demo.ps1 -FrontendPort 3100 -ApiPort 8180
```

Если команда `pwsh` не найдена, установите PowerShell 7 или используйте вариант B.

### Вариант B — без PowerShell

1. Создайте `.env` из примера:

```bash
cp .env.example .env
```

2. Откройте `.env` в любом текстовом редакторе.
3. Замените **все** значения вида `<...>` реальными значениями. В готовом `.env` не должно остаться угловых скобок.
4. Запустите проект:

```bash
docker compose -p otklik up --build -d
```

5. Посмотрите состояние:

```bash
docker compose -p otklik ps
```

6. Дождитесь, когда у всех шести сервисов появится состояние `healthy`.

7. Откройте <http://localhost:3000>.

Безопасные случайные значения для `.env` можно получить через OpenSSL:

```bash
openssl rand -hex 18
openssl rand -base64 32
openssl rand -base64 32
```

Первая команда подходит для `POSTGRES_PASSWORD`. Результат второй — для `OTKLIK_TRACK_HASH_KEY`, третьей — для `OTKLIK_RATE_LIMIT_HASH_KEY`. Эти два ключа должны отличаться.

## Ручной запуск через Docker Compose

Этот способ одинаково подходит для Windows, macOS и Linux, но `.env` потребуется подготовить самостоятельно.

### 1. Создайте файл `.env`

Скопируйте шаблон:

```bash
cp .env.example .env
```

В Windows PowerShell:

```powershell
Copy-Item .env.example .env
```

Файл `.env` уже добавлен в `.gitignore`: локальные секреты не должны попадать в Git.

### 2. Заполните обязательные значения

Минимальный рабочий пример для локального демо выглядит так:

```dotenv
POSTGRES_DB=otklik
POSTGRES_USER=otklik
POSTGRES_PASSWORD=replace-with-a-random-local-password
ASPNETCORE_ENVIRONMENT=Development
OTKLIK_OPERATOR_PASSWORD=Operator!2026
OTKLIK_EXPERT_PASSWORD=ExpertHelp!2026
OTKLIK_ADMINISTRATOR_PASSWORD=AdminPanel!2026
OTKLIK_TRACK_HASH_KEY=replace-with-base64-encoded-32-random-bytes
OTKLIK_RATE_LIMIT_HASH_KEY=replace-with-different-base64-encoded-32-random-bytes
OTKLIK_PUSH_PUBLIC_KEY=
OTKLIK_PUSH_PRIVATE_KEY=
OTKLIK_PUSH_SUBJECT=mailto:dev@otklik.local
```

Не копируйте этот пример как production-конфигурацию. Значения `replace-with-...` обязательно нужно заменить. Для локального демо можно оставить указанные демонстрационные пароли сотрудников.

### 3. Проверьте конфигурацию

```bash
docker compose -p otklik config --quiet
```

Если команда завершилась без вывода и с кодом `0`, Compose смог прочитать файл и обязательные переменные присутствуют.

### 4. Соберите и запустите сервисы

```bash
docker compose -p otklik up --build -d
```

Значение флагов:

- `-p otklik` задаёт стабильное имя Compose-проекта;
- `up` создаёт и запускает сервисы;
- `--build` пересобирает образы приложения;
- `-d` запускает контейнеры в фоне и возвращает управление терминалу.

### 5. Дождитесь готовности

```bash
docker compose -p otklik ps
```

Нормальный результат — шесть запущенных сервисов:

- `postgres` — `healthy`;
- `redis` — `healthy`;
- `kafka` — `healthy`;
- `api` — `healthy`;
- `worker` — `healthy`;
- `frontend` — `healthy`.

### Ручной запуск на других портах

Порты читаются из переменных `OTKLIK_FRONTEND_PORT` и `OTKLIK_API_PORT`.

macOS/Linux:

```bash
OTKLIK_FRONTEND_PORT=3100 OTKLIK_API_PORT=8180 docker compose -p otklik up --build -d
```

Windows PowerShell:

```powershell
$env:OTKLIK_FRONTEND_PORT = "3100"
$env:OTKLIK_API_PORT = "8180"
docker compose -p otklik up --build -d
```

Чтобы выбранные порты сохранялись между терминальными сессиями, можно добавить в локальный `.env`:

```dotenv
OTKLIK_FRONTEND_PORT=3100
OTKLIK_API_PORT=8180
```

## Проверка успешного запуска

### Проверка в браузере

Откройте следующие страницы:

| Проверка | Адрес | Ожидаемый результат |
|---|---|---|
| Главная страница | <http://localhost:3000> | загружается интерфейс «Отклика» |
| Новое обращение | <http://localhost:3000/appeal/new> | открывается форма обращения |
| Кабинет сотрудника | <http://localhost:3000/staff> | открывается форма входа |
| Состояние системы | <http://localhost:3000/system> | показаны состояния компонентов |
| API liveness | <http://localhost:8080/api/health/live> | JSON со статусом `healthy` |
| API readiness | <http://localhost:8080/api/health/ready> | JSON со статусом `healthy` |

Если были выбраны другие порты, замените `3000` и `8080` на них.

### Автоматическая smoke-проверка

Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-demo.ps1
```

PowerShell 7 на любой ОС:

```bash
pwsh -File ./scripts/verify-demo.ps1
```

Для нестандартных портов:

```bash
pwsh -File ./scripts/verify-demo.ps1 -FrontendPort 3100 -ApiPort 8180
```

Скрипт проверяет frontend, readiness API и заранее подготовленный анонимный кризисный сценарий. Успешный результат:

```text
Smoke passed: frontend 200, API Healthy, seeded anonymous crisis route available.
```

### Проверка без PowerShell

```bash
curl --fail http://localhost:8080/api/health/ready
curl --fail http://localhost:3000/
```

Первая команда должна вернуть JSON со значением `status: "healthy"`, вторая — HTML страницы без HTTP-ошибки.

## Адреса и демо-учётные записи

### Полезные страницы

| Раздел | URL |
|---|---|
| Главная | <http://localhost:3000> |
| Подать обращение | <http://localhost:3000/appeal/new> |
| Найти своё обращение | <http://localhost:3000/appeal> |
| Вход сотрудников | <http://localhost:3000/staff> |
| Состояние системы | <http://localhost:3000/system> |
| API | <http://localhost:8080/api> |
| API readiness | <http://localhost:8080/api/health/ready> |

### Демо-учётные записи

Они создаются только при `ASPNETCORE_ENVIRONMENT=Development`.

| Роль | Логин | Пароль по умолчанию |
|---|---|---|
| Оператор | `operator` | `Operator!2026` |
| Эксперт | `expert` | `ExpertHelp!2026` |
| Эксперт по медиации | `expert.mediator` | `ExpertHelp!2026` |
| Администратор | `administrator` | `AdminPanel!2026` |

Если пароли в `.env` были изменены до первого старта, используйте значения из `.env`. Если база уже была создана, простое изменение пароля в `.env` не обязательно изменит пароль существующей учётной записи: для полностью чистого демо используйте явный reset.

Полный маршрут ручной приёмки C1–C8 и постоянные демонстрационные трек-номера описаны в [DEMO.md](./DEMO.md).

## Повседневное управление стендом

Все команды ниже выполняются из корня проекта.

### Посмотреть состояние контейнеров

```bash
docker compose -p otklik ps
```

### Посмотреть общие логи

```bash
docker compose -p otklik logs --tail 200
```

### Смотреть логи в реальном времени

```bash
docker compose -p otklik logs -f
```

Остановить просмотр логов можно сочетанием `Ctrl+C`. Контейнеры при этом продолжат работать.

### Посмотреть логи одного сервиса

```bash
docker compose -p otklik logs --tail 200 api
docker compose -p otklik logs --tail 200 worker
docker compose -p otklik logs --tail 200 frontend
docker compose -p otklik logs --tail 200 postgres
```

### Остановить контейнеры без их удаления

```bash
docker compose -p otklik stop
```

### Снова запустить остановленные контейнеры

```bash
docker compose -p otklik start
```

### Удалить контейнеры и сеть, сохранив данные

```bash
docker compose -p otklik down
```

Эта команда сохраняет named volumes, поэтому обращения, база, вложения, Kafka, Redis и ключи защиты данных останутся на месте.

### Снова создать контейнеры из сохранённых данных

```bash
docker compose -p otklik up -d
```

### Пересобрать проект после изменения исходного кода

```bash
docker compose -p otklik up --build -d
```

### Перезапустить отдельный сервис

```bash
docker compose -p otklik restart api
```

После изменений исходного кода одного `restart` недостаточно: сначала требуется пересборка через `up --build -d`.

## Настройки и переменные окружения

Docker Compose автоматически читает `.env` из корня репозитория.

| Переменная | Обязательность | Назначение |
|---|---|---|
| `POSTGRES_DB` | есть значение по умолчанию | имя базы PostgreSQL, по умолчанию `otklik` |
| `POSTGRES_USER` | есть значение по умолчанию | пользователь PostgreSQL, по умолчанию `otklik` |
| `POSTGRES_PASSWORD` | обязательна | локальный пароль PostgreSQL |
| `ASPNETCORE_ENVIRONMENT` | есть значение по умолчанию | `Development` включает демо-seed |
| `OTKLIK_OPERATOR_PASSWORD` | обязательна | пароль демо-оператора |
| `OTKLIK_EXPERT_PASSWORD` | обязательна | пароль обоих демо-экспертов |
| `OTKLIK_ADMINISTRATOR_PASSWORD` | обязательна | пароль демо-администратора |
| `OTKLIK_TRACK_HASH_KEY` | обязательна | Base64-ключ для безопасного хеширования трек-номеров |
| `OTKLIK_RATE_LIMIT_HASH_KEY` | обязательна | отдельный Base64-ключ для идентификаторов rate limit |
| `OTKLIK_PUSH_PUBLIC_KEY` | необязательна для базового демо | публичный VAPID-ключ Web Push |
| `OTKLIK_PUSH_PRIVATE_KEY` | необязательна для базового демо | приватный VAPID-ключ Web Push |
| `OTKLIK_PUSH_SUBJECT` | необязательна | контакт владельца VAPID-ключа в формате `mailto:...` |
| `OTKLIK_FRONTEND_PORT` | необязательна | внешний порт frontend, по умолчанию `3000` |
| `OTKLIK_API_PORT` | необязательна | внешний порт API, по умолчанию `8080` |

Важные правила:

- не добавляйте `.env` в Git;
- не используйте одинаковые значения для двух hash-ключей;
- после создания рабочих данных не меняйте `OTKLIK_TRACK_HASH_KEY` без плана миграции: старые трек-номера могут перестать находиться;
- не удаляйте volume ключей Data Protection у работающего стенда без необходимости: существующие защищённые данные и cookie могут стать недоступны;
- пустые VAPID-ключи не мешают базовому локальному запуску, но реальные push-уведомления требуют корректной VAPID-пары;
- после изменения `.env` пересоздайте контейнеры командой `docker compose -p otklik up -d --force-recreate`.

## Данные и Docker volumes

Compose создаёт пять named volumes:

| Volume | Что хранит |
|---|---|
| `postgres-data` | обращения, пользователи, настройки и операционные данные PostgreSQL |
| `redis-data` | персистентные данные Redis |
| `kafka-data` | журнал Kafka |
| `api-keys` | ключи ASP.NET Core Data Protection, общие для API и worker |
| `attachments-data` | приватные вложения обращений |

С учётом имени проекта реальные имена обычно выглядят как `otklik_postgres-data`, `otklik_attachments-data` и так далее.

Обычные команды `stop`, `start`, `restart`, `down` и `up` volumes не удаляют. Данные удаляются только при использовании `down --volumes`, короткого варианта `down -v` или скрипта полного reset.

Посмотреть volumes проекта:

```bash
docker volume ls --filter label=com.docker.compose.project=otklik
```

Автоматизированный backup/restore в MVP не реализован. Если данные важны, не используйте reset и заранее организуйте отдельное резервное копирование PostgreSQL, вложений и ключей Data Protection.

## Полный сброс демо-данных

Reset удаляет базу, обращения, вложения, Redis, Kafka и ключи только выбранного Compose-проекта. Операция необратима. Файл `.env` сохраняется.

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\reset-demo.ps1 -ConfirmDataLoss
```

PowerShell 7 на macOS/Linux:

```bash
pwsh -File ./scripts/reset-demo.ps1 -ConfirmDataLoss
```

После удаления volumes скрипт сразу заново соберёт и запустит стенд, применит миграции и создаст чистые демонстрационные данные.

Для отдельного Compose-проекта и других портов:

```bash
pwsh -File ./scripts/reset-demo.ps1 \
  -ConfirmDataLoss \
  -ProjectName otklik-test \
  -FrontendPort 3100 \
  -ApiPort 8180
```

Без PowerShell эквивалент выглядит так:

```bash
docker compose -p otklik down --volumes --remove-orphans
docker compose -p otklik up --build -d
```

Перед ручной командой внимательно проверьте значение после `-p`: именно volumes этого Compose-проекта будут удалены.

## Запуск тестов

Перед тестами должен существовать заполненный `.env`. Compose сам запустит необходимые зависимости, если они ещё не работают, но заранее поднятый здоровый стенд сократит ожидание.

Интеграционные и E2E-сценарии создают и изменяют демонстрационные данные. Кроме того, часть приёмочных сценариев ожидает стандартные Development-пароли из таблицы выше. Для воспроизводимого прогона оставьте эти пароли и не запускайте тесты против стенда с ценными данными.

### Backend: сборка и unit-тесты в контейнере

```bash
docker build --target test -f src/backend/Otklik.Api/Dockerfile .
```

### Frontend: lint, unit-тесты и production build в контейнере

```bash
docker build --target test -f src/frontend/Dockerfile .
```

### Интеграционные backend-тесты

```bash
docker compose -p otklik --profile tests run --rm --build integration-tests
```

### Браузерные Playwright E2E-тесты

```bash
docker compose -p otklik --profile tests run --rm --build e2e-tests
```

Тестовые контейнеры используют пароли сотрудников из `.env`. Флаг `--rm` удаляет одноразовый контейнер теста после завершения, но не удаляет основной стенд и его volumes.

Для полностью изолированного прогона можно использовать другое имя Compose-проекта и свободные внешние порты. Например, на macOS/Linux:

```bash
OTKLIK_FRONTEND_PORT=3100 OTKLIK_API_PORT=8180 \
  docker compose -p otklik-tests --profile tests run --rm --build integration-tests
OTKLIK_FRONTEND_PORT=3100 OTKLIK_API_PORT=8180 \
  docker compose -p otklik-tests --profile tests run --rm --build e2e-tests
docker compose -p otklik-tests down --volumes --remove-orphans
```

Последняя команда намеренно удаляет только тестовые volumes проекта `otklik-tests`.

### Локальные проверки зависимостей

Эти команды требуют установленных .NET SDK 10 и Node.js 24:

```bash
dotnet list Otklik.slnx package --vulnerable --include-transitive
cd src/frontend
npm audit
npm audit --omit=dev
```

## Локальная разработка без сборки приложения в Docker

Для обычной демонстрации этот режим не нужен. Он полезен, если требуется быстрый hot reload frontend или запуск backend из IDE.

### Дополнительные требования

- .NET SDK `10.0.100` или совместимая более новая feature-версия .NET 10;
- Node.js 24;
- npm;
- Docker для PostgreSQL, Redis и Kafka.

Проверка:

```bash
dotnet --version
node --version
npm --version
```

### Рекомендуемый режим разработки

Поддерживаемый и самый простой режим разработки — запуск всего стека в Docker с пересборкой:

```bash
docker compose -p otklik up --build -d
```

После этого frontend можно дополнительно запустить через Vite с hot reload. Docker-версия API при этом уже доступна на `http://localhost:8080`:

```bash
cd src/frontend
npm ci
npm run dev
```

Vite откроется на <http://localhost:5173> и будет проксировать `/api` на `http://localhost:8080`. Для полноценной локальной разработки SignalR может потребовать дополнительной настройки proxy, потому что текущая Vite-конфигурация явно проксирует только `/api`.

Запуск API и worker непосредственно через `dotnet run` пока не является готовым сценарием «из коробки»: PostgreSQL, Redis и Kafka в текущем Compose не публикуют порты на хост. Для такого режима потребуется отдельный локальный Compose override и host-ориентированные connection strings. Не публикуйте инфраструктурные порты в общедоступную сеть.

## Обновление проекта

1. Остановите активные операции в интерфейсе.
2. Получите изменения из Git:

```bash
git pull
```

3. Пересоберите и пересоздайте контейнеры:

```bash
docker compose -p otklik up --build -d
```

4. Проверьте состояние:

```bash
docker compose -p otklik ps
```

5. Запустите smoke-проверку.

При старте API автоматически применяет миграции базы. Повторный Development seed идемпотентен и не должен создавать дубликаты.

Перед обновлением стенда с важными данными обязательно сделайте резервную копию: полноценный автоматизированный backup/restore не входит в этот MVP.

## Решение частых проблем

### `docker: command not found`

Docker не установлен либо терминал был открыт до установки. Установите Docker Desktop/Docker Engine, затем полностью перезапустите терминал.

### `Cannot connect to the Docker daemon` или похожая ошибка

Docker установлен, но Engine не запущен. Запустите Docker Desktop и дождитесь готовности. На Linux проверьте состояние Docker daemon и права пользователя на работу с Docker.

### Команда `docker compose` не существует

Установлен старый Docker без Compose v2. Обновите Docker Desktop либо установите Compose plugin. Проект и скрипты используют именно `docker compose`.

### PowerShell запрещает выполнение скрипта

На Windows используйте команду из README с локальным обходом политики:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\start-demo.ps1
```

Либо откройте PowerShell 7 и выполните `pwsh -File ./scripts/start-demo.ps1`.

### `.env still contains placeholder values`

В `.env` остались значения вида `<generate-a-local-password>`. Возможны два решения:

1. заменить каждый placeholder настоящим значением;
2. если в файле нет нужных вам настроек, удалить локальный `.env` и снова запустить `start-demo.ps1`, чтобы скрипт создал его автоматически.

Не удаляйте `.env`, если в нём уже хранятся нужные секреты рабочего стенда.

### `port is already allocated` или `address already in use`

Порт `3000` или `8080` занят другим процессом. Запустите стенд на других портах:

```bash
pwsh -File ./scripts/start-demo.ps1 -FrontendPort 3100 -ApiPort 8180
```

Либо остановите программу/контейнер, который использует нужный порт.

Посмотреть опубликованные Docker-порты:

```bash
docker ps --format "table {{.Names}}\t{{.Ports}}"
```

### Скрипт не дождался готовности за 240 секунд

На первом запуске или медленном компьютере увеличьте тайм-аут:

```bash
pwsh -File ./scripts/start-demo.ps1 -TimeoutSeconds 600
```

Допустимый диапазон скрипта — от 30 до 900 секунд.

После ошибки изучите состояние и последние логи:

```bash
docker compose -p otklik ps
docker compose -p otklik logs --tail 200 api worker frontend postgres redis kafka
```

### Один контейнер имеет статус `unhealthy`

Сначала найдите проблемный сервис через `docker compose -p otklik ps`, затем посмотрите его лог. Типичная последовательность:

```bash
docker compose -p otklik logs --tail 200 api
docker compose -p otklik logs --tail 200 worker
```

API зависит от PostgreSQL, Redis и Kafka; worker дополнительно ждёт здоровый API. Поэтому ошибка инфраструктуры может последовательно сделать нездоровыми несколько сервисов.

### В браузере старая версия интерфейса

1. Пересоберите frontend:

```bash
docker compose -p otklik up --build -d frontend
```

2. Выполните жёсткое обновление страницы.
3. Если PWA была установлена, закройте все её окна и откройте снова.
4. При необходимости очистите данные сайта для `localhost`, учитывая, что это удалит локальный доступ устройства к обращениям.

### Учётная запись не принимает пароль из таблицы

Проверьте реальные значения `OTKLIK_OPERATOR_PASSWORD`, `OTKLIK_EXPERT_PASSWORD` и `OTKLIK_ADMINISTRATOR_PASSWORD` в локальном `.env`. Учётные записи создаются только в `Development` и только при инициализации demo seed.

Если это одноразовый локальный стенд без ценных данных, выполните полный reset. Не делайте reset, если обращения или вложения нужно сохранить.

### Не находятся старые обращения по трек-номеру

Убедитесь, что не менялся `OTKLIK_TRACK_HASH_KEY` и не удалялся PostgreSQL volume. Трек-номер является секретом доступа и не восстанавливается из интерфейса администратора.

### Push-уведомления не приходят

Базовый демо-запуск допускает пустой приватный VAPID-ключ, поэтому push может быть фактически не настроен. Для реального Web Push нужны валидная VAPID-пара, корректный `OTKLIK_PUSH_SUBJECT`, разрешение браузера и secure context. `localhost` считается безопасным контекстом для локальной разработки; удалённое развёртывание должно использовать HTTPS.

### Нужно начать совсем с чистого состояния

Используйте только явный reset из соответствующего раздела. Он удалит все volumes выбранного проекта. Простая пересборка образов данные не удаляет.

## Ограничения production-развёртывания

Текущий `docker-compose.yml` предназначен прежде всего для локальной демонстрации и приёмки. Не следует без изменений публиковать его в интернет.

Перед production-запуском как минимум потребуется:

- reverse proxy или ingress с валидным HTTPS-сертификатом;
- безопасное хранение секретов вне `.env` в репозитории и вне истории shell;
- уникальные сильные пароли и ключи;
- создание production-учётных записей: Development seed вне режима `Development` не выполняется;
- явная настройка VAPID-ключей для Web Push;
- политика резервного копирования и проверенная процедура восстановления PostgreSQL, вложений и Data Protection keys;
- мониторинг, централизованные логи, алерты и ограничение ресурсов контейнеров;
- production-настройка PostgreSQL, Redis и Kafka, включая доступ, шифрование, отказоустойчивость и retention;
- проверка доверенных reverse proxy и заголовков `X-Forwarded-*`;
- проверка миграций на копии production-базы до обновления;
- юридическая и организационная проверка обработки чувствительных данных;
- внешний security review и нагрузочное тестирование.

При `ASPNETCORE_ENVIRONMENT` не равном `Development` приложение включает более строгие требования к secure cookie и HSTS. Такой режим предполагает корректно настроенный HTTPS reverse proxy.

Дополнительные ограничения MVP перечислены в разделе [«Что не входит в MVP»](#что-не-входит-в-mvp). Технический отчёт о защитных мерах находится в [SECURITY_REVIEW.md](./SECURITY_REVIEW.md).

## Архитектура и структура репозитория

### Как проходит запрос

```text
Браузер
   │
   ▼
Nginx + React PWA (:3000)
   │  /api и /hubs
   ▼
ASP.NET Core API (:8080)
   ├── PostgreSQL — основная модель и transactional outbox
   ├── Redis      — rate limit, idempotency, lease и очереди
   ├── Kafka      — события аналитики
   └── volumes    — приватные вложения и Data Protection keys

Worker
   ├── читает/пишет PostgreSQL
   ├── работает с Kafka и Redis
   └── обрабатывает фоновые сценарии и push
```

### Основные каталоги

```text
.
├── src/
│   ├── backend/
│   │   ├── Otklik.Api/             # HTTP API, SignalR, endpoints и безопасность
│   │   ├── Otklik.Worker/          # фоновые обработчики
│   │   ├── Otklik.Application/     # прикладные правила и контракты
│   │   ├── Otklik.Domain/          # доменная модель
│   │   └── Otklik.Infrastructure/  # EF Core, Identity, Redis, Kafka, файлы
│   └── frontend/                    # React, Vite, PWA, Nginx и Playwright
├── tests/backend/                   # unit- и integration-тесты backend
├── scripts/                         # запуск, smoke-проверка и reset
├── tickets/                         # история реализации и критерии задач
├── docker-compose.yml               # весь локальный стенд
├── .env.example                     # шаблон локальных настроек
├── Otklik.slnx                      # решение .NET
└── DEMO.md                          # сценарий ручной приёмки C1–C8
```

React PWA использует Axios, TanStack Query, Zustand, SignalR и Material Web. Nginx раздаёт production-сборку frontend и проксирует same-origin запросы к API.

ASP.NET Core API использует Identity cookie, CSRF, RBAC, health checks и SignalR. PostgreSQL хранит рабочую модель и transactional outbox. Worker публикует обезличенные события в Kafka, обновляет аналитическую проекцию и обрабатывает фоновые задачи. Redis используется для rate limit, idempotency, совместных lease и атомарного закрепления операторских задач.

## Приватность и границы ролей

Трек-номер — секрет доступа. Он передаётся только в теле POST-запроса и не должен попадать в URL, аналитику, push payload, логи или offline-кэш. На текущем устройстве сервер может сохранить отдельную HttpOnly capability; её можно отозвать кнопкой «Удалить доступ с устройства».

Оператор видит первоначальное обращение и его вложения для разбора, но не видит экспертный чат и внутренние заметки. Эксперт видит содержание только назначенного ему обращения либо обращения, где он является активным соисполнителем. Администратор не получает тексты, ответы, чат, заметки, файлы, кризисные контакты и трек-номера. CSV/XLSX строятся по фиксированному whitelist обезличенных служебных полей.

Файлы хранятся приватно. Принимаются до пяти PNG, JPEG или PDF размером до 10 МБ; MIME проверяется по сигнатуре, изображения перекодируются без EXIF/GPS, активные и зашифрованные PDF отклоняются.

## Кризисный сценарий

Если текст указывает на непосредственную опасность, форма сразу показывает `112`, детский телефон доверия `8-800-2000-122` и короткий номер `124`. Подача обращения не блокируется. Контакт можно оставить добровольно; он хранится отдельно в зашифрованном виде и открывается только оператором с аудитом.

Если контакта нет, система не пытается установить личность и не обещает невозможный исходящий вызов. Обращение поднимается в кризисную очередь с таймером, источником сигнала и исходными сведениями. Оператор подтверждает срочность и назначает специалиста либо снимает ошибочный сигнал с обязательной причиной. Экспертный диалог оператору не раскрывается; кризисное обращение не закрывается автоматически.

## Что не входит в MVP

- Передача обращений во внешние государственные или ведомственные сервисы.
- ML-классификация: подсказка категории использует прозрачные настраиваемые правила/маркеры, решение принимает оператор.
- Production SLA, круглосуточное дежурство и организационные регламенты эскалации.
- Реальная эксплуатационная аттестация, внешний аудит, penetration test, готовый backup/restore и сертификация обработки персональных данных.
- Гарантия экстренного реагирования без добровольного контакта: интерфейс честно направляет к экстренным каналам и сохраняет анонимный диалог.

## Дополнительная документация

- [DEMO.md](./DEMO.md) — ручная приёмка и демонстрационные сценарии C1–C8.
- [SECURITY_REVIEW.md](./SECURITY_REVIEW.md) — реализованные меры безопасности и известные ограничения.
- [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) — сторонние компоненты и лицензии.
- [src/frontend/DESIGN_SYSTEM.md](./src/frontend/DESIGN_SYSTEM.md) — дизайн-система frontend.
- [tickets/progress.md](./tickets/progress.md) — технический журнал выполнения задач.
