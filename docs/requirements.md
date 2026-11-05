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

| # | Where | Issue |
| --- | --- | --- |
| B1 | `PaymentsController.cs:24` | Unknown id returns `Ok(null)`, which is `204`, not `404`. `Returns404IfPaymentNotFound` fails |
| B2 | `PaymentsController.cs:20` | GET is typed as `PostPaymentResponse`; `GetPaymentResponse` exists and is unused |
| B3 | `PaymentsController.cs:20` | `async` with nothing awaited |
| B4 | `PaymentsController.cs` | Derives from `Controller` (MVC with views); an API wants `ControllerBase` |
| B5 | `PostPaymentRequest.cs:5` | Request carries `CardNumberLastFour`, not the full card number |
| B6 | `PostPaymentRequest.cs:5,10`, both responses `:7` | Card fields are `int`: a 16–19 digit number overflows, and a CVV or last four loses leading zeros (`012`) |
| B7 | `PaymentsRepository.cs:7` | Public mutable `List` field; registered as a singleton (`Program.cs:12`), so concurrent writes are unsafe |
| B8 | `PaymentsRepository.cs:16`, `Currency` on all three models | Nullable warnings (CS8603, CS8618) |
| B9 | — | No `POST` endpoint and no bank client |
| B10 | `PaymentsControllerTests.cs:14–26` | `Random.Next(1, 12)` never returns 12; `Next(2023, 2030)` produces expired years |
| B11 | `PaymentGateway.Api.Tests.csproj:11` | `Mvc.Testing` 6.0.24 on a net8.0 project |
| B12 | Both `.csproj` files | Known-vulnerable packages (`dotnet list package --vulnerable --include-transitive`). **High**, test project, transitive: `System.Text.Json` 6.0.0, `Newtonsoft.Json` 9.0.1, `System.Net.Http` 4.3.0, `System.Text.RegularExpressions` 4.3.0. **Moderate**, API: `Swashbuckle.AspNetCore.SwaggerUI` 6.2.3 |

## Open decisions

- D1. Status code for `Rejected`: `400` with the errors, and nothing stored (per F1 and F4).
- D2. Bank `503`: what the merchant receives, and whether anything is stored.
- D3. Bank `400`: our validation should make it impossible, so treat it as our fault, not the merchant's.
- D4. The three supported currencies.
- D5. Idempotency and retries: out of scope for phase one, but say where they would go.

## Acceptance criteria

_To be extracted from the above._
