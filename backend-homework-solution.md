# Виконання ДЗ (Backend, .NET): груповий pre-order для Owner

Завдання — у файлі [homework-backend.md](./homework-backend.md).

## Рівень 1 — `GET /guest/groups/{groupReservationId}/sessions`

- Новий маршрут у `Routing/RouteRegistry.cs` + `Handlers/GuestSessions/GetGroupReservationSessionsHandler.cs`.
- `GuestSessionService.GetAllSessionsAsync`: перевірка Owner (`ValidateReservationOwnerAsync`, інакше 403), завантаження всіх сесій групи через GSI, batch-завантаження страв, формування `Participant` з підсумком по кожній сесії (ціна × кількість) та загальною сумою.
- Власник групи виключений зі списку учасників (`IsOwnerSession`); response — camelCase
  (`ResponseBuilder.CreateJsonResponse`).

## Рівень 2

- `GET /reservations/{reservationId}/guest-session-id` — `GetReservationGuestSessionIdHandler` +
  `GetOrCreateOwnerSessionIdAsync`: Owner-only; 404, якщо reservation або group reservation не знайдені; owner-сесія знаходиться або створюється (`IsOwnerSession` / `OwnerUserId`).
- `POST /group-reservations/{groupReservationId}/submit` — `SubmitGroupReservationHandler` +
  `GroupReservationService.SubmitGroupReservationAsync`: лише Owner; guard-и статусу (already Submitted → 400, Closed → expired); беруться тільки `IsSubmitted` guest-сесії; 400 — немає submitted-сесій / немає страв / не знайдено order; страви об'єднуються за `dishId` (кількості сумуються) і додаються в order; статус order → `SubmittedByCustomer`, групи → `Submitted`; повертається об'єднаний список `[{dishId, quantity}]`.

## Рівень 3 — `DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`

- Логіку снапшота винесено в спільний `BuildGroupSessionsSnapshotAsync` (використовують і GET, і DELETE): оновлений список учасників без видаленої сесії та перерахована сума групи.
  Перевірка Owner, 404 (сесію не знайдено) і 400 (сесія не належить групі) були реалізовані.

## Інфраструктура

- `deployment_resources.json`: виправлено шлях ресурсу API Gateway
  `/guest/group/...` → `/guest/groups/...` (без цього новий маршрут не відповідав би) додано ресурси для двох нових ендпоінтів з `cognito_authorizer`.

## Тести

- Додано 17 тестів (xUnit + Moq + AutoFixture) у `Tests/Services`: підсумки 360/220/580,
  виключення Owner, не-Owner → заборона, не знайдено, порожня група, guard-и статусів,
  об'єднання страв в order.
- `dotnet test`: 158 тестів пройдено, 0 помилок.

## Деплой

Задеплоєно в мій AWS-акаунт (`eu-north-1`) через AWS Syndicate (`syndicate build &&
syndicate deploy`). Оновлено функцію `api-handler`; маршрути з'явились в API Gateway
(`rghcb6xua0`, stage `develop`), доступ захищено авторизатором Cognito.

Перевірка після деплою:

- `CodeSize` функції змінився порівняно з попереднім деплоєм (новий код справді завантажено);
- обидва нові маршрути + виправлений `/guest/groups/{groupReservationId}/sessions`
  присутні у `aws apigateway get-resources`;
- виклик ендпоінтів без токена повертає `401 Unauthorized` (авторизатор активний).

## Використання AI

Рішення підготовлено за допомогою AI-асистента (OpenCode): постановку задач, архітектурні
рішення та фінальний огляд виконано мною, але з допомогою AI; код згенеровано AI та перевірено локально
(`dotnet build`, `dotnet test`).
