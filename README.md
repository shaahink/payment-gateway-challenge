# Payment Gateway

A merchant-facing payment gateway for the [Checkout.com engineering assessment](https://github.com/cko-recruitment/). A merchant posts a card payment; the gateway validates it, sends it to the acquiring bank (the provided simulator), stores the result, and lets the merchant retrieve it. Full card numbers and CVVs are never stored, returned or logged.

.NET 8 · ASP.NET Core · xUnit · Docker. No packages beyond the skeleton's own.

## Run it

```bash
docker compose up -d --build        # the bank simulator on :8080 and the gateway on :5000
```

```bash
curl -i -X POST http://localhost:5000/api/payments -H "Content-Type: application/json" \
  -d '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2027,"currency":"GBP","amount":100,"cvv":"123"}'

curl http://localhost:5000/api/payments/{id}     # the Location header from the POST
curl http://localhost:5000/health
```

The simulator decides by the card's last digit: odd authorises, even declines, `0` returns `503`.

To run the gateway from an IDE instead, start only the simulator (`docker compose up -d bank_simulator`) and run `src/PaymentGateway.Api`. Swagger is at `/swagger` in Development.

## Test it

```bash
docker compose up -d bank_simulator
dotnet test                                      # all 71 tests
dotnet test --filter Category!=Integration       # the 68 that need no Docker
```

| Layer | Tests | What it proves |
| --- | --- | --- |
| Unit | 50 | Every validation rule at its boundaries; the bank client's wire format and its failure mapping, against a stubbed `HttpMessageHandler`; card data masked in `ToString()` |
| Endpoint | 18 | The HTTP contract in-process (`WebApplicationFactory`) with a fake bank: status codes, problem details, `Location`, no card data in responses or logs, the outcome counter, `/health` |
| Integration | 3 | The real gateway against the real simulator, with no test doubles: odd, even and `0` cards |

Built test-first against the acceptance criteria in [docs/requirements.md](docs/requirements.md). Each criterion names the test that proves it. Tests that went green on first run (because an earlier step had already built the behaviour) were checked by mutation: the code was broken on purpose to confirm the test would catch it. Line coverage of the in-process tests is 98.1%.

## The API

`POST /api/payments`

```json
{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }
```

| Outcome | Status | Body |
| --- | --- | --- |
| Authorized | `201 Created` + `Location` | The payment: `id`, `status`, `cardNumberLastFour`, expiry, `currency`, `amount` |
| Declined | `201 Created` + `Location` | The same, with `status: "Declined"` |
| Rejected | `400 Bad Request` | Validation problem details naming every failing field. The bank is not called and nothing is stored |
| Bank unavailable | `503 Service Unavailable` | Problem details: not processed, safe to retry |
| Bank timed out | `504 Gateway Timeout` | Problem details: outcome unknown |
| Bank error | `502 Bad Gateway` | Problem details |

`GET /api/payments/{id}` returns `200` with the stored payment, or `404`.

**Validation:** card number 14–19 digits · expiry month 1–12 · expiry month and year not in the past (a card is valid until its expiry month ends) · currency `GBP`, `EUR` or `USD` · amount a positive integer in minor units (`1050` is 10.50) · CVV 3–4 digits.

## Design

```
Controllers/   PaymentsController     HTTP in and out, nothing else
Models/        Requests, Responses    the merchant's contract
Services/      PaymentService         validate → bank → store → log → count
               PaymentRequestValidator, PaymentMetrics
Domain/        Payment, PaymentStatus, PaymentOutcome, BankFailure, ValidationError
Bank/          IBankClient, BankClient, wire records   the acquiring bank's contract
Storage/       PaymentsRepository     in memory
```

`PaymentService` returns a `PaymentOutcome` (`Processed`, `Rejected` or `BankFailed`) and the controller maps it to a status code, so every outcome the brief defines is a type the code must handle. The bank's contract (snake_case, `expiry_date` as `MM/YYYY`) stays inside `Bank/`. The stored `Payment` holds the last four digits only.

The decisions behind each choice, and the alternatives turned down, are in [docs/decisions.md](docs/decisions.md). The ones most worth reading:

- **A bank failure is three failures** (D2). A `503` means the bank did not process the payment, so a retry is safe. A timeout means the outcome is unknown, and a retry could charge the shopper twice. The merchant is told which.
- **The bank call ignores the merchant's disconnect** (D13). Cancelling it could abandon a payment the bank has already authorised. The `HttpClient` timeout bounds it instead.
- **`201 Created` for Declined as well as Authorized** (D6). A payment resource exists either way; `status` carries the bank's decision.
- **No card data anywhere** (D19, AC6, AC9). Records holding the PAN and CVV mask them in `ToString()`, and tests send a known card number down every path and assert it reaches no response and no log.
- **Built-in over packages** (D7, D12, D23). Hand-written validation, `ILogger`, `System.Diagnostics.Metrics`. Each has a named point at which a library would earn its place.

## Observability

- **Logs:** JSON outside Development, one line per outcome with `PaymentId` and `Status` as fields. Every line carries the request's `TraceId`, so one id joins the merchant's request, the bank call (its status and latency) and the outcome. Bank failures log at Warning.
- **Metrics:** `payment_gateway.payments`, tagged by `outcome`: the authorisation, rejection and bank-failure rates. `dotnet-counters monitor --counters PaymentGateway` shows it live.
- **Health:** `/health` is liveness only. It does not follow the bank, so a bank outage cannot get every gateway instance pulled out of service.
- **Errors:** every error response is `application/problem+json` with a `traceId` matching the logs, and outside Development none carries a stack trace.

## Assumptions

- Three currencies are enough (the brief asks for no more than three). Lowercase codes are rejected, not normalised.
- No Luhn check: the brief does not ask for one, and the simulator's test cards are chosen by last digit.
- An amount of `0` is rejected. Zero-value card verification is a separate flow.
- In-memory storage, as the brief allows. Data is lost on restart.
- No merchant authentication. In production it sits at the edge, with TLS.

## What I would do next

1. **Idempotency keys.** An `Idempotency-Key` header stored with the payment, so a retry after a timeout returns the original result instead of charging twice.
2. **Pending payments and reconciliation.** Store a timed-out payment as `Pending` and resolve it against the bank, rather than storing nothing.
3. **Tokenisation and PCI scope.** Swap the PAN for a token as soon as it arrives, so only a small vault service is in PCI scope.
4. **A real store** behind the repository, with a unique constraint on the idempotency key.
5. **OpenTelemetry** to export the existing traces and the counter, with alerts on bank error rate and p99 latency against an SLO.
6. **.NET 10.** .NET 8 support ends on 10 November 2026. Kept here because the brief targets net8.

## Repository

The skeleton's issues found at the start (a GET that returned `204` for an unknown id, `int` card fields that overflow and lose leading zeros, a public mutable list, vulnerable packages) are listed in [docs/requirements.md](docs/requirements.md) with what resolved each. The commit history follows the build order: each acceptance criterion went red, then green, then got committed.
