# Third-party notices

Этот файл — краткий реестр основных прямых зависимостей демонстрационного MVP. Полные тексты лицензий и полный transitive tree находятся в соответствующих пакетах и lock-файлах. Проверяемые версии зафиксированы в `package-lock.json`, `.csproj` и `docker-compose.yml`.

## Frontend и тестирование

| Компонент | Версия | Лицензия |
|---|---:|---|
| React / React DOM | 19.2.8 | MIT |
| Material Web | 2.5.0 | Apache-2.0 |
| Onest Variable font | 5.3.1 | OFL-1.1 |
| Axios | 1.20.0 | MIT |
| TanStack Query | 5.102.8 | MIT |
| Zustand | 5.0.15 | MIT |
| Microsoft SignalR JavaScript client | 10.0.11 | MIT |
| Vite | 8.2.2 | MIT |
| Vitest | 5.0.0 | MIT |
| Playwright Test | 1.62.1 | Apache-2.0 |

## Backend

| Компонент | Версия | Лицензия |
|---|---:|---|
| ASP.NET Core / EF Core / Microsoft.Extensions | 10.0.11 | MIT |
| Npgsql / Npgsql EF provider | 10.0.3 | PostgreSQL |
| Confluent.Kafka | 2.15.0 | Apache-2.0 |
| librdkafka | 2.15.0 | BSD-2-Clause |
| StackExchange.Redis | 3.1.31 | MIT |
| ClosedXML | 0.105.1 | MIT |
| DocumentFormat.OpenXml | 3.1.1 | MIT |
| StbImageSharp | 2.30.16 | MIT |
| StbImageWriteSharp | 1.16.7 | Public Domain / MIT / Unlicense upstream terms |
| WebPush | 1.0.13 | MPL-2.0 |
| Portable.BouncyCastle | 1.9.0 | MIT |
| xUnit | 4.0.0 | Apache-2.0 |

`WebPush` используется без изменения его исходных файлов. MPL-2.0 не распространяется на собственный код приложения; при распространении измененных MPL-файлов необходимо выполнить требования этой лицензии.

## Контейнеры

| Образ | Версия | Основная лицензия проекта |
|---|---:|---|
| PostgreSQL Alpine | 18.1 | PostgreSQL |
| Redis Alpine | 8.2.1 | RSALv2 / SSPLv1 / AGPLv3, по выбору применимого режима |
| Apache Kafka | 4.1.0 | Apache-2.0 |
| Nginx Alpine | 1.29.8 | BSD-2-Clause |
| Microsoft .NET SDK/ASP.NET runtime | 10.0 | MIT и notices образа |
| Microsoft Playwright Noble | 1.62.1 | Apache-2.0 плюс системные лицензии Ubuntu |

Перед внешним или production-распространением нужно сохранить notices базовых образов, выбрать и юридически подтвердить допустимый вариант лицензирования Redis, а также повторно сформировать полный SBOM для фактических образов.
