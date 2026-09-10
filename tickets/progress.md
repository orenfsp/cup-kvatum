# Progress: правила и состояние выполнения

Этот файл — источник истины о ходе долгоживущих Goal Mode, которыми последовательно выполнены тикеты 001–015. Он является точкой восстановления после сжатия контекста, перезапуска, паузы или смены хода.

## Правила Goal Mode

1. **Один goal — весь активный план.** Objective определяется актуальным ticket-файлом; внутри него checkpoints выполняются последовательно, после каждого сохраняется работающий Docker-срез, а goal завершается только после финальной приемки. Бюджет токенов задается только при прямом указании пользователя.
2. **Старт.** До изменений прочитать `tickets/GOAL_PROMPT.md`, `tickets/README.md`, этот файл и первый незавершенный тикет; проверить `git status`, рабочий Docker-стенд и результат предыдущего тикета. Существующие пользовательские изменения сохраняются.
3. **Статус.** Одновременно только один тикет имеет `IN_PROGRESS`. После его завершения поставить `DONE`, заполнить финальную запись, перевести следующий `PLANNED` в `IN_PROGRESS` и продолжить в том же Goal Mode.
4. **Tracer bullets.** Сначала довести минимальный сквозной путь тикета до UI и API, затем расширять обработку ошибок и тесты. После каждого checkpoint сохранять рабочую сборку и обновлять журнал ниже.
5. **Видимый промежуточный результат.** Не реже каждого законченного checkpoint запускать актуальный стенд в Docker и записывать URL, роль/трек-номер, что уже можно посмотреть и что еще не готово. Если Goal Mode затянется, пользователь должен открыть последний рабочий срез без ожидания завершения всего тикета.
6. **Проверка.** Выполнить тесты, перечисленные в тикете, затем `docker compose up --build -d` и health-checks. Результат команды и ручной smoke-flow записать в журнал.
7. **Завершение тикета.** Статус `DONE` ставится только после выполнения каждого критерия приемки, успешного Docker-запуска из текущих файлов и обновления документации. Затем автоматически начинается следующий тикет. Goal Mode остается активным.
8. **Блокировка.** Не выдавать незавершенную работу за готовую. После повторяющегося блокера записывать каждую попытку и продолжать безопасные проверки. Goal Mode помечается blocked только по правилам режима после трех последовательных безуспешных goal-ходов с тем же внешним блокером.
9. **Завершение goal.** Goal Mode можно отметить complete только когда все строки его активного плана имеют `DONE`, финальный стенд healthy, обязательные сквозные сценарии пройдены и итоговая запись активного ticket-файла заполнена.
10. **Изменение требований.** Новое решение добавляется в журнал решений с датой. Уже выполненные записи не переписываются; влияние на прошлые тикеты фиксируется отдельным пунктом.
11. **Миграции и данные.** Миграции EF Core добавляются последовательно. Seed должен оставаться детерминированным. Разрушительное удаление volume не используется как обычный способ починки.

## Восстановление после сжатия контекста

В начале каждого нового хода, особенно если история была сжата или прежние детали недоступны:

1. Прочитать этот файл целиком.
2. Найти единственный `IN_PROGRESS`. Если его нет, выбрать первый `PLANNED` после последнего `DONE`.
3. Прочитать соответствующий ticket-файл и последнюю запись его checkpoint.
4. Проверить `git status`, актуальные файлы, запущенные контейнеры и тесты; считать файловую систему и фактический Docker-стенд источником истины, а не пытаться восстановить работу по памяти.
5. Продолжить с поля «Следующий шаг». Уже подтвержденные checkpoints не переделывать, если текущий код и проверки подтверждают их сохранность.
6. До новых крупных изменений обновить «Активный тикет», если состояние кода расходится с журналом.

Сжатие контекста не является блокером и не поводом завершать goal. `progress.md` должен содержать достаточно данных, чтобы продолжить без вопроса пользователю.

## Статусы

- `PLANNED` — не начат.
- `IN_PROGRESS` — выполняется текущим Goal Mode.
- `BLOCKED` — подтвержденный внешний блокер по правилам Goal Mode.
- `DONE` — критерии приемки и Docker-проверка выполнены.

## Сводка

| Тикет | Статус | Последний checkpoint | Локальный просмотр |
|---|---|---|---|
| 001 | DONE | Checkpoint 4/4: container-only tests и полный Compose rebuild пройдены | http://localhost:3000 |
| 002 | DONE | Checkpoint 4/4: RBAC integration tests 5/5 и полный Docker smoke пройдены | http://localhost:3000/staff |
| 003 | DONE | Checkpoint 4/4: два сквозных пути, status lookup и контейнерная приемка пройдены | http://localhost:3000/appeal/new |
| 004 | DONE | Checkpoint 4/4 и сквозной дизайн-фундамент приняты; C1 с вложением и Docker-проверка пройдены | http://localhost:3000/appeal/new |
| 005 | DONE | Checkpoint 4/4: C3, container-only tests и mobile UX пройдены | http://localhost:3000/staff |
| 006 | DONE | Checkpoint 4/4: C4/chat, privacy, SignalR и container checks пройдены | http://localhost:3000/staff |
| 007 | DONE | Checkpoint 4/4: transfer, coexecutor, presence/lease и Docker-приемка пройдены | http://localhost:3000/staff |
| 008 | DONE | Checkpoint 4/4: возвраты, повторное назначение, оценка, жалоба и auto-close приняты | http://localhost:3000/appeal/status |
| 009 | DONE | Checkpoint 4/4: C6, изоляция контакта, кризисная очередь и Docker-приемка пройдены | http://localhost:3000/appeal/new |
| 010 | DONE | Checkpoint 4/4: C7, container tests и полный Compose smoke пройдены | http://localhost:3000/staff |
| 011 | DONE | Checkpoint 4/4: Kafka projection, scoped dashboard, CSV/XLSX и C8 приняты | http://localhost:3000/staff |
| 012 | DONE | Checkpoint 4/4: PWA installability, offline privacy, capability/revoke, push и Docker-приемка пройдены | http://localhost:3000/appeal/status |
| 013 | DONE | Checkpoint 4/4: authorization, rate-limit, HTTP/log/secret audit и Docker negative suite пройдены | http://localhost:3000 |
| 014 | DONE | Checkpoint 4/4: чистый release, C1–C8, persistence, документация и финальный стенд приняты | http://localhost:3000 |
| 015 | DONE | Checkpoint 6/6: единый UX, непрерывная история, кабинеты ролей, accessibility и persistence приняты | http://localhost:3000 |

## Активный тикет

- Тикет: нет — все тикеты 001–015 завершены
- Goal objective: Ticket 015 выполнен одним Goal Mode по checkpoints 1–6 с работающим Docker-срезом после каждого этапа
- Стартовое состояние: Tickets 001–015 DONE; основной Compose-срез healthy, PostgreSQL persistence после полного рестарта подтвержден
- Текущий checkpoint: Ticket 015 checkpoint 6/6 завершен; C1–C8, UX, accessibility и финальные критерии приняты
- Следующий шаг: отсутствует; Goal Mode Ticket 015 готов к завершению
- Блокеры: нет

## Журнал checkpoints

Добавлять новые записи сверху, не удаляя старые.

```text
2026-09-10 00:46 +10:00 — post-acceptance — живой экспертский inbox и понятные переходы статусов
Готово: устранено «исчезновение» обращения после взятия в работу. URL, активный пункт навигации и кнопка возврата автоматически следуют за фактическим состоянием Assigned → InProgress → NeedsClarification → InProgress → RecommendationReady/Closed. В строках списка и шапке карточки первым показано требуемое действие; после перехода выводится фокусируемая квитанция с объяснением нового раздела. Левая навигация показывает счётчики новых, активных, ожидающих и завершённых обращений; активные дополнительно подписаны как требующие действия. Добавлены персональная SignalR-группа эксперта и единая серверная сводка, поэтому назначение, ответ заявителя, сообщение эксперта, результат и изменение команды обновляют навигацию без reload; polling 30 секунд оставлен страховкой реконнекта. Запрос оператору явно описан как параллельный процесс, который не меняет рабочий статус сам по себе.
Проверки: исходный Playwright repro падал на сохранённом `from=inbox` после «Взять в работу». После исправления focused red/green-сценарий прошёл и подтвердил без reload точные изменения счётчиков при вопросе и ответе заявителя. Frontend ESLint/build прошли, Vitest 34/34; .NET build 0 warnings/errors, unit 38/38, live integration 31/31; Expert Playwright mobile+desktop 3 passed/3 ожидаемых viewport skips; общий ux-routing 11 passed/1 ожидаемый skip.
Docker: api и frontend пересобраны без удаления volumes; персональный SignalR-канал и work-summary работают на текущем стенде.
Посмотреть: http://localhost:3000/staff/expert/inbox, /active и /waiting. В открытой карточке текущий `from` меняется автоматически вместе со статусом.
Осталось: в рамках замечания ничего.
Риски/блокеры: блокеров нет; 30-секундный refetch восстанавливает счётчики, если WebSocket временно недоступен.
```

```text
2026-09-10 00:18 +10:00 — post-acceptance — последовательная операторская «Линия»
Готово: в постоянную навигацию оператора добавлена вкладка «Линия». На стартовом экране оператор выбирает срочный, обычный или низкий приоритет без доступа к списку. Сервер атомарно закрепляет одну свободную карточку точного приоритета; после назначения, ответа или отклонения открывается следующая карточка той же линии. Реализованы состояния поиска, пустой линии, конфликта, повторной проверки и смены режима с освобождением lease и защитой несохранённых данных. Маршрут хранит выбранный режим и текущий appealId, поэтому переживает refresh. Неподтверждённые кризисные сигналы остаются в «Срочной помощи».
Проверки: frontend ESLint и production build прошли; Vitest 33/33; .NET build 0 warnings/errors; unit 38/38; live integration 31/31, включая точную фильтрацию линии по priority; Playwright ux-routing mobile+desktop 11 passed/1 ожидаемый skip, операторский regression 5 passed/5 viewport skips. На 320 px горизонтальной прокрутки нет, список `.queue-list` в линии не рендерится.
Docker: api и frontend пересобраны из текущих файлов без удаления volumes; postgres, redis, kafka, api, worker и frontend — 6/6 healthy.
Посмотреть: http://localhost:3000/staff/operator/line — выбрать «Срочные», «Обычные» или «Низкие» и работать с одной карточкой за раз.
Осталось: в рамках запроса ничего.
Риски/блокеры: нет; при отсутствии свободной карточки выбранный режим сохраняется и доступна ручная повторная проверка.
```

```text
2026-09-09 23:59 +10:00 — post-acceptance — параллельная работа операторов и компоновка detail
Готово: устранено растяжение исходного текста по высоте общей grid-строки в двухколоночной карточке. Основная и кризисная очереди получили общий Redis lease на 90 секунд с heartbeat 30 секунд, состояния «доступно / в этом окне / у другого оператора», запрет открытия занятой строки и понятный direct-link conflict. Добавлено атомарное «взять следующее»; после завершающего решения следующая свободная задача также закрепляется сервером, а не выбирается из устаревшего клиентского списка. PostgreSQL optimistic version оставлен независимой финальной защитой сохранения. Тексты обращений и контакты в Redis не помещаются.
Проверки: regression геометрии сначала падал с разрывом 213 px и после исправления прошёл; frontend ESLint, Vitest 33/33 и production build прошли; .NET build 0 warnings/errors, unit 38/38, live integration 30/30. Integration подтверждает конфликт чужого lease, освобождение и разные appealId для двух одновременных acquire-next. Operator Playwright mobile+desktop: 5 passed, 5 ожидаемых viewport skips; отдельный двухоконный сценарий подтверждает disabled busy-row и отказ прямого открытия.
Docker: API и frontend пересобраны из текущих файлов без удаления volumes; postgres, redis, kafka, api, worker и frontend — 6/6 healthy; readiness PostgreSQL/Redis/Kafka healthy. В runtime-логах нет exception/critical; записи Staff login failed созданы ожидаемыми negative integration tests.
Посмотреть: http://localhost:3000/staff/operator/queue и http://localhost:3000/staff/operator/urgent. В пустой detail-области доступны Material-действия «Взять следующее по приоритету» и «Взять следующий срочный сигнал».
Осталось: в рамках замечания ничего.
Риски/блокеры: блокеров нет; Vite chunk-size warning информационный. Если Redis недоступен, выдача операторской работы останавливается безопасно, не допуская незащищённого параллельного разбора.
```

```text
2026-09-09 22:15 +10:00 — post-acceptance — рабочая область оператора усилена по результатам проверки
Готово: устранён crash при переходе из существующей группы/правила в создание новой записи; служебные коды категорий и групп формируются автоматически. Операторская IA разделена на разбор, срочную помощь, запросы специалистов, возвраты и жалобы. В очередях появились безопасный рабочий номер ОБР-XXXXXXXX, URL-поиск и порционная выдача по 30; секретный трек-номер не раскрывается. Карточка разбора сохраняет исходный контекст рядом с решениями, завершение действий показывает фокусируемую квитанцию. Срочная карточка показывает источник и признаки риска, допускает подтверждение или снятие сигнала с причиной. Минимум причины назначения сверх лимита синхронизирован на 10 символов. Добавлены детерминированные демозапрос и жалоба.
Проверки: frontend ESLint/build, Vitest 33/33; backend build 0 warnings/errors, unit 38/38, live integration 29/29; operator Playwright mobile+desktop 4 passed/4 ожидаемых skips; release/admin/routing desktop 15 passed/3 ожидаемых skips. Ручной browser QA подтвердил навигацию, 30 из 513 в основной очереди, 30 из 140 в срочной очереди, содержательную кризисную карточку, демозапрос и отдельную очередь жалоб.
Docker: существующая PostgreSQL-база сохранена; api и frontend пересобраны, все 6 сервисов healthy.
Посмотреть: http://localhost:3000/staff/operator/queue, /urgent, /requests, /complaints.
Осталось: в рамках замечаний ничего.
Риски/блокеры: нет; Vite chunk-size warning информационный.
```

```text
2026-09-09 21:05 +10:00 — Ticket 015 — checkpoint 6/6 завершен; Ticket 015 DONE
Готово: итоговая единая информационная архитектура и Material UX для заявителя, оператора, эксперта, медиатора и администратора; непрерывная история обращения под одним трек-номером; URL-driven navigation; единая тональность «ты/вы»; обновленные DESIGN_SYSTEM.md, README.md, DEMO.md и release C1–C8.
Проверки: frontend lint, 32/32 unit и production build; backend build без warnings, 38/38 unit, 29/29 integration; полный Docker Playwright — 35 passed, 17 ожидаемых viewport skips; финальные focused suites — 13 passed и 2 passed. Browser QA, keyboard, 320 px, 200% reflow, contrast и visual constraints подтверждены.
Docker: исправлено постоянное именованное хранилище PostgreSQL 18 без удаления пользовательских данных; контрольный трек `ОТК-6DFT-ERZV` пережил отдельный down/up. Все 6 сервисов healthy, frontend 200, severe runtime errors — 0.
Посмотреть: http://localhost:3000; публичное обращение `/appeal`, оператор `/staff/operator/queue`, эксперт `/staff/expert/inbox`, администратор `/staff/admin`.
Осталось: в scope Ticket 015 ничего.
Риски/блокеры: нет; Vite chunk-size warning информационный, прежний анонимный volume сохранен как восстановительная копия.
```

```text
2026-09-09 10:13 +05:00 — Ticket 014 — checkpoint 4/4 завершен; Ticket 014 DONE; финальная приемка завершена
Готово: собран воспроизводимый release с детерминированным seed из 12 сценарных обращений; Playwright покрывает C1–C8, mobile/desktop Material/Onest, keyboard focus, accessible action names, контраст, отсутствие горизонтального scroll, градиентов и декоративных bullets. Добавлены one-command start, guarded reset, readiness всех шести сервисов, smoke, README, пошаговый DEMO и third-party notices. В ходе E2E исправлены race формы трек-номера/асинхронных действий и двойной `/api/api` при реальном скачивании аналитики.
Проверки: backend container suite — unit 38/38; live Compose integration и negative RBAC/CSRF/object access — 28/28; frontend container suite — ESLint passed, Vitest 23/23, TypeScript/Vite build passed; Playwright финального основного стенда — 8 passed, 4 ожидаемо skipped как профильные дубли, C1–C8 полностью покрыты. CSV/XLSX test проверил точный whitelist, отсутствие `PRIVATE-C8-NARRATIVE`, трек-номера и запрещенных колонок, one-time download. npm audit — 0; NuGet vulnerable scan — 0 во всех 7 проектах.
Docker: из пустых volumes проект `otklik-release-check` собрался и вышел в readiness за 59,6 с, smoke passed и 6/6 healthy. После restart сохранились 43 обращения, 9 записей вложений и 9 физических файлов. Итоговый `scripts/start-demo.ps1` обновил основной проект без удаления данных за 20,1 с; 6/6 healthy; финальный smoke passed. Основная БД: appeals=585, projections=585, analytics pending=0, notifications pending=0. Runtime error/sensitive-log matches=0.
Посмотреть: http://localhost:3000; intake http://localhost:3000/appeal/new; status http://localhost:3000/appeal/status; staff http://localhost:3000/staff; system http://localhost:3000/system. Учетки: operator / Operator!2026, expert / ExpertHelp!2026, expert.mediator / ExpertHelp!2026, administrator / AdminPanel!2026. Постоянный кризисный трек: ОТК-RUSH-DEFG; полный список — DEMO.md. Финальные QA screenshots: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket014-qa\landing-mobile.png`, `intake-mobile-ready.png`, `staff-desktop.png`.
Осталось: в scope тикетов 001–014 ничего.
Риски/блокеры: нет. Ограничения MVP: нет внешних интеграций и ML-классификации; production SLA/дежурство, эксплуатационная аттестация, внешний pentest, backup/restore и сертификация не выполнены; без добровольного контакта сервис не может направить физическую помощь.

Ticket 014 DONE
Commit/состояние: коммит не создавался; репозиторий имеет unborn history, все изменения находятся в общем рабочем дереве Goal Mode.
Реализовано: demo seed, C1–C8 E2E, mobile/desktop UI acceptance, clean/reset/readiness/smoke workflow, persistence proof и полный release handoff.
Автотесты: unit 38/38; integration/negative 28/28; frontend 23/23 + lint/build; Playwright 8 passed/4 expected skipped; dependency scans clean.
Docker-команда: powershell -ExecutionPolicy Bypass -File scripts/start-demo.ps1
Health-checks: api, worker, frontend, postgres, redis и kafka healthy; readiness и smoke passed; outboxes drained.
URL и тестовые данные: http://localhost:3000; учетки и постоянные треки приведены выше и в DEMO.md.
Ручной сценарий: C1–C8 воспроизводятся без скрытых шагов по DEMO.md; mobile landing/intake и desktop staff визуально проверены.
Известные ограничения: перечислены в README и записи выше; они находятся вне согласованного MVP.
Следующий тикет может начинаться: нет — план 001–014 завершен, общий goal готов к complete.
```

```text
2026-09-09 09:56 +05:00 — Ticket 014 — checkpoints 1–3 реализованы; начинается чистая финальная приемка
Готово: детерминированный seed с 12 обращениями во всех рабочих статусах, кризисным и диалоговыми примерами; Playwright release suite для C1–C8 на mobile/desktop; параметризованные Compose-порты и изолированный e2e-runner; readiness, start, явный reset с подтверждением потери только выбранных volumes и smoke scripts; README, DEMO и THIRD_PARTY_NOTICES. Сквозные проверки обнаружили и исправили гонки формы status/асинхронных действий, а также реальный двойной `/api/api` в скачивании аналитики.
Проверки до чистого прогона: dotnet build 0 warnings/errors; frontend lint/build и Vitest 23/23; Compose integration 28/28; изолированный стенд 6/6 healthy. Точечные Playwright: C3–C5 desktop passed, C4–C6 mobile passed, C7–C8 desktop passed.
Docker: рабочий проверочный срез доступен на http://localhost:3300 (API http://localhost:18080); запускается документированный reset только проекта `otklik-release-check`, ожидается воспроизводимый seed из пустых volumes.
Посмотреть: http://localhost:3300; основной http://localhost:3000 пока сохранен без удаления данных.
Осталось: чистый полный suite C1–C8, container test targets/audit, persistence после restart, финальный основной rebuild и запись handoff.
Риски/блокеры: нет.
```

```text
2026-09-09 08:47 +05:00 — Ticket 013 — checkpoint 4/4 завершен; Ticket 013 DONE; Ticket 014 начат
Готово: автоматизированы role/object boundaries, включая прямые API и mismatched appeal/file IDs; оператор не читает chat/notes, администратор content/contact/file, неназначенный эксперт чужой тикет. Status lookup защищен Redis: пять неуспешных попыток/60 секунд, HMAC digest адреса, одинаковая 404-форма и нарастающий Retry-After; общий public limit включен. Добавлены строгие CSP/security headers, same-origin/CORS/CSRF проверки, production HSTS, no-store, cross-platform storage-key allowlist и явное отключение EF sensitive/detailed logging. Operational secrets вынесены в ignored `.env`; final images и image ENV чистые. Результаты dependency/license audit сохранены в SECURITY_REVIEW.md.
Проверки: backend build 0 warnings/errors; unit 38/38; frontend lint/build и Vitest 23/23; live integration 28/28; Compose integration 28/28; backend и frontend container targets успешны. NuGet vulnerable scan: 0 для 7 проектов; npm audit production/full: 0. Manual negative: anonymous queue=401, operator→expert content=403, administrator→operator content/file=403, expert→unassigned=404; rate sequence=404×5→429, Retry-After=2. Redis key имел TTL=30 и attempts=6, затем автоматически исчез (`active-track-rate-keys=0`).
Docker: финальный `docker compose up --build -d`; 6/6 сервисов healthy; API опубликован только на 127.0.0.1:8080. Appeals=572, projections=572, analytics outbox pending=0, notification outbox pending=0. Sensitive log matches=0, runtime error matches=0. Root/API/offline headers подтверждены; repository имеет unborn history, VAPID private/rate-limit secret/DB password не встречаются вне ignored `.env`; TrackHash совпадает только с намеренно публичным unit-test fixture и не считается production secret.
Browser: Chromium 320 px — Onest Variable, Material actions, навигация «Обратиться / Статус / Кабинет», clientWidth=scrollWidth=320, gradients=0, list markers=0, external resources=0, CSP/page errors=0. Screenshots: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket013-qa\landing-320.png`, `status-320.png`.
Посмотреть: http://localhost:3000; статус http://localhost:3000/appeal/status; staff http://localhost:3000/staff.
Осталось: Ticket 014; он переведен в IN_PROGRESS.
Риски/блокеры: нет. Известное обновление TypeScript 7 является новым major, не security finding; зафиксирован TypeScript 6.0.3.

Ticket 013 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Автотесты: unit 38/38; frontend 23/23; live/container integration 28/28; оба container test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 healthy; analytics projection синхронизирована; outboxes, logs, rate-limit TTL и final images проверены.
URL и тестовые данные: http://localhost:3000; demo-учетки остаются в README и локальном ignored `.env`.
Следующий тикет может начинаться: да — Ticket 014 переведен в IN_PROGRESS.
```

```text
2026-09-09 08:26 +05:00 — Ticket 013 — checkpoints 1–3 реализованы, запускается проверка
Готово в коде: Redis-лимит пяти неуспешных status lookup за 60 секунд с HMAC-digest адреса и нарастающим Retry-After; общий защитный лимит публичного API; одинаковая 404-форма без отражения трек-номера. Добавлен negative suite для ролей и несовпадающих appeal/file id, CSP/security headers/same-origin/CSRF, запрета оператору notes/chat, администратору content/contact/file и эксперту неназначенного обращения. EF sensitive logging явно выключен; device cookie учитывает HTTPS reverse proxy; offline CSS вынесен из inline-стиля для строгого CSP.
Запускается: backend/frontend build и тесты, затем Compose rebuild и live/container negative suite. Ожидается рабочий срез на http://localhost:3000 без регрессии дизайна/PWA.
Осталось: устранить результаты проверок; dependency/license/secrets audit; проверить Redis TTL и логи; записать финальные четыре forbidden-запроса и 429.
Риски/блокеры: нет.
```

```text
2026-09-09 08:01 +05:00 — Ticket 012 — checkpoint 4/4 завершен; Ticket 012 DONE; Ticket 013 начат
Готово: installable mobile-first PWA, нейтральный offline shell без данных обращения, HttpOnly device capability без секрета в URL/browser storage, fallback по трек-номеру на новом устройстве, explicit push opt-in/revoke, зашифрованные subscription secrets, transactional notification outbox и browser-safe constant notification payload. UX использует Material Web/Onest, спокойные линии и двухшаговое удаление с общего устройства без градиентов, плашек, декоративных bullets или tooltip.
Проверки: backend build 0 warnings/errors; unit 35/35; frontend lint/build и Vitest 23/23; live integration 25/25; backend и frontend container test targets успешны; Compose integration 25/25. Chromium manifest errors=0, installability error только `in-incognito` headless; service worker controlled=true. Сквозной browser test 320 px: create → track → capability return → offline → revoke; private offline match=false, browser storage пуст, cache whitelist подтвержден, после revoke нужен track.
Docker: финальный `docker compose up --build -d`; 6/6 сервисов healthy; миграция `20260909022830_PwaDeviceCapability`; Appeals=462/projections=462/unpublished analytics=0/pending notifications=0; invalid token hashes=0; active test subscriptions=0; plaintext endpoint matches=0; manifest MIME строго `application/manifest+json`; error/private log matches=0.
Посмотреть: http://localhost:3000/appeal/status; screenshots `C:\Users\Admin\AppData\Local\Temp\otklik-ticket012-qa\device-return-final-320.png`, `offline-320.png`, `success-320.png`.
Осталось: Tickets 013–014; Ticket 013 переведен в IN_PROGRESS.
Риски/блокеры: нет. Push использует браузерную доставку только после согласия и никогда не заменяет основной сценарий.

Ticket 012 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: PWA installability, safe return, neutral push, permission/offline/shared-device fallbacks и documentation.
Автотесты: backend unit 35/35; live/container integration 25/25; frontend 23/23; оба container test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 контейнеров healthy; outboxes/projection синхронизированы; logs и encrypted subscription checks чистые.
URL и тестовые данные: http://localhost:3000/appeal/new → http://localhost:3000/appeal/status; трек создается в сценарии и не попадает в URL.
Ручной сценарий: подача, автоматический возврат на том же устройстве, neutral notification controls, offline shell и двухшаговый revoke проверены в Chromium на 320 px.
Известные ограничения: фактическая push-доставка зависит от permission и push-инфраструктуры браузера; при недоступности остается polling/device return/track lookup.
Следующий тикет может начинаться: да — Ticket 013 переведен в IN_PROGRESS.
```

```text
2026-09-09 07:52 +05:00 — Ticket 012 — checkpoints 1–3 завершены, checkpoint 4 готовится к контейнерной приемке
Готово: installable manifest и SVG-иконки; service worker кэширует только нейтральный offline shell и versioned assets, API/hubs/staff/navigation идут с no-store; после подачи выдается 256-bit HttpOnly SameSite=Strict capability с SHA-256 hash в PostgreSQL, без трек-номера в URL/localStorage/sessionStorage. Тот же браузер возвращает последнее обращение, новое устройство требует трек-номер, а двухшаговое удаление отзывает grants/subscriptions/cookie. Web Push подписка зашифрована общими Data Protection keys, payload и service worker принудительно используют только «Отклик / В обращении есть обновление / /appeal/status»; permission явный и неблокирующий.
Проверки: backend build 0 warnings/errors; unit 35/35; frontend lint/build и Vitest 23/23; live integration 25/25. Chromium CDP: manifest errors=0, installability errors только headless-incognito, service worker controlled=true. Полный browser flow на 320 px: create → track → device return → offline → revoke; offline private text=false, local/session storage пусты, document.cookie не содержит HttpOnly capability, cache entries только offline/manifest/icons/assets. После revoke device copy=false и снова виден ввод трек-номера.
Docker: миграция `20260909022830_PwaDeviceCapability` применена; 6/6 сервисов healthy. Mobile 320 px после коррекции Material-кнопки: clientWidth=scrollWidth=320, Onest, gradient=0, listItems=0. Screenshots: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket012-qa\device-return-final-320.png`, `offline-320.png`, `success-320.png`.
Посмотреть: http://localhost:3000/appeal/status; для полного пути сначала http://localhost:3000/appeal/new.
Осталось: backend/frontend container test targets, Compose integration/health/log/privacy audit, финальный rebuild и итоговая запись.
Риски/блокеры: нет. Реальная доставка Web Push зависит от разрешения браузера и его push-инфраструктуры; отказ или отсутствие поддержки не влияет на основной сценарий. WebPush 1.0.13 зафиксирован, VAPID dev-ключи должны заменяться вне локального стенда.
```

```text
2026-09-09 07:06 +05:00 — Ticket 011 — checkpoint 4/4 завершен; Ticket 011 DONE; Ticket 012 начат
Готово: transactional outbox в одной транзакции с Appeal, настоящий Kafka producer/consumer, idempotent PostgreSQL projection и backfill; role-scoped dashboard с метриками ТЗ и явными формулами; CSV/XLSX по 11 whitelist-столбцам; одноразовая выдача, удаление временных файлов и безопасный аудит. «Аналитика» встроена как общий вторичный раздел продуманной Material-навигации.
Проверки: backend build 0 warnings/errors; unit 33/33, включая schema и replay tests; frontend lint/build и Vitest 19/19; live/Compose integration 24/24; оба container test targets успешны. C8 проверяет eventual Kafka projection, три role scope, CSV и XLSX, whitelist, отсутствие private narrative/track, one-time download и audit. Mobile 320 px: clientWidth=scrollWidth=320, Onest, privacy/export/summary видимы, gradient=false, decorative markers=false.
Docker: `docker compose up --build -d`; 6/6 сервисов healthy; readiness direct/proxy healthy; migration `20260909013812_AnalyticsPipeline` последняя; Kafka topic partition=1/ISR=1. Финальная сверка: Appeals=349, projections=349, unpublished=0, processed events=421; forbidden outbox/audit matches=0; временных export files=0; error lines=0; sensitive log matches=0.
Посмотреть: http://localhost:3000/staff; administrator / AdminPanel!2026; раздел «Аналитика». Screenshot: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket011-qa\admin-analytics-320.png`.
Осталось: Tickets 012–014; Ticket 012 переведен в IN_PROGRESS.
Риски/блокеры: нет. ClosedXML 0.105.1 зафиксирован под MIT; ожидание заявителя включено в календарные метрики MVP и исходные timestamps сохранены.

Ticket 011 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: C8, безопасная event-driven аналитика, dashboard для трех ролей и одноразовые CSV/XLSX без содержимого обращений.
Автотесты: backend unit 33/33; live/container integration 24/24; frontend 19/19; оба container test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 контейнеров healthy; readiness healthy; Kafka и проекция синхронизированы; логи/аудит/outbox/временные файлы чистые.
URL и тестовые данные: http://localhost:3000/staff; administrator / AdminPanel!2026; dashboard за 7/30/90/365 дней и CSV/XLSX.
Ручной сценарий: открыт общий dashboard, сверены точные распределения и формулы; CSV скачан и удален с сервера; на 320 px операционная навигация отделена от аналитики.
Известные ограничения: метрики MVP календарные; одна Kafka partition соответствует текущей нагрузке MVP; push/PWA появляются в Ticket 012.
Следующий тикет может начинаться: да — Ticket 012 переведен в IN_PROGRESS.
```

```text
2026-09-09 07:01 +05:00 — Ticket 011 — checkpoints 2–3 завершены, checkpoint 4 готовится к контейнерной приемке
Готово: role-scoped metrics API и dashboard для всех трех ролей; количество, active workload, распределения, календарное время до оператора/первого ответа/закрытия, доля Urgent и возвратов; CSV/XLSX строго по whitelist, одноразовая выдача, удаление временного файла и аудит создания/скачивания. Общая «Аналитика» отделена от операционной навигации; Material UI использует Onest, линии и точные таблицы без градиентов, плашек и декоративных маркеров.
Проверки: backend build 0 warnings/errors; unit 33/33; frontend lint/build и Vitest 19/19; live integration 24/24. API smoke: Administrator 307, Operator 147, Expert 42 до тестовых прогонов; role scopes различаются. CSV header соответствует 11 разрешенным столбцам, forbidden fields=false; серверный файл удален после скачивания. ClosedXML 0.105.1 подтверждает MIT. После полного live suite: Appeals=328, projections=328, unpublished=0, processed events=364, запрещенных ключей/PRIVATE-C8 в outbox=0.
Docker: актуальные api/worker/frontend развернуты и healthy. Mobile 320 px: clientWidth=scrollWidth=320, Onest; admin navigation 2×2 плюс отдельная полная строка «Аналитика»; privacy/export/summary видимы; gradient=false, decorative markers=false.
Посмотреть: http://localhost:3000/staff; administrator / AdminPanel!2026; раздел «Аналитика». Screenshot: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket011-qa\admin-analytics-320.png`.
Осталось: оба container test target, Compose integration 24/24, финальный compose rebuild, health/log/privacy cleanup и итоговая запись.
Риски/блокеры: нет.
```

```text
2026-09-09 06:40 +05:00 — Ticket 011 — checkpoint 1/4 завершен в Docker
Готово: типизированный whitelist event schema без текстовых полей; автоматическая запись snapshot-event в transactional outbox при изменении Appeal; Kafka topic с idempotent producer; consumer с ручным commit; PostgreSQL projection и дедупликация по eventId; безопасный исторический backfill через тот же Kafka-путь.
Проверки: backend build — 0 warnings/errors; миграция `20260909013812_AnalyticsPipeline` применена. В Docker topic `otklik.appeal-analytics.v1` имеет 1 partition/ISR; `Appeals=307`, `AppealAnalyticsProjections=307`, `unpublished outbox=0`, `ProcessedAnalyticsEvents=307`.
Docker: актуальные api/worker пересобраны без удаления volume; API healthy, worker публикует и потребляет реальные Kafka events.
Посмотреть: текущий UI остается на http://localhost:3000/staff; dashboard появится в checkpoint 3.
Осталось: scoped metrics API/read model, dashboard, CSV/XLSX, schema/idempotency/role/export tests и финальная C8.
Риски/блокеры: нет. Исторический backfill читает контент только внутри доверенного worker для вычисления timestamp первого ответа; event payload и projection содержат исключительно whitelist metadata.
```

```text
2026-09-09 06:28 +05:00 — Ticket 010 — checkpoint 4/4 завершен; Ticket 010 DONE; Ticket 011 начат
Готово: административный контур из четырех постоянных разделов; CRUD/deactivation категорий и групп, membership/лимиты, версионируемые правила со снимком на обращении; учетные записи с одной ролью, availability, блокировкой/восстановлением и немедленным отзывом сессии; безопасное вмешательство в зависшие обращения; фильтруемый read-only аудит. UX закреплен в дизайн-системе: Onest, Material Web, 2×2 mobile navigation, спокойные линии без градиентов, status-плашек и декоративных маркеров.
Проверки: backend build — 0 warnings/errors; unit 30/30; frontend lint/build и Vitest 18/18; live/Compose integration 23/23; backend и frontend container test targets успешны. C7 вручную: категория «Социальная изоляция», эксперт `expert.isolation`, группа и v1→v2 правила; ОТК-2ATQ-8PBM сохранил v1, текущий маршрут использует v2. Заблокированный сотрудник немедленно потерял сессию; аудит вмешательства содержит только статус, приоритет, категорию, назначение и версии. SQL sensitive match — 0.
Docker: `docker compose up --build -d`; api, worker, frontend, postgres, redis и kafka healthy; direct/proxy readiness healthy; последняя миграция `20260909004655_AdminConfigurationAudit`; API/worker error lines — 0, sensitive log matches — 0.
Посмотреть: http://localhost:3000/staff; administrator / AdminPanel!2026; «Конфигурация», «Сотрудники», «Зависшие», «Журнал». Mobile QA PNG: `C:\Users\Admin\AppData\Local\Temp\otklik-ticket010-qa\admin-configuration-320.png` и `admin-audit-320.png`.
Осталось: Tickets 011–014; Ticket 011 переведен в IN_PROGRESS.
Риски/блокеры: нет.

Ticket 010 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: полный C7, безопасное администрирование конфигурации/сотрудников/зависших и audit trail без доступа к содержимому обращений.
Автотесты: backend unit 30/30; live/container integration 23/23; frontend 18/18; оба container test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 контейнеров healthy; readiness через :8080 и :3000 healthy; миграция AdminConfigurationAudit актуальна; логи чистые.
URL и тестовые данные: http://localhost:3000/staff; administrator / AdminPanel!2026; ОТК-2ATQ-8PBM; expert.isolation / SupportHelp!2026.
Ручной сценарий: создана конфигурация маршрута и сотрудник; отправлено обращение; изменение правила не переписало снимок; зависший тикет безопасно изменен с причиной и проверен в журнале.
Известные ограничения: внешние передачи отсутствуют по scope; физического удаления использованных конфигурационных сущностей нет; экспорт и агрегаты появятся в Ticket 011.
Следующий тикет может начинаться: да — Ticket 011 переведен в IN_PROGRESS.
```

```text
2026-09-09 06:22 +05:00 — Ticket 010 — checkpoints 1–4 реализованы, начинается финальная контейнерная приемка
Готово: CRUD и деактивация категорий/групп, membership/лимиты, версионируемые правила со снимком на обращении; учетные записи с одной ролью, доступностью, блокировкой/восстановлением и немедленным отзывом сессии; безопасный список зависших, обязательная причина вмешательства и read-only аудит до/после. Административный API не возвращает тексты, чат, заметки, файлы и контакты.
Проверки: backend build — 0 warnings/errors; unit 30/30; frontend lint/build и Vitest 18/18; live integration 23/23. В браузере создана категория «Социальная изоляция», эксперт `expert.isolation`, группа и две версии правила; обращение ОТК-2ATQ-8PBM сохранило снимок v1, при этом текущая подсказка использует v2. Вмешательство в зависшее обращение записало только безопасные метаданные. Mobile 320 px: scrollWidth=clientWidth=320, Onest, навигация 2×2, запрещенных градиентов/маркеров нет.
Docker: миграция `20260909004655_AdminConfigurationAudit` применена; основной стенд из checkpoint healthy. Финальный rebuild после документации и тестового уточнения еще выполняется.
Посмотреть: http://localhost:3000/staff; administrator / AdminPanel!2026; разделы «Конфигурация», «Сотрудники», «Зависшие», «Журнал».
Осталось: backend/frontend container test targets, Compose integration 23/23, окончательный compose up, health/log/privacy checks.
Риски/блокеры: нет.
```

```text
2026-09-09 05:31 +05:00 — Ticket 010 — checkpoint 1/4 готов в рабочем дереве, начинается Docker-проверка
Готово: безопасная административная модель и API для категорий, групп, membership и версионируемых правил; снимок версии правила закрепляется на обращении; добавлены Material-интерфейс «Конфигурация» и новый 2×2 mobile navigation foundation; подготовлена последовательная миграция AdminConfigurationAudit.
Проверки: dotnet build — 0 warnings/errors; frontend lint и production build успешны; административный экран вынесен в отдельный lazy chunk. Docker и живой UI этого checkpoint еще проверяются.
Docker: запускается пересборка API/frontend и применение миграции без удаления volume; последний стабильный срез Ticket 009 остается доступным до завершения recreate.
Посмотреть: после сборки — http://localhost:3000/staff, administrator / AdminPanel!2026, раздел «Конфигурация».
Осталось: browser CRUD tracer bullet; аккаунты и availability; зависшие обращения; журнал, C7 и финальные тесты.
Риски/блокеры: нет.
```

```text
2026-09-09 05:31 +05:00 — Ticket 009 — checkpoint 4/4 завершен; Ticket 009 DONE; Ticket 010 начат
Готово: конфигурируемые crisis markers и телефоны помощи; локальная и серверная проверка исходного текста, структурированных ответов и поздних сообщений; спокойная inline-карточка помощи; отдельный зашифрованный добровольный контакт; operator-only чтение с аудитом; кризисная очередь с таймером и ручным Urgent; помощь без контакта продолжается в анонимном чате.
Проверки: backend build 0 warnings/errors; unit 30/30; frontend lint/build passed и Vitest 17/17; live и container integration 22/22; backend/frontend container test targets успешны. Expert и Administrator получают 403 на контакт; нейтральный текст не получает flag; структурированный кризисный ответ и позднее сообщение поднимают тикет без автоматического Urgent.
Docker: `docker compose up --build -d` завершен из актуальных файлов; api, worker, frontend, postgres, redis и kafka healthy; direct/proxy readiness healthy; миграция CrisisFlow последняя; error lines 0, sensitive log matches 0. SQL подтвердил ciphertext length=198, plaintext position=0, access audit=1.
Посмотреть: http://localhost:3000/appeal/new; http://localhost:3000/appeal/status; http://localhost:3000/staff. С контактом: ОТК-444Y-SY73. Без контакта и с продолженным анонимным диалогом: ОТК-NMJ2-QBQU. Operator: operator / Operator!2026.
Осталось: Tickets 010–014; Ticket 010 переведен в IN_PROGRESS.
Риски/блокеры: нет. Mobile QA на 320 px: clientWidth=scrollWidth=320, Onest, карточка доступна, операторская навигация 2×2, градиентов и декоративных маркеров нет.

Ticket 009 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: C6 с двумя ветками, server-side marker detection, crisis support UI, отдельная очередь, manual Urgent, encrypted contact storage и access audit без деанонимизации/внешних передач.
Автотесты: backend unit 30/30; live/container integration 22/22; frontend 17/17; оба container test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 контейнеров healthy; readiness через :8080 и :3000 healthy; логи чистые, актуальная миграция 20260908182838_CrisisFlow.
URL и тестовые данные: http://localhost:3000/appeal/new; ОТК-444Y-SY73; ОТК-NMJ2-QBQU; operator / Operator!2026.
Ручной сценарий: карточка возникла до отправки и не заблокировала форму; оператор отдельно раскрыл и подтвердил срочность обращения с контактом; обращение без контакта было принято экспертом и продолжило безопасный анонимный диалог.
Известные ограничения: внешние передачи отсутствуют по scope; без добровольного контакта/местонахождения сервис честно не обещает физическое вмешательство.
Следующий тикет может начинаться: да — Ticket 010 переведен в IN_PROGRESS.
```

```text
2026-09-09 05:31 +05:00 — Ticket 009 — checkpoint 1/4 в рабочем дереве, готовится сквозной UI
Готово: добавлены конфигурируемые таблицы CrisisMarkers/CrisisSupportContacts с детерминированным seed; нормализованный matcher поддерживает словоформы через stems и одну частую опечатку; добавлены crisis flag/detectedAt, отдельные зашифрованные контакты и аудит чтения; создана последовательная миграция 20260908182838_CrisisFlow; API-компиляция проходит.
Проверки: dotnet build — 0 warnings/errors; окончательные unit/integration/container checks еще не запускались после frontend-среза.
Docker: последний подтвержденный стенд Ticket 008 остается healthy на http://localhost:3000; новые изменения Ticket 009 еще не развернуты.
Посмотреть: http://localhost:3000 — последний стабильный срез; кризисный UI появится в checkpoint 2.
Осталось: frontend detection/help panel; crisis contact API/UI; кризисная операторская очередь; C6, privacy/RBAC и Docker smoke.
Риски/блокеры: нет; прошлый ход был прерван после миграции, состояние восстановлено по файлам и build.
```

```text
2026-09-08 23:15 +05:00 — Ticket 008 — checkpoint 4/4 завершен; Ticket 008 DONE; Ticket 009 начат
Готово: равнозначные исходы «Это помогло / Это не помогло»; атомарное закрытие и оценка; структурированный возврат с максимумом двух циклов; очередь возвратов, повторное назначение и финальное решение оператора; отдельная жалоба, скрытая от эксперта; настройка срока закрытия и worker-развилка для обычных/кризисных обращений.
Проверки: backend build 0 warnings/errors; unit 22/22; frontend lint/build passed и Vitest 9/9; live integration 20/20; backend/frontend container test targets passed; container integration 20/20. Гонки помогло/не помогло, отзыв доступа старого эксперта и privacy жалобы покрыты тестами.
Docker: все 6 сервисов healthy на актуальной миграции ApplicantOutcomeLifecycle; настройка Appeal.AutoCloseDays=7. На 320 px public outcome имеет clientWidth=scrollWidth=320, Onest, без градиентов и декоративных list markers.
Посмотреть: http://localhost:3000/appeal/status; завершенный с оценкой и жалобой ОТК-TRST-2345; возвращенный, переназначенный, получивший версию 2 и завершенный ОТК-MMBU-SRUV; операторская очередь http://localhost:3000/staff.
Осталось: Tickets 009–014; Ticket 009 переведен в IN_PROGRESS.
Риски/блокеры: нет. QA screenshot сохранен вне рабочего дерева: C:/Users/Admin/AppData/Local/Temp/otklik-ticket008-qa/public-outcome-320.png.

Ticket 008 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: applicant outcomes, feedback, complaints, два возврата, reassign/final close, версии рекомендаций, auto-close policy/worker и операторская навигация «Возвраты».
Автотесты: backend unit 22/22; live/container integration 20/20; frontend 9/9; оба container test targets успешны.
Docker-команда: docker compose up --build -d; docker compose --profile tests build integration-tests; docker compose --profile tests run --rm integration-tests.
Health-checks: api, worker, frontend, postgres, redis и kafka healthy; readiness healthy; актуальные логи без ошибок и содержимого обращений.
URL и тестовые данные: http://localhost:3000/appeal/status; ОТК-TRST-2345 и ОТК-MMBU-SRUV; operator / Operator!2026.
Ручной сценарий: первое обращение закрыто с оценкой и отдельной жалобой; второе возвращено, прежний эксперт потерял доступ, оператор назначил другого, заявитель увидел версии 1 и 2 и завершил обращение.
Известные ограничения: кризисная карточка и изолированный добровольный контакт реализуются в Ticket 009.
Следующий тикет может начинаться: да — Ticket 009 переведен в IN_PROGRESS.
```

```text
2026-09-08 22:46 +05:00 — Ticket 007 — checkpoint 4/4 завершен; Ticket 007 DONE; Ticket 008 начат
Готово: структурированные запросы Transfer/CoExecutor/PriorityReview; отдельная операторская навигация «Очередь / Запросы»; профильный выбор специалиста; один ответственный; coexecutor читает рабочую историю/чат/заметки и пишет, но не публикует итог; append-only события назначения; удаление участника; Redis presence и возобновляемый composer lease с TTL и серверной проверкой.
Проверки: backend build 0 warnings/errors; unit 18/18; frontend lint/build passed и Vitest 9/9; live integration 18/18; backend/frontend container test targets passed; container integration 18/18. Автотест проверяет приватность оператора, coexecutor access, конфликт lease, удаление, передачу, 403 прежнему эксперту на сообщение и отсутствие имен публично.
Docker: миграция ExpertCollaboration применена без удаления volume; все 6 сервисов healthy; readiness healthy; API/worker error lines 0, sensitive matches 0; запрещенные градиенты 0.
Посмотреть: http://localhost:3000/staff; operator / Operator!2026; expert / ExpertHelp!2026; demo 50000000-0000-0000-0000-000000000001, track ОТК-TRST-2345 сейчас передан expert.
Осталось: Tickets 008–014; Ticket 008 переведен в IN_PROGRESS.
Риски/блокеры: нет. Два независимых headless Chrome-сеанса на 320 px подтвердили role/presence, Onest и блокировку редактора; clientWidth=scrollWidth=320. QA screenshots сохранены вне рабочего дерева.

Ticket 007 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: workflow requests/decisions, active participants, неизменяемая история событий, coexecutor permissions, transfer/revoke, Redis presence и renewable lease.
Автотесты: backend unit 18/18; live/container integration 18/18; frontend 9/9; оба container test targets успешны.
Docker-команда: docker compose up --build -d; docker compose --profile tests build integration-tests; docker compose --profile tests run --rm integration-tests.
Health-checks: api, worker, frontend, postgres, redis и kafka healthy; readiness healthy; актуальные логи без ошибок и содержимого обращений.
URL и тестовые данные: http://localhost:3000/staff; operator, expert, expert.mediator; ОТК-TRST-2345.
Ручной сценарий: ответственный запросил соисполнителя; оператор подтвердил без доступа к заметке/чату; два staff-сеанса показали presence и lease-блокировку; затем оператор передал обращение, старый эксперт потерял доступ, новый увидел историю.
Известные ограничения: возврат заявителя «не помогло» и закрытие появляются в Ticket 008.
Следующий тикет может начинаться: да — Ticket 008 переведен в IN_PROGRESS.
```

```text
2026-09-08 22:07 +05:00 — Ticket 006 — checkpoint 4/4 завершен; Ticket 006 DONE; Ticket 007 начат
Готово: «Мои обращения» с фильтрами и срочными сверху; initial/detail/attachments; принятие в работу; отдельные публичный чат и закрытые заметки; idempotent сообщения/заметки; автоматический NeedsClarification↔InProgress; версии рекомендаций и RecommendationReady; SignalR с rejoin после reconnect через WebSocket proxy.
Проверки: backend build 0 warnings/errors; unit 18/18; frontend lint/build passed и Vitest 9/9; live integration 17/17; backend/frontend container test targets passed. Permission tests: чужой эксперт 404, operator/admin 403, публичный payload без заметок и имен, повторы команд без дублей.
Docker: актуальный frontend/API подняты; ExpertConversation и ExpertCommandIds применены; все 6 контейнеров healthy; SignalR negotiate=200 и WebSocket upgrade=101; API/worker error lines — 0, sensitive log matches — 0.
Посмотреть: http://localhost:3000/staff; expert.mediator / ExpertHelp!2026; demo id 50000000-0000-0000-0000-000000000001, track ОТК-TRST-2345; публичный итог на /appeal/status.
Осталось: Tickets 007–014; Ticket 007 переведен в IN_PROGRESS.
Риски/блокеры: нет. Проверка 320 px: expert queue, detail и public status имеют clientWidth=scrollWidth=320; явный возврат к списку; Onest; заметка отсутствует в публичном DOM. QA screenshots находятся вне рабочего дерева.

Ticket 006 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: scoped expert workspace, initial/detail, private notes, anonymous idempotent chat, status transitions, versioned recommendations, SignalR updates/reconnect.
Автотесты: backend unit 18/18; live integration 17/17; frontend 9/9; оба container test targets успешны.
Docker-команда: docker compose up --build -d; docker compose --profile tests run --rm integration-tests
Health-checks: api, worker, frontend, postgres, redis и kafka healthy; readiness healthy; SignalR 200/101 подтвержден; logs без ошибок и содержимого обращений.
URL и тестовые данные: http://localhost:3000/staff; expert.mediator / ExpertHelp!2026; ОТК-TRST-2345.
Ручной сценарий: эксперт берет назначение, оставляет внутреннюю заметку, задает вопрос; заявитель отвечает по track; эксперт видит ответ и публикует рекомендацию версии 1; заявитель видит диалог/рекомендацию без заметки и имени.
Известные ограничения: передача, соисполнитель и presence появляются в Ticket 007.
Следующий тикет может начинаться: да — Ticket 007 переведен в IN_PROGRESS.
```

```text
2026-09-08 21:56 +05:00 — Ticket 006 — checkpoints 1–4 собраны; начинается Docker-приемка
Готово: эксперт видит только свои назначения и фильтры; карточка включает исходные данные/вложения/историю; чат, заметки и версии рекомендаций физически разделены; принятие, вопрос, ответ заявителя и публикация рекомендации реализуют переходы статусов; все команды idempotent; SignalR обновляет только после REST-записи.
Проверки: backend build 0 warnings/errors; unit 18/18; frontend lint/build passed, Vitest 9/9. Добавлены два live integration сценария: permission matrix и полный idempotent conversation.
Docker: следующий шаг — docker compose up --build -d применит ExpertConversation и ExpertCommandIds без удаления данных.
Посмотреть: после rebuild — http://localhost:3000/staff; expert.mediator / ExpertHelp!2026; заявитель возвращается через http://localhost:3000/appeal/status.
Осталось: container/live tests, browser C4/chat C5, публичная приватность, SignalR smoke, 320 px, logs/health.
Риски/блокеры: нет.
```

```text
2026-09-08 21:28 +05:00 — Ticket 005 — checkpoint 4/4 завершен; Ticket 005 DONE; Ticket 006 начат
Готово: полная операторская очередь и карточка только с первоначальными данными; отдельная подсказка категории; группы, нагрузка и предупреждение о лимите; triage, атомарное назначение, reasoned override, ответ без эксперта и отклонение с приватной внутренней причиной. README обновлен до текущего среза.
Проверки: backend build 0 warnings/errors; unit 18/18; frontend lint/build passed и Vitest 9/9; live integration 15/15. Повторный container-run обнаружил зависимость concurrency-теста от накопленной нагрузки — тест изолирован явным reasoned override и повторно прошел 15/15 на непустой базе.
Docker: docker compose up --build -d; backend/frontend test targets passed; шесть основных контейнеров healthy; readiness healthy; API/worker error lines за 15 минут — 0; трек-номер и внутренняя причина в логах не найдены.
Посмотреть: http://localhost:3000/staff; operator / Operator!2026. Назначено ОТК-TRST-2345 (id 50000000-0000-0000-0000-000000000001); отклонено ОТК-CARE-6789 (id 50000000-0000-0000-0000-000000000002); публичный ответ доступен на /appeal/status.
Осталось: Tickets 006–014; Ticket 006 переведен в IN_PROGRESS.
Риски/блокеры: нет. Mobile queue и detail проверены при 320 CSS px: clientWidth=scrollWidth=320, detail имеет явный возврат к очереди, используется Onest Variable; QA screenshots находятся вне рабочего дерева.

Ticket 005 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: очередь/sorting/overdue, initial-only detail, category suggestion, routing groups/load/capacity, optimistic version, operator actions и публичный бережный итог.
Автотесты: backend unit 18/18; live integration 15/15; frontend 9/9; оба container test targets успешны.
Docker-команда: docker compose up --build -d; docker compose --profile tests run --rm integration-tests
Health-checks: api, worker, frontend, postgres, redis и kafka healthy; readiness healthy; актуальные API/worker logs без ошибок.
URL и тестовые данные: http://localhost:3000/staff; operator / Operator!2026; ОТК-TRST-2345 и ОТК-CARE-6789.
Ручной сценарий: открыть очередь, сохранить разбор, назначить первое обращение; отклонить второе; по его трек-номеру увидеть только публичный бережный ответ.
Известные ограничения: эксперт еще не может работать с назначенным обращением — это Ticket 006.
Следующий тикет может начинаться: да — Ticket 006 переведен в IN_PROGRESS.
```

```text
2026-09-08 21:16 +05:00 — Ticket 005 — checkpoints 1–3 и API действий собраны; финальная приемка C3
Готово: операторская очередь с сортировкой, временем ожидания и порогом 4 часа; карточка только с первоначальными данными/вложениями; отдельная словарная подсказка категории; две экспертные группы, правила category→group, ранжирование по нагрузке и лимит; optimistic version; triage, atomic assignment, reasoned capacity override, reject и ответ без эксперта; публичный статус показывает только бережный итоговый ответ.
Проверки: backend build 0 warnings/errors; unit 18/18; live integration 15/15, включая RBAC, отсутствие chat/notes в operator payload, конфликт двух операторов с разными экспертами, приватность внутренней причины и превышение лимита. Frontend lint/build passed, Vitest 9/9.
Docker: миграция OperatorTriageRouting применена без удаления данных; development seed добавил три очередных обращения, две группы и двух экспертов; все основные сервисы запущены.
Посмотреть: http://localhost:3000/staff; operator / Operator!2026; очередь и карточка уже доступны, демо ОТК-TRST-2345 назначено через browser.
Осталось: пересобрать последние frontend-правки, отклонить второе demo через browser, проверить applicant status, 320 px и container-only tests.
Риски/блокеры: нет.
```

```text
2026-09-08 20:43 +05:00 — Ticket 004 — checkpoint 4/4 и дизайн-фундамент завершены; Ticket 004 DONE; Ticket 005 начат
Готово: полный C1 с приватным вложением; ограничения и очистка metadata; сквозная дизайн-система с семантическими Material-токенами, локальным variable-font Onest с кириллицей, общей публичной навигацией и отдельным служебным контекстом. Технический health вынесен с лендинга на /system; правила зафиксированы в src/frontend/DESIGN_SYSTEM.md.
Проверки: frontend lint/build passed, host и container Vitest 9/9; live integration 10/10; пять маршрутов проверены при 320 CSS px — clientWidth=scrollWidth=320, navRight=308; body и Material-button используют Onest Variable; desktop и mobile screenshots осмотрены; запрещенные градиенты/status-пилюли/декоративные точки отсутствуют.
Docker: docker compose up --build -d frontend; frontend/api/postgres/redis/kafka/worker healthy; API system status healthy; API/worker error lines за 15 минут — 0.
Посмотреть: http://localhost:3000, /appeal/new, /appeal/status, /staff и /system; демо с вложением — ОТК-68QY-CSTB.
Осталось: Tickets 005–014; Ticket 005 переведен в IN_PROGRESS.
Риски/блокеры: нет. QA screenshots и CDP-профиль находятся вне рабочего дерева во временном каталоге.

Ticket 004 DONE
Commit/состояние: коммит не создавался; изменения сохранены в общем рабочем дереве Goal Mode.
Реализовано: upload/preview/progress/remove/retry, private storage, серверная content validation, decode/re-encode изображений, безопасная выдача; дополнительно по прямой обратной связи — продуктовый UI/UX-фундамент без изменения границы MVP.
Автотесты: frontend 9/9; backend unit 12/12; live integration 10/10; frontend container test target успешен.
Docker-команда: docker compose up --build -d frontend; docker compose --profile tests run --rm integration-tests
Health-checks: 6/6 основных контейнеров healthy; system status healthy; актуальные API/worker logs без ошибок.
URL и тестовые данные: http://localhost:3000/appeal/new; http://localhost:3000/appeal/status; ОТК-68QY-CSTB.
Ручной сценарий: выбрать тип и свободный текст, приложить PNG, увидеть preview, отправить, сохранить трек, открыть статус, скачать очищенную копию.
Известные ограничения: операторская обработка обращения начинается в Ticket 005.
Следующий тикет может начинаться: да — Ticket 005 переведен в IN_PROGRESS.
```

```text
2026-09-08 20:18 +05:00 — Ticket 004 — функциональная приемка пройдена; начат сквозной фундамент дизайн-системы
Готово: Docker-миграция и private volume применены; live integration 10/10; upload idempotency, лимиты, private POST-download и безопасные headers проверены; browser C1 с реальным PNG пройден на 320 CSS px без горизонтального скролла. Все 6 основных контейнеров healthy; frontend tests 7/7, backend unit tests 12/12.
Проверки: тестовый track ОТК-U6ZZ-XR7B прошел полный browser-flow подача → загрузка → статус; отдельный ОТК-68QY-CSTB подтвердил download и отсутствие исходного имени/текста в логах.
Docker: рабочий срез доступен на http://localhost:3000; backend test image успешно собран из актуальных файлов.
Посмотреть: http://localhost:3000/appeal/new и http://localhost:3000/appeal/status.
Осталось: по прямой обратной связи заказчика до дальнейших продуктовых тикетов создать единую дизайн-систему, исправить кириллический шрифт и навигационный UX на всех текущих экранах; после этого повторить приемку и закрыть Ticket 004.
Риски/блокеры: нет; функциональность Ticket 004 не меняется.
```

```text
2026-09-08 20:03 +05:00 — Ticket 004 — checkpoints 1–3 собраны, начинается Docker-проверка
Готово: mobile upload с превью/прогрессом/удалением; серверная проверка содержимого, расширения, MIME, 5×10 МБ; decode/re-encode PNG/JPEG без EXIF/GPS; строгая базовая проверка PDF; случайные storage keys, приватный storage interface/volume; POST-download с track-проверкой и attachment disposition. Исходное имя не сохраняется.
Проверки: backend build 0 warnings/errors; sanitizer unit tests 4/4, всего backend unit 12/12; frontend lint/build passed, Vitest 7/7. Актуальный managed-кодек выбран с permissive Unlicense/MIT вместо зависимости, требующей отдельной коммерческой лицензии.
Docker: начинается rebuild и применение миграции PrivateAttachments.
Посмотреть: после rebuild — шаг 3 на http://localhost:3000/appeal/new и вложения на http://localhost:3000/appeal/status.
Осталось: live integration, реальный файл в browser/mobile, проверка private headers/metadata/logs и полный C1.
Риски/блокеры: нет.
```

```text
2026-09-08 19:44 +05:00 — Ticket 003 — checkpoint 4/4 завершен; Ticket 003 DONE; Ticket 004 начат
Готово: mobile-first Material-форма с тремя типами заявителя, двумя равноправными путями, необязательными уточнениями, бережным ты/вы; idempotent создание обращения; CSPRNG `ОТК-XXXX-XXXX`, HMAC lookup; сохранение номера и POST-страница статуса. По прямому уточнению заказчика отсутствуют градиенты, status-плашки, декоративные маркеры и полупрозрачное задвоение.
Проверки: frontend lint/build passed, Vitest 4/4; backend unit 8/8; live integration 8/8; container test-targets passed; браузерные C1 без вложения и C2 пройдены; viewport 320 CSS px — clientWidth=320, scrollWidth=320; запрещенные CSS-паттерны — 0; API/worker error lines — 0; тестовая проверка логов — 0 совпадений трек-номера и текста.
Docker: `docker compose up --build -d`; все 6 основных контейнеров healthy; `docker compose --profile tests build integration-tests` и `docker compose --profile tests run --rm integration-tests` — 8/8 passed.
Посмотреть: http://localhost:3000/appeal/new и http://localhost:3000/appeal/status; школьник/свободный текст — ОТК-3DDW-DGT5; родитель/«Конфликт» — ОТК-BH6H-J3XW.
Осталось: Tickets 004–014; Ticket 004 переведен в IN_PROGRESS.
Риски/блокеры: нет. Вспомогательные QA-артефакты перемещены из репозитория во временный каталог после проверки.

Ticket 003 DONE
Commit/состояние: коммит не создавался; изменения находятся в общем рабочем дереве Goal Mode.
Реализовано: доменная модель обращения/категорий/ответов/истории; EF migration; публичные options/create/status endpoints; защищенный трек; React intake/success/status UI.
Автотесты: frontend 4/4; backend unit 8/8; live integration полный набор 8/8; Docker test targets успешны.
Docker-команда: docker compose up --build -d
Health-checks: api, worker, frontend, postgres, redis, kafka — healthy; за последние 15 минут ошибок API/worker не найдено.
URL и тестовые данные: http://localhost:3000/appeal/new; http://localhost:3000/appeal/status; ОТК-3DDW-DGT5 и ОТК-BH6H-J3XW.
Ручной сценарий: подать школьником свободный текст без уточнений; сохранить номер; открыть статус; затем подать родителем категорию «Конфликт» без текста и открыть статус.
Известные ограничения: вложения появятся в Ticket 004; чат и работа сотрудников — в последующих тикетах.
Следующий тикет может начинаться: да — Ticket 004 переведен в IN_PROGRESS.
```

```text
2026-09-08 19:24 +05:00 — Ticket 003 — checkpoints 1–4 реализованы, начинается общая проверка
Готово: доменная модель, миграция, категории, два пути формы, необязательные уточнения, ты/вы, idempotent POST, CSPRNG трек-номер с HMAC-хешем и POST-проверка статуса; новые страницы приведены к спокойной Material-системе без градиентов, плашек и декоративных маркеров.
Проверки: backend unit tests 8/8; frontend ESLint passed, Vitest 4/4, production build passed; ранее API smoke подтвердил создание, replay и lookup.
Docker: текущие 6 контейнеров healthy; начинается rebuild frontend и test-image из актуальных файлов.
Посмотреть: после rebuild — http://localhost:3000/appeal/new и http://localhost:3000/appeal/status.
Осталось: live integration tests, mobile/desktop browser smoke, проверка логов, фиксация двух демо-номеров и финальный Docker health.
Риски/блокеры: нет.
```

```text
2026-09-08 17:37 +05:00 — Ticket 002 — checkpoint 4/4 завершен; Ticket 002 DONE; Ticket 003 начат
Готово: документированы dev-учетки; добавлены black-box auth/RBAC integration tests и отдельный Compose test-service; worker runtime приведен в соответствие с ASP.NET Identity dependency.
Проверки: backend build — 0 warnings/0 errors; live integration tests — 5/5 passed на хосте и 5/5 в Docker; frontend lint/build — passed, Vitest 1/1; browser smoke трех ролей на 320 px и desktop на 1024 px.
Docker: `docker compose up --build -d`; все 6 основных контейнеров healthy; `docker compose --profile tests run --rm integration-tests` — passed.
Посмотреть: http://localhost:3000/staff; operator / Operator!2026, expert / ExpertHelp!2026, administrator / AdminPanel!2026.
Осталось: продуктовые тикеты 003–014.
Риски/блокеры: нет; в актуальных API/worker logs 0 error lines и 0 совпадений dev-паролей/session-cookie.

Ticket 002 DONE
Commit/состояние: коммит не создавался; изменения находятся в рабочем дереве поверх Ticket 001.
Реализовано: Identity schema/migration/seed, HttpOnly SameSite cookie, CSRF, login/logout/me, server policies, role кабинеты и Material-адаптивная навигация.
Автотесты: auth/RBAC integration 5/5; frontend 1/1; container build targets успешны.
Docker-команда: docker compose up --build -d; docker compose --profile tests run --rm integration-tests
Health-checks: api, worker, frontend, postgres, redis, kafka — healthy; readiness=healthy.
URL и тестовые данные: http://localhost:3000/staff; три логина и dev-пароля приведены в README и checkpoint выше.
Ручной сценарий: войти каждой учетной записью, увидеть свой кабинет; чужой API получает 403; logout возвращает к форме входа.
Известные ограничения: кабинеты пока показывают только role-specific заглушки без обращений; это покрывается тикетами 004–008.
Следующий тикет может начинаться: да — Ticket 003 переведен в IN_PROGRESS.
```

```text
2026-09-08 17:25 +05:00 — Ticket 002 — checkpoint 3/4 завершен
Готово: React staff guard, форма входа и отдельные кабинеты Operator/Expert/Administrator; TanStack Query хранит сессию, 401 превращается в завершенную сессию без retry-loop; logout очищает client cache.
Проверки: через browser UI выполнен вход каждой ролью; заголовки/навигация соответствуют роли; mobile 320 px и desktop 1024 px без horizontal page scroll.
Docker: frontend пересобран и доступен на http://localhost:3000/staff; API role overview вызывается через same-origin proxy.
Посмотреть: http://localhost:3000/staff; operator / Operator!2026, expert / ExpertHelp!2026, administrator / AdminPanel!2026.
Осталось: автоматические auth/RBAC tests, README, container-only tests и полный Compose rebuild.
Риски/блокеры: нет. По уточнению заказчика лендинг и кабинеты переведены на спокойную Material-палитру; градиенты, status-пилюли, декоративные буллеты и полупрозрачные ореолы удалены.
```

```text
2026-09-08 17:13 +05:00 — Ticket 002 — checkpoint 2/4 завершен
Готово: endpoints csrf/login/logout/me и три role overview защищены Identity cookie и серверными policies; API вместо redirect возвращает 401/403; state-changing auth-запросы требуют CSRF.
Проверки: все три роли — login 200, свой route 200, чужой route 403; anonymous me 401; login без CSRF 400; logout 204 и следующий me 401; staff cookie HttpOnly=true; dev-пароли в логах не найдены.
Docker: проверки выполнены через same-origin reverse proxy http://localhost:3000/api; API healthy.
Посмотреть: пока технический экран http://localhost:3000; UI входа и кабинетов — следующий checkpoint.
Осталось: три role UI, frontend guards/session expiry, интеграционные тесты и финальный Docker smoke.
Риски/блокеры: нет.
```

```text
2026-09-08 17:11 +05:00 — Ticket 002 — checkpoint 1/4 завершен
Готово: ASP.NET Core Identity подключен к PostgreSQL; создана миграция InitialIdentity; роли Operator/Expert/Administrator и три детерминированные dev-учетки seeded.
Проверки: dotnet build — 0 warnings/0 errors; SQL join подтверждает ровно три учетные записи и корректную роль каждой; повторный seed идемпотентен.
Docker: API снова healthy на существующем volume; история миграций явно хранится в схеме `otklik`, данные volume не удалялись.
Посмотреть: инфраструктурный UI остается доступен на http://localhost:3000; кабинеты появятся в checkpoint 3.
Осталось: проверить cookie/CSRF endpoints и policies, сделать role UI, добавить интеграционные тесты и полный Docker smoke.
Риски/блокеры: нет. Во время первого прогона безопасно исправлено расхождение схемы EF history в уже существующем dev-volume.
```

```text
2026-09-08 16:58 +05:00 — Ticket 001 — checkpoint 4/4 завершен; Ticket 001 DONE; Ticket 002 начат
Готово: весь стек собирается и запускается одной Compose-командой; API слушает штатный порт без конфигурационного предупреждения; README-команды проверены.
Проверки: backend Docker test target — 1/1 passed; frontend Docker test target — ESLint passed, Vitest 1/1 passed, production build passed; прямой readiness=healthy; reverse-proxy status=healthy; browser smoke 320 px без horizontal scroll.
Docker: `docker compose up --build -d`; api, worker, frontend, postgres, redis и kafka имеют status healthy.
Посмотреть: http://localhost:3000; API readiness — http://localhost:8080/api/health/ready; все четыре UI-индикатора зеленые.
Осталось: продуктовые срезы тикетов 002–014.
Риски/блокеры: нет.

Ticket 001 DONE
Commit/состояние: коммит не создавался; рабочее дерево содержит новый воспроизводимый каркас.
Реализовано: solution и слои backend, React/Vite frontend, PostgreSQL, Redis, Kafka, worker, health-checks, Nginx reverse proxy, Dockerfiles/Compose, инструкции и smoke-тесты.
Автотесты: backend 1/1; frontend 1/1; lint и TypeScript/Vite build успешны в Docker.
Docker-команда: docker compose up --build -d
Health-checks: 6/6 контейнеров healthy; API, PostgreSQL, Redis, Kafka доступны через `/api/system/status`.
URL и тестовые данные: http://localhost:3000; тестовые учетные записи появятся в Ticket 002.
Ручной сценарий: открыть UI на 320 px, увидеть technical stand и четыре healthy-состояния, раскрыть/скрыть детали.
Известные ограничения: это инфраструктурный срез без продуктовых форм; ограничение снимают следующие тикеты.
Следующий тикет может начинаться: да — Ticket 002 переведен в IN_PROGRESS.
```

```text
2026-09-08 16:56 +05:00 — Ticket 001 — checkpoint 3/4 завершен
Готово: React/Vite frontend собран в Nginx-контейнере; same-origin `/api` proxy показывает статусы API, PostgreSQL, Redis и Kafka; исправлены переносы длинного заголовка.
Проверки: npm lint — passed; Vitest — 1/1 passed; Vite build — passed; browser smoke на 320 px — document width=320, horizontal scroll отсутствует; все четыре состояния зеленые.
Docker: frontend healthy и доступен на http://localhost:3000; запрос http://localhost:3000/api/system/status проходит через Nginx.
Посмотреть: http://localhost:3000 — страница «Отклик — технический стенд»; детали готовности раскрыты по умолчанию.
Осталось: container-only test stages, финальный полный Compose rebuild и проверка README-команд.
Риски/блокеры: нет.
```

```text
2026-09-08 16:51 +05:00 — Ticket 001 — checkpoint 2/4 завершен
Готово: ASP.NET Core API и background worker собраны и запущены в Docker; readiness включает PostgreSQL, Redis и Kafka.
Проверки: dotnet build — 0 warnings/0 errors; API /api/health/ready — healthy; worker dependency probe — succeeded; оба контейнера healthy.
Docker: api доступен на http://localhost:8080; worker работает в фоне. В runtime добавлен krb5-libs для чистых последующих логов.
Посмотреть: http://localhost:8080/api/system/status возвращает детальные состояния трех зависимостей.
Осталось: frontend-контейнер, визуальная mobile-проверка, container-only test stages и финальный полный compose rebuild.
Риски/блокеры: нет.
```

```text
2026-09-08 16:50 +05:00 — Ticket 001 — checkpoint 1/4 завершен
Готово: Compose поднял PostgreSQL 18.1, Redis 8.2.1 и Kafka 4.1.0 с постоянными volumes.
Проверки: docker inspect — health=healthy у всех трех сервисов; docker compose config — valid.
Docker: otklik-postgres-1, otklik-redis-1, otklik-kafka-1 работают.
Посмотреть: инфраструктурный checkpoint подтвержден через docker compose ps; браузерный UI появится после checkpoint 3.
Осталось: контейнерные API/worker, frontend и чистый общий запуск.
Риски/блокеры: нет.
```

```text
2026-09-08 16:47 +05:00 — Ticket 001 — checkpoint 1/4 проверка инфраструктуры
Готово: созданы solution, проекты API/Application/Domain/Infrastructure/Worker, React frontend, Dockerfiles, Compose, lock-файл и базовые тесты.
Проверки: dotnet test — 1/1 passed; frontend lint — passed; Vitest — 1/1 passed; Vite production build — passed; docker compose config — valid.
Docker: начинается загрузка и запуск PostgreSQL 18.1, Redis 8.2.1 и Kafka 4.1.0.
Посмотреть: после health-check инфраструктуры будет запущен UI на http://localhost:3000.
Осталось: подтвердить health инфраструктуры, собрать сервисы, проверить browser slice и чистый container-only test.
Риски/блокеры: первый pull образов может занять продолжительное время; это ожидаемая операция.
```

```text
2026-09-08 16:32 +05:00 — Ticket 001 — checkpoint 1/4 начат
Готово: прочитаны цель, общий prompt, progress, анализ и Ticket 001; проверено исходное состояние репозитория и Docker daemon.
Проверки: git status — исходного кода нет; Docker Server 29.6.1 доступен.
Docker: Compose-проект еще не создан.
Посмотреть: пока нет; ожидаемый первый срез — инфраструктурные health-checks.
Осталось: создать инфраструктуру, backend, frontend, тесты и выполнить чистый Docker-запуск.
Риски/блокеры: нет.
```

```text
YYYY-MM-DD HH:MM — Ticket NNN — checkpoint K/M
Готово:
Проверки:
Docker:
Посмотреть:
Осталось:
Риски/блокеры:
```

## Журнал решений

| Дата | Решение | Причина | Затронутые тикеты |
|---|---|---|---|
| 2026-09-08 | Оператор не видит внутренние заметки | Матрица прав имеет приоритет над ошибочной фразой раздела 4.4 | 005–007, 013 |
| 2026-09-08 | Внешние интеграции исключены | Граница MVP | все |
| 2026-09-08 | Минимальные кризисные контакты: 8-800-2000-122 и 124 | Ответ заказчика; значения остаются конфигурируемыми | 009, 010 |
| 2026-09-08 | Визуальная система: спокойная Material-палитра; без градиентов, status-пилюль, декоративных буллетов и полупрозрачного задвоения | Прямое уточнение заказчика по промежуточному стенду | 002–014; также исправлен срез 001 |
| 2026-09-08 | До дальнейшего расширения функций создать сквозной UI/UX-фундамент: полноценная кириллическая типографика, семантические Material-токены, единая публичная и служебная навигация; технический health вынести с лендинга | Прямая обратная связь заказчика: текущие шрифт, дизайн-система и навигация не соответствуют сервису поддержки | 001–014; ретрофит всех уже созданных экранов |
| 2026-09-09 | Основная и срочная операторские очереди используют атомарное закрепление карточки в Redis с TTL; версия PostgreSQL остается независимой защитой сохранения | При большом числе операторов общий список без lease предотвращал потерю данных, но не предотвращал одновременный разбор одного обращения | 005, 009, 015 post-acceptance |
| 2026-09-10 | Для непрерывной работы добавлена «Линия»: выбор точного приоритета без списка, одна lease-карточка и автоматический переход к следующей | Оператору нужен сфокусированный сценарий разбора наподобие последовательного квиза, не требующий ручного выбора каждой записи | 005, 015 post-acceptance |

## Финальная запись тикета

При переводе в `DONE` добавить:

```text
Ticket NNN DONE
Commit/состояние:
Реализовано:
Автотесты:
Docker-команда:
Health-checks:
URL и тестовые данные:
Ручной сценарий:
Известные ограничения:
Следующий тикет может начинаться: да/нет
```
