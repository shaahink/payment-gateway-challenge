# Decisions

Numbered to match [requirements.md](requirements.md). Each records the choice, why, and what was turned down.

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
