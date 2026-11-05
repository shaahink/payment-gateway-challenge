# Decisions

Numbered to match [requirements.md](requirements.md). Each records the choice, why, and what was turned down.

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
