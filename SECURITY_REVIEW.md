# Security и dependency review

Проверено: 9 сентября 2026 года. Документ фиксирует границы MVP, воспроизводимые команды и результаты; это не заменяет внешний аудит перед production-развертыванием.

## Границы доступа

- Заявитель предъявляет трек-номер только в теле POST либо использует HttpOnly device capability. Секрет не входит в URL, frontend storage, события аналитики или уведомления.
- Оператор читает содержимое обращения и вложения для маршрутизации, но не получает чат эксперта и внутренние заметки. Жалоба является отдельным разрешенным текстом.
- Эксперт получает content/chat/notes/files только назначенного обращения или обращения, где он активный соисполнитель. Несовпадающие `appealId` / `attachmentId` возвращают `404`.
- Администратор получает конфигурацию, обезличенные метаданные, аудит и аналитику, но не content/chat/notes/files/contact.
- Staff-маршруты используют role policies, HttpOnly SameSite=Strict cookie и antiforgery-токен для изменений. Cross-origin allowlist не включен.

Эти правила проверяет `SecurityHardeningTests`: anonymous/cross-role, direct API, object-level mismatch, operator note/chat denial, administrator content/contact/file denial, unassigned expert denial, applicant cross-ticket file denial и CSRF.

## Трек-номер и HTTP

- После пяти неуспешных status lookup за 60 секунд Redis блокирует адрес. Ключ — HMAC-SHA-256 digest адреса без связи с обращением; TTL удаляет его автоматически. Повторные запросы получают нарастающий `Retry-After`, не раскрывая существование номера.
- Неверный формат и неизвестный номер имеют одинаковые status code, набор полей, длину и пользовательский текст; вход не отражается в ответе.
- Публичный API дополнительно ограничен fixed-window лимитом. Nginx перезаписывает `X-Forwarded-For`, production API-порт привязан к loopback, а доверие private-proxy адресу включается отдельной настройкой.
- API и frontend выставляют `nosniff`, frame denial, referrer policy, permissions policy, COOP и строгий CSP. Production добавляет HSTS для HTTPS. API и приватные browser-маршруты используют `no-store`.
- Offline shell не содержит данных обращения и не требует `unsafe-inline`; API, staff, hubs и навигационные ответы не кэшируются service worker.

## Файлы, логи и секреты

- Принимаются только PNG/JPEG/PDF, не более пяти файлов и 10 МБ каждый. Тип проверяется по сигнатуре, изображения декодируются и перекодируются без EXIF/GPS, активные/зашифрованные PDF отклоняются.
- Storage key генерирует сервер. Absolute/path traversal ключи блокируются, скачивание требует правильной пары ticket/file и разрешенной роли.
- Request bodies не логируются. EF sensitive data и detailed errors явно выключены; worker пишет только счетчики и внутренние event id. Контрольные поиски по контейнерным логам не должны находить track/contact/message/note/filename.
- Рабочие значения пароля БД, HMAC и VAPID private key не входят в `appsettings.json` или final image. Локальный `.env` исключен из Git и Docker build context; `.env.example` содержит только placeholders. В исходниках остаются намеренно публичные demo-пароли интеграционных тестов и отдельный unit-test HMAC fixture: production seed отключен, и эти значения нельзя использовать как production secrets.

## Зависимости и лицензии

Воспроизводимые проверки:

```bash
dotnet list Otklik.slnx package --vulnerable --include-transitive
dotnet list Otklik.slnx package --include-transitive --format json
cd src/frontend && npm audit && npm audit --omit=dev
```

Результат на дату проверки: NuGet — у всех семи проектов нет известных vulnerable packages; npm production и полный dependency tree — `0 vulnerabilities`. `npm outdated` показывает только новый major TypeScript 7 относительно зафиксированного TypeScript 6.0.3, что не является security finding.

Frontend lockfile содержит только 0BSD, Apache-2.0, BSD-2/3-Clause, BlueOak-1.0.0, CC0-1.0, ISC, MIT/MIT-0, MPL-2.0, OFL-1.1 и Unlicense. Material Web — Apache-2.0, Onest — OFL-1.1.

NuGet metadata содержит MIT, Apache-2.0, BSD, PostgreSQL, MPL-2.0, Bouncy Castle и public-domain/Unlicense компоненты. `StbImageWriteSharp 1.16.7` не записывает SPDX-лицензию в nuspec, но [upstream repository](https://github.com/StbSharp/StbImageWriteSharp) явно объявляет Public Domain; исходный stb также доступен по MIT или Unlicense. `WebPush 1.0.13` использует [MPL-2.0](https://github.com/web-push-libs/web-push-csharp/blob/master/LICENSE), а `librdkafka` — [BSD-2-Clause](https://github.com/confluentinc/librdkafka). GPL/AGPL и коммерчески ограниченных зависимостей в зафиксированном дереве не обнаружено.

## Production checklist

1. Передать secrets через штатный secret store, не через образ или репозиторий; заменить все dev-значения.
2. Завершать TLS на доверенном reverse proxy, не публиковать API напрямую и ограничить список доверенных proxy networks.
3. Настроить backup/restore, retention, централизованный redacted logging и внешний мониторинг без request bodies.
4. Повторить dependency/vulnerability scan и независимый penetration test перед вводом реальных обращений.
