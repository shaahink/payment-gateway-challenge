# Requirements

Source: the [cko-recruitment brief](https://github.com/cko-recruitment/) and the interview scorecard.

## Functional

**F1. Process a payment.** `POST` a card payment and get one of three outcomes:

| Outcome | Meaning |
| --- | --- |
| `Authorized` | The bank authorised it |
| `Declined` | The bank declined it |
| `Rejected` | Validation failed. No payment is created and the bank is never called |

**F2. Validate the request.** All fields are required.

| Field | Rule |
| --- | --- |
| Card number | 14–19 characters, numeric only |
| Expiry month | 1–12 |
| Expiry year | Month + year in the future |
| Currency | 3 characters, one of at most 3 supported ISO codes |
| Amount | Integer, minor units (`1050` = 10.50) |
| CVV | 3–4 characters, numeric only |

**F3. Response for a processed payment:** id, status (`Authorized`/`Declined`), last four card digits, expiry month, expiry year, currency, amount. Never the full card number.

**F4. Retrieve a payment by id.** Same fields as F3. Status is only ever `Authorized` or `Declined`.

**F5. Call the bank simulator.** `POST http://localhost:8080/payments`, snake_case body, `expiry_date` as `MM/YYYY`.

| Card number ends in | Simulator returns |
| --- | --- |
| 1, 3, 5, 7, 9 | `200`, `authorized: true`, an `authorization_code` |
| 2, 4, 6, 8 | `200`, `authorized: false`, `authorization_code: ""` |
| 0 | `503` |
| Missing field | `400` |

## Non-functional

**Stated in the brief**
- N1. The code compiles.
- N2. Covered by automated tests (type and number are my choice).
- N3. Simple and maintainable, no over-engineering.
- N4. Design decisions and assumptions are documented.
- N5. In-memory storage is acceptable.
- N6. The full card number is never returned.

**Implied by the scorecard** (Observability, Packaging/Hosting, Technical Documentation, API Design, Code Design, Testing Mindset)
- N7. Logs are structured and never contain card data.
- N8. Runs in a container next to the simulator.

## Skeleton issues found

Baseline `08fafb4`: build passes with 4 warnings; tests run **1 passed, 1 failed**.

| # | Where | Issue | Resolved |
| --- | --- | --- | --- |
| B1 | `PaymentsController.cs:24` | Unknown id returns `Ok(null)`, which is `204`, not `404`. `Returns404IfPaymentNotFound` fails | ✅ AC8 |
| B2 | `PaymentsController.cs:20` | GET is typed as `PostPaymentResponse`; `GetPaymentResponse` exists and is unused | ✅ AC1 |
| B3 | `PaymentsController.cs:20` | `async` with nothing awaited | ✅ AC8 |
| B4 | `PaymentsController.cs` | Derives from `Controller` (MVC with views); an API wants `ControllerBase` | ✅ AC1 |
| B5 | `PostPaymentRequest.cs:5` | Request carries `CardNumberLastFour`, not the full card number | ✅ D1 |
| B6 | `PostPaymentRequest.cs:5,10`, both responses `:7` | Card fields are `int`: a 16–19 digit number overflows, and a CVV or last four loses leading zeros (`012`) | ✅ D1 (request), AC1 (responses) |
| B7 | `PaymentsRepository.cs:7` | Public mutable `List` field; registered as a singleton (`Program.cs:12`), so concurrent writes are unsafe | ✅ AC1 — private `ConcurrentDictionary` |
| B8 | `PaymentsRepository.cs:16`, `Currency` on all three models | Nullable warnings (CS8603, CS8618) | ✅ AC1 — API builds clean |
| B9 | — | No `POST` endpoint and no bank client | ✅ AC1 + bank client |
| B10 | `PaymentsControllerTests.cs:14–26` | `Random.Next(1, 12)` never returns 12; `Next(2023, 2030)` produces expired years | ✅ test reorganisation |
| B11 | `PaymentGateway.Api.Tests.csproj:11` | `Mvc.Testing` 6.0.24 on a net8.0 project | ✅ package update, 8.0.33 |
| B12 | Both `.csproj` files | Known-vulnerable packages (`dotnet list package --vulnerable --include-transitive`). **High**, test project, transitive: `System.Text.Json` 6.0.0, `Newtonsoft.Json` 9.0.1, `System.Net.Http` 4.3.0, `System.Text.RegularExpressions` 4.3.0. **Moderate**, API: `Swashbuckle.AspNetCore.SwaggerUI` 6.2.3 | ✅ package update: none left |

## Decisions

Settled ones are recorded in [decisions.md](decisions.md).

- D1. Status code for `Rejected`: `400` with the errors, and nothing stored (per F1 and F4).
- D2. ✅ Bank `503` → `503`, timeout → `504`, anything else → `502`. Nothing is stored.
- D3. ✅ A bank `400` is a `502`: our fault or the bank's, never the merchant's.
- D4. ✅ GBP, EUR, USD.
- D5. Idempotency and retries: out of scope for phase one, but say where they would go.
- D6. ✅ `POST` returns `201 Created` with a `Location` header, for Authorized and Declined alike.
- D7. ✅ Hand-written validation, no library.
- D8. ✅ No Luhn check.
- D9. ✅ The current expiry month is valid.
- D10. ✅ Lowercase currency is rejected.
- D11. ✅ Amount must be at least 1.
- D12. ✅ Built-in `ILogger` with message templates, no Serilog.
- D13. ✅ The bank call is not cancelled when the merchant disconnects; a timeout bounds it.
- D14. ✅ The service returns a `PaymentOutcome` rather than throwing for expected results.
- D15. ✅ Typed `HttpClient` for the bank, with its own wire records.
- D16. ✅ Unexpected errors are problem details outside Development, never a stack trace.
- D17. ✅ Packages updated within the lines the skeleton chose: net8, xUnit v2, Swashbuckle 6.
- D18. ✅ JSON console logs with scopes everywhere but Development, chosen by configuration.
- D19. ✅ Records that hold card data mask it in `ToString()`, as a second line of defence.
- D20. ✅ A multi-stage, non-root image, added as a service next to the simulator in the existing compose file.
- D21. ✅ No HTTPS redirection in the app: TLS ends at the load balancer.
- D22. ✅ `/health` is liveness only and never depends on the bank.
- D23. ✅ One outcome counter on `System.Diagnostics.Metrics`, exported later by OpenTelemetry.

## Acceptance criteria

Each criterion is proved by the test named against it. A criterion that depends on an open decision is settled when the decision is.

| AC | Given / when | Then | Traces to | Depends on | Test | Status |
| --- | --- | --- | --- | --- | --- | --- |
| AC1 | A valid request the bank authorises (simulator: card ending in an odd digit) | `201` with `Location`, status `Authorized`, id, last four, expiry month and year, currency, amount. The payment is stored | F1, F3 | D6 | `AuthorizesAPaymentTheBankApproves`, `BankSimulatorTests` | ✅ |
| AC2 | A valid request the bank declines (simulator: card ending in an even digit) | `201`, same shape, status `Declined`. The payment is stored | F1, F3 | D6 | `DeclinesAPaymentTheBankDeclines`, `BankSimulatorTests` | ✅ |
| AC3 | A request that breaks any F2 rule, or sends a field of the wrong type | `400` problem details naming every failing field in one response. The bank is not called and nothing is stored | F1, F2 | D1 | `RejectsAPaymentWithAnInvalidCardNumber`, `ReportsEveryInvalidFieldInOneResponse`, `RejectsAFieldOfTheWrongTypeWithTheSameProblemShape` | ✅ |
| AC4 | Each F2 rule at its boundaries | Accepted or rejected as the rule says (validator unit table) | F2 | D4, D8–D11 | `PaymentRequestValidatorTests` | ✅ |
| AC5 | The bank fails: `503` (simulator: card ending in `0`), a timeout, or anything unexpected | `503`, `504` or `502` as problem details saying whether a retry is safe. Nothing is stored | F5 | D2, D3 | `ReportsABankFailureAsProblemDetailsAndStoresNothing`, `BankClientTests`, `BankSimulatorTests` | ✅ |
| AC6 | Any `POST` or `GET` response | Never contains the full card number or the CVV | N6 | | `NeverReturnsTheCardNumberOrCvv` | ✅ |
| AC7 | `GET` with the id of a stored payment | `200`, the AC1 fields, last four only | F4 | | `RetrievesAPaymentSuccessfully`, `BankSimulatorTests` | ✅ |
| AC8 | `GET` with an unknown id | `404` | F4, B1 | | `Returns404IfPaymentNotFound` | ✅ |
| AC9 | Any processed payment | Logs carry the payment id and outcome, never card data | N7 | | `LoggingTests`, `CardDataFormattingTests` | ✅ |
| AC11 | Any payment request | The `payment_gateway.payments` counter goes up by one, tagged with its outcome: `Authorized`, `Declined`, `Rejected` or `Bank<failure>` | N7 | | `CountsEveryPaymentByOutcome` | ✅ |
| AC10 | `docker compose up` | The API and the simulator both start; `/health` returns `200` | N8 | | `ReportsHealthyEvenWhileTheBankIsDown`, `docker compose up` by hand | ✅ |

**Integration tests** run the gateway against the real simulator in Docker and prove the *(simulator: …)* clauses of AC1, AC2 and AC5. Every other test uses a fake bank or a stubbed `HttpMessageHandler`. They live in their own project, `PaymentGateway.Api.IntegrationTests`, tagged `Category=Integration`: start the simulator first (`docker compose up -d`), or skip them with `dotnet test --filter Category!=Integration`.

**Build order:** AC8 → AC4 → AC1 → AC2 → AC3 (complete) → AC5 → integration tests → AC7 → AC6 → package updates (B11, B12) → AC9 → AC11 → AC10 → README. Smallest change first; each decision is settled when its criterion comes up.
