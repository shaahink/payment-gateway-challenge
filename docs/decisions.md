# Decisions

Numbered to match [requirements.md](requirements.md). Each records the choice, why, and what was turned down.

## D1. A rejection is a `400` validation problem, and nothing is stored

- **Why:** the brief says a rejected payment is one where no payment could be created, and `GET` only ever returns `Authorized` or `Declined`, so a rejection has no id and no record. A `400` with problem details is the standard way to say "fix the request".
- **How:** the title is "The payment was rejected.", so the brief's word is in the body. Every failing field comes back in one response, keyed by the name the merchant sent (`cardNumber`, not `CardNumber`). A field of the wrong type (`"amount": "abc"`) is rejected by model binding before the validator runs, and comes back in the same shape, keyed by its JSON path (`$.amount`) with a generic message, so no .NET type names reach the merchant. A missing or `null` body is rejected the same way.
- **Rejected:** `201` or `200` with `status: "Rejected"`, which would create the record the brief says must not exist. `422`, which is defensible but less widely expected than `400` for validation.
- **Consequence:** a merchant tells a rejection from a bank decision by the status code. The casing is converted at the HTTP boundary (the controller), so the validator keeps using C# property names.

## D2. A bank failure is three failures, and none is stored

| The bank | We know | The merchant gets |
| --- | --- | --- |
| Returns `503`, or cannot be reached | It did not process the payment | `503`: safe to retry |
| Does not answer within the timeout | Nothing: it may have authorised | `504`: outcome unknown |
| Returns anything else (`400`, `500`, an unreadable body) | Something is wrong on one side | `502` |

- **Why:** retry safety is what the merchant needs to know, and it differs between the three. Each response is problem details saying which case it is.
- **Rejected:** one `502` for everything, which hides the one case where a retry could charge the shopper twice.
- **Consequence:** nothing is stored, because `GET` only knows `Authorized` and `Declined`. In production a timeout would be stored as `Pending` and reconciled with the bank, and an idempotency key (D5) would make retries safe. A connection dropped *after* the request was sent is really "unknown" too; .NET 8's `HttpRequestException.HttpRequestError` could separate it from "refused", and here both count as unavailable.

## D3. A bank `400` is our fault, not the merchant's

- **Why:** validation runs first, so a `400` from the bank means our mapping is wrong or the bank changed. Passing it on as a `400` would tell the merchant to fix a request that was fine.
- **Consequence:** it is a `502`, logged at Warning with the payment id.

## D4. Supported currencies: GBP, EUR, USD

- **Why:** the brief caps it at three ISO codes. These are the obvious three for a London gateway, and GBP is the brief's own example.
- **Rejected:** a zero-decimal currency such as JPY. It would make the minor-units rule more interesting without changing any requirement.
- **Consequence:** any other code, including a valid ISO one, is rejected.

## D5. Idempotency and retries are out of scope, but designed for

- **Why:** a payment is not idempotent. Retrying one the bank may have authorised can charge the shopper twice, so the gateway retries nothing itself, and its errors tell the merchant when a retry is safe (D2).
- **Where it would go:** an `Idempotency-Key` header on `POST`, stored with the payment under a unique constraint. A repeat with the same key returns the original result instead of calling the bank again. The payment id is already generated before the bank call, which is where the key would be bound to it.
- **Consequence:** until then, a merchant retry after a `504` is the one unsafe case, and the README says so.

## D6. `POST` returns `201 Created` with a `Location` header

- **Why:** a payment resource is created whenever the bank answers, and the merchant needs its id to retrieve it. `Location` points at the `GET`.
- **Rejected:** `200 OK`, which is common but says nothing about a new resource.
- **Consequence:** `Declined` is a `201` too. The HTTP status says the request was processed; the payment status says what the bank decided. A `Rejected` request creates nothing, so it stays a `400` (D1).

## D7. Hand-written validation, no library

- **Why:** six fields, one cross-field rule and a clock. A dependency would cost more than it saves, and dependencies need a reason in a PCI-scoped estate.
- **Rejected:** FluentValidation, which suits a larger rule set or a team that already uses it. DataAnnotations, which handle single-field rules but not the expiry rule with an injected clock.
- **Consequence:** the validator is a plain class returning a list of errors, so swapping to FluentValidation later is mechanical.

## D8. No Luhn check

- **Why:** the brief's rules don't include one, and the simulator decides its answer by the last digit, so a Luhn rule could reject its test cards.
- **Consequence:** a mistyped card number reaches the bank. In production it would be a cheap first check.

## D9. A card expiring this month is valid

- **Why:** cards are valid until the end of their expiry month, so "in the future" is read as month-and-year not before the current one.
- **Rejected:** the literal reading, which would turn away cards the bank accepts.
- **Consequence:** expiry is compared as (year, month) against an injected `TimeProvider`, in UTC. Comparing numbers rather than building a date also means an absurd year cannot throw.

## D10. Lowercase currency is rejected

- **Why:** ISO 4217 codes are uppercase. Normalising would hide a merchant integration bug, and the stored value would differ from the one sent.

## D11. Amount must be at least 1

- **Why:** a zero-value payment is not a payment.
- **Rejected:** accepting `0`. Zero-amount authorisation (card verification) is a real flow, but a different one, and out of scope.

## D12. Built-in logging with message templates

- **Why:** `ILogger` message templates are already structured: each placeholder becomes a named field. One line per outcome, carrying the payment id and status.
- **Rejected:** Serilog. It earns its place with sinks and enrichers, and none are in scope.
- **Consequence:** log lines name fields, never values from the card. A rejection logs which fields failed, not what was sent.

## D13. The bank call ignores the merchant's cancellation

- **Why:** if the merchant disconnects mid-call, cancelling could abandon a request the bank has already authorised, leaving money held with no record on our side.
- **Rejected:** passing the request's `CancellationToken` through, which is the usual .NET default.
- **Consequence:** the call runs to completion, bounded by the `HttpClient` timeout (`Bank:TimeoutSeconds`).

## D14. The service returns an outcome, not exceptions

- **Why:** the brief defines the outcomes, and a rejection or a decline is expected, not exceptional. `PaymentOutcome` makes them a type the controller has to handle.
- **Rejected:** throwing a validation exception and mapping it in middleware, which hides the flow. A general-purpose `Result<T>` library, which is more than three cases need.
- **Consequence:** the controller is a switch from outcome to HTTP status. A new outcome (bank unavailable) is a new case.

## D15. The bank client is a typed `HttpClient` with its own wire records

- **Why:** the bank's contract (snake_case, `expiry_date` as `MM/YYYY`) stays inside `Bank/`, and `JsonNamingPolicy.SnakeCaseLower` maps it without attributes. `IHttpClientFactory` manages connection lifetimes.
- **Consequence:** a change on the bank's side touches two internal records. Tests stub `HttpMessageHandler`, the seam `HttpClient` is built around, so no mocking library is needed. The simulator's empty `authorization_code` on a decline becomes `null` in the domain.

## D16. Unexpected errors are problem details, never a stack trace

- **Why:** outside Development, an unhandled exception must not leak internals, and every error the merchant sees should have the same shape.
- **Consequence:** `AddProblemDetails()` plus `UseExceptionHandler()` outside Development. Every error response, expected or not, is `application/problem+json` with a `traceId` that matches the logs.

## D17. Packages updated within the lines the skeleton chose

The skeleton pinned 2021–2022 versions, five of them with published advisories (B12). Updated once a test suite existed that could catch a regression, as a commit of its own:

| Package | From | To | Why this version |
| --- | --- | --- | --- |
| `Microsoft.AspNetCore.Mvc.Testing` | 6.0.24 | 8.0.33 | Latest for net8 (B11). 10.x needs net10 |
| `Microsoft.NET.Test.Sdk` | 17.1.0 | 18.11.0 | Supports net8; drops the old `Newtonsoft.Json`, `System.Net.Http` and `System.Text.RegularExpressions` chain |
| `xunit` | 2.4.1 | 2.9.3 | Last of v2. Its nullability annotations clear the CS8602 warnings |
| `xunit.runner.visualstudio` | 2.4.3 | 3.1.5 | The adapter, not the framework; 3.x runs v2 tests |
| `coverlet.collector` | 3.1.2 | 10.1.0 | Verified with `--collect:"XPlat Code Coverage"` |
| `Swashbuckle.AspNetCore` | 6.2.3 | 6.9.0 | Clears the SwaggerUI advisory without the breaking OpenAPI changes of 7+ |

- **Why:** fix every known vulnerability without changing what the brief gave: the net8 target, xUnit v2 and the `.editorconfig`.
- **Rejected:** moving to net10. **.NET 8 support ends on 10 November 2026**, so it is the next step, but a framework move is a deliberate change of its own, not part of a take-home.
- **Consequence:** `dotnet list package --vulnerable --include-transitive` reports nothing in any project, the build has no warnings, and all 63 tests pass unchanged. Line coverage of the in-process tests is 97.9%.

## D18. JSON logs with scopes, chosen by configuration

- **Why:** a log platform needs fields, not sentences. The JSON console formatter writes each template placeholder (`PaymentId`, `Status`) as its own field, and with scopes on, every line carries the request's `TraceId`. One id then joins the merchant's request, the bank call with its latency and status, and the outcome.
- **How:** `Logging:Console:FormatterName` is `json` in `appsettings.json` and `simple` in `appsettings.Development.json`, where people read the console. No code, no package.
- **Rejected:** Serilog (D12), and a hand-written correlation-id middleware, which ASP.NET's `TraceId` scope already makes unnecessary.

## D19. Records that hold card data mask it in `ToString()`

- **Why:** a record's compiler-generated `ToString()` prints every property. `BankPaymentRequest` and `BankWireRequest` carry the full card number and CVV, so one `_logger.LogInformation("{Request}", request)` would put a PAN in the logs.
- **Consequence:** both print `CardNumber = ****1111, … Cvv = ***`. The logging tests send a known card number and CVV down every path and assert neither reaches any log entry. A mutation check confirmed the layering: with a careless request log added, the tests fail before the masking and pass after it.

## D20. A multi-stage, non-root image beside the simulator

- **Why:** the SDK builds, the ASP.NET runtime image runs (221 MB), and the process runs as the image's built-in non-root `app` user. Restoring from the `.csproj` alone first means a code change does not re-download packages.
- **How:** `docker compose up -d --build` starts the simulator and the gateway on `http://localhost:5000`. The gateway reaches the bank by its service name (`Bank__BaseUrl=http://bank_simulator:8080/`), and it runs as Production, so it logs JSON and hides exception details.
- **Rejected:** a separate compose file. The brief's file is where a reviewer looks, and their `bank_simulator` service is left exactly as it was, including the obsolete `version` key.
- **Consequence:** no `HEALTHCHECK` in the image, because the runtime image has no `curl`. The orchestrator (an ECS target group, a Kubernetes probe) checks `/health` over HTTP instead.

## D21. No HTTPS redirection inside the app

- **Why:** in a container, TLS ends at the load balancer and the app listens on plain HTTP behind it. `UseHttpsRedirection` had no HTTPS port to redirect to and logged a warning on every request.
- **Consequence:** HTTPS and HSTS belong to the edge. Merchant authentication (API keys or mutual TLS) belongs there too, and is out of scope here.

## D22. `/health` is liveness, never the bank's health

- **Why:** if the health check followed the bank, a bank outage would make the orchestrator pull every gateway instance, taking down `GET` traffic that still works and the 503s that tell merchants to retry.
- **Consequence:** `/health` says the process is up. The bank's health shows in the logs and the `payments.processed` counter instead. A test proves `/health` stays `200` while the bank is failing.

## D23. One counter, tagged by outcome

- **Why:** logs say what happened to one payment; a metric says what is happening to all of them. `payment_gateway.payments`, tagged `outcome`, gives the authorisation rate (`Authorized` against `Declined`), the rejection rate (merchant integration problems) and the bank failure rate, the three numbers an on-call engineer would alert on.
- **How:** `System.Diagnostics.Metrics` through `IMeterFactory`, built into .NET 8, no package. Readable locally with `dotnet-counters monitor --counters PaymentGateway`.
- **Rejected:** an OpenTelemetry exporter and a `/metrics` endpoint. They are the production step (D12's reasoning: no infrastructure the brief does not ask for), and the counter needs no change to be exported.
- **Consequence:** the test listens only to meters from its own host's `IMeterFactory`, so parallel tests cannot pollute the count.

