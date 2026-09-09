# Ticket 001 — Инициализация приложений и окружения

Перед выполнением прочитать `tickets/README.md` и `tickets/progress.md`; вести текущий Goal Mode по правилам progress.

## Цель

Создать воспроизводимый каркас frontend/backend и локальное окружение, которое запускается одной Docker-командой без установленного на хосте .NET SDK или Node.js.

## Checkpoints

1. **Инфраструктура:** Compose поднимает PostgreSQL, Redis и Kafka с health-checks.
2. **Backend:** API и worker собираются в контейнерах; `/health/live` и `/health/ready` различают жизнь процесса и готовность зависимостей.
3. **Frontend:** mobile-first React-страница показывает состояние API и ссылки на будущие пользовательские контуры.
4. **Чистый запуск:** весь стек собирается и запускается общей командой; инструкция проверена.

## План

- Зафиксировать поддерживаемые стабильные версии образов и создать lock-файлы. Базовая цель: .NET 10 LTS и Node 24 LTS; фактические patch-версии фиксируются при выполнении.
- Создать solution с проектами `Domain`, `Application`, `Infrastructure`, `Api`, `Worker` и backend tests.
- Создать React + TypeScript + Vite приложение.
- Установить Axios, TanStack Query, Zustand и `@material/web`; добавить React-адаптеры/JSX typing для Material Web components и единый слой design tokens.
- Настроить ESLint, TypeScript strict mode, frontend tests и форматирование.
- Добавить EF Core + Npgsql, Redis client и Kafka client; на этом тикете только проверка соединений.
- Создать Dockerfiles с multi-stage build и `docker-compose.yml` для `frontend`, `api`, `worker`, `postgres`, `redis`, `kafka`.
- Настроить reverse proxy: браузер обращается к API через `/api` на том же origin.
- Добавить `.env.example`; секреты и пароли для production не коммитить.
- Создать корневой README с запуском, остановкой, просмотром логов и диагностикой Docker.
- Добавить CI-совместимые команды `frontend test/build` и `dotnet test` внутри контейнеров.

## Проверки

- Unit smoke tests frontend/backend.
- API readiness становится healthy только после доступности PostgreSQL, Redis и Kafka.
- Worker пишет успешное подключение без публикации пользовательских данных.
- Frontend открывается в узком viewport без горизонтального scroll.

## Docker checkpoint

Запустить `docker compose up --build -d`. Пользователь должен открыть frontend и увидеть страницу «Отклик — технический стенд» с зелеными состояниями API, PostgreSQL, Redis и Kafka. API health доступен через reverse proxy. Точные URL записать в `progress.md`.

## Готово, когда

Проект стартует из чистого состояния одной командой, не использует host `dotnet`/`node` для сборки, все контейнеры healthy, тесты проходят, а стартовая mobile-first страница доступна в браузере.

