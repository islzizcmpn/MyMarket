# Runbook: payment gateway onboarding

Taking a payment rail from "built and tested against the spec" to "taking real money". Every provider
is behind `IPaymentProvider` and independently feature-flagged, so they can be switched on one at a
time — and switched off again without a deploy.

Rails implemented: **Click**, **Payme**, **Uzcard** and **Humo** (both riding the Click rail), and
**Cash on delivery**. Configuration lives under `Payments:*` (see `src/PcMarket.Api/appsettings.json`
for the full shape); override per environment with `Payments__Click__SecretKey`-style variables.

## Before you start

- The API must be reachable over **public HTTPS**. Both gateways call back to it, and neither will
  accept a plain-HTTP or private address.
- Callback URLs to register with the provider:
  - Click: `https://<API_HOST>/api/v1/payments/click/callback`
  - Payme: `https://<API_HOST>/api/v1/payments/payme/callback`
- These endpoints are anonymous by design — a gateway cannot present a JWT. They authenticate by each
  provider's own scheme (Click's MD5 signature, Payme's Basic auth), which is why the keys below are as
  sensitive as any password.

## Sequence

1. **Get sandbox credentials** from the provider's merchant cabinet.
2. **Put them in `.env`** on staging. Never in `appsettings.json` — that file is in git.

   ```bash
   Payments__Click__ServiceId=...
   Payments__Click__MerchantId=...
   Payments__Click__SecretKey=...
   Payments__Payme__MerchantId=...
   Payments__Payme__MerchantKey=...
   ```

3. **Register the callback URL** in the merchant cabinet.
4. **Leave the rail disabled in production** (`Payments__Click__Enabled=false`) until step 7 passes.
5. **Run the flow on staging**: place an order, pay it, confirm it reaches `Paid` and that
   `PaymentTransactions` has exactly one settled row.
6. **Replay the callback** — send the same completion payload a second time and confirm the order stays
   `Paid` with still one settled ledger row. This is the single most important check; duplicate and
   out-of-order callbacks are normal in production, and `Phase4PaymentTests` covers exactly this, so a
   regression here should have been caught before you got this far.
7. **Test the failure paths**: a cancelled payment, a wrong-amount payload (must be rejected), and a
   callback for an order that no longer exists.
8. **Swap in production credentials** and set `Enabled=true`. Take one real payment of a small amount
   and refund it through the admin panel.

## Switching a rail off

```bash
# .env on the VPS
Payments__Payme__Enabled=false
docker compose up -d --no-deps api
```

Checkout stops offering it immediately. Orders already awaiting payment on that rail keep their
existing payment URL — the gateway does not know it has been disabled — so watch for stragglers before
retiring a rail for good.

## Where to look when a payment misbehaves

| Symptom | Where to look |
| --- | --- |
| Gateway reports a signature error | `Payments__<rail>__SecretKey` mismatch; keys differ between sandbox and production |
| Callback returns 404 | `PublicApiUrl`/registered callback URL wrong, or nginx not routing `API_HOST` |
| Order stays `AwaitingPayment` after a successful payment | Callback never arrived — check the provider's delivery log, then the raw payload ledger |
| Order paid twice | Should be impossible: the ledger has a unique index on `(Provider, ProviderTxnId)`. If it happened, capture both raw payloads before touching anything |
| Payment succeeded but the amount is wrong | Amount verification rejected it; Payme works in **tiyin**, Click in **so'm** — a factor-of-100 bug looks exactly like this |

Every gateway call is stored raw in `PaymentTransactions.RawPayload` (JSONB) precisely so a dispute can
be reconstructed. Card numbers are never stored — only the gateway's transaction reference.

## Adding a new rail

1. Implement `IPaymentProvider` in `src/PcMarket.Payments`.
2. Add its settings class and an `Enabled` flag, and register it in `PaymentJobsExtensions`/DI.
3. Add the wire enum value to `PcMarket.Contracts.Orders.PaymentMethod` **and** the matching
   `PcMarket.Domain.Enums.PaymentMethod` — they map by value, so they must stay aligned.
4. Add a callback endpoint in `PaymentEndpoints.cs` authenticating by the provider's own scheme.
5. Cover it with an integration test in the shape of `Phase4PaymentTests`, including the replay.
