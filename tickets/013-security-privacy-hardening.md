# Ticket 013 — Усиление безопасности и приватности

Зависит от Ticket 012. Перед выполнением прочитать `tickets/README.md` и `tickets/progress.md`.

## Цель

Проверить и усилить все границы доступа, трек-номер, логи, файлы и frontend перед финальной приемкой.

## Checkpoints

1. Автоматическая матрица authorization и object-level access.
2. Защита трек-номера, rate-limit и uniform responses.
3. HTTP/browser hardening, безопасные логи и секреты.
4. Негативный end-to-end прогон в Docker.

## План

- Создать integration test suite по каждой клетке матрицы ролей, включая запрет оператору notes/chat и запрет администратору любого content.
- Проверить object-level authorization для каждого ticket/file/message id, а не только роль.
- Реализовать лимит неуспешного ввода трек-номера в Redis: пять попыток в минуту с краткоживущим keyed digest адреса, затем нарастающая задержка. Digest не связывать с тикетом и автоматически удалять.
- Сравнять status code, размер и форму ответа для неверного/несуществующего номера; добавить глобальный защитный лимит.
- Исключить трек-номер из URL, logs, traces, metrics и error reports.
- Проверить CSRF, CORS same-origin, CSP, HSTS production, security headers, cookie flags и отсутствие сторонних trackers/resources.
- Централизованно маскировать request bodies, contacts, messages, notes и filenames в логах.
- Проверить upload на path traversal, MIME spoofing, oversized body и unauthorized download.
- Добавить optimistic concurrency для критических переходов и idempotency для submit/message/status commands.
- Запустить dependency/license scan; оставить только open-source и свободно используемые компоненты, результаты сохранить в репозитории.
- Проверить, что dev credentials и VAPID/local secrets не попали в production image или git history текущего проекта.

## Проверки

- Все отрицательные authorization tests.
- C5: подбор неверных номеров ограничен.
- Оператор не может читать notes/chat даже прямым API-запросом.
- Администратор не может получить content/contact/file.
- Эксперт не может открыть неназначенный тикет.
- Поиск известных секретов/текста по container logs ничего не находит.
- Security headers и cookie settings соответствуют Development/Production профилям.

## Docker checkpoint

Поднять стенд, выполнить автоматический negative suite и вручную показать минимум четыре запрещенных запроса с ожидаемыми 401/403/404, а также срабатывание rate-limit. Команды и результаты записать в `progress.md`.

## Готово, когда

Все границы доступа подтверждены отрицательными тестами, публичный контур устойчив к перебору и утечке секрета, логи безопасны, а compose-стенд остается рабочим.

