# Issue: Telegram bot — clicking a product does nothing, cart stays empty

| Field | Value |
| --- | --- |
| **Issue Title** | Clicking a product in the Telegram bot never adds it to the cart |
| **Description** (verbatim) | " When I click on the product  it does not apper in my cart  " |
| **Created** | 2026-07-29 |
| **Last Updated** | 2026-07-29 15:35 UTC |
| **Current Phase** | **Validated** |
| **Status Summary** | **Resolved.** `Telegram:StorefrontUrl` was `http://localhost:8080`; Telegram rejects `localhost` in inline-keyboard URL buttons with `400 Wrong HTTP URL` and discards the *entire* message, so the product card — and with it the only "Add to cart" button — never rendered. The cart was empty because nothing was ever added; the cart itself was never at fault. Fixed with Option C + D: URL buttons are now dropped unless publicly reachable (`PublicUrl`), a failed send retries without its keyboard rather than vanishing, and a `tunnel-web` service gives the storefront a real public URL in dev. Verified end-to-end: a simulated product tap → add-to-cart → cart now produces zero send failures and a persisted cart row. |

## Investigation Log

**2026-07-29 15:08 UTC — Investigation started.**
Invoked as `fix-it-v1 "When I click on the product it does not apper in my cart"`, with two
screenshots of the live chat with @Attach_Incbot.

**Restatement of symptom:** In the Telegram bot, the user reached a category listing
("Комплектующие · 3 product(s) · page 1") showing three product buttons. Tapping a product button
produced no visible response — the message did not change and no new message arrived. Tapping
🛒 Cart then showed "Your cart is empty."

**Context confirmed without needing to ask:** this is the local Docker stack configured earlier in
this session; the bot went live at 14:56 UTC and the screenshots are from 15:01–15:03 UTC, so the
API container logs for the exact interaction were still available.

**15:09 UTC — Read the bot flow code** (`TelegramUpdateHandler`, `CatalogFlow`, `BotKeyboards`,
`BotResponder`) and pulled the API container logs for the interaction window.

## Evidence

**E1 — Product-detail sends failed with a 400, repeatedly** (`docker compose logs api`):

```
[15:01:19 WRN] Telegram send to chat 576388865 failed.
Telegram Bot API error 400: Bad Request: inline keyboard button URL
'http://localhost:8080/product/logitech-m330-silent' is invalid: Wrong HTTP URL
   at PcMarket.Bot.Handlers.BotResponder.SendAsync(...) BotResponder.cs:line 65
```

Same error at 15:01:21 (logitech), 15:02:17 (kingston-fury-16gb-ddr4), 15:02:21
(asus-vivobook-15), 15:03:30, 15:03:53 — one per product tap, matching the screenshots exactly.
Every product the user tried failed.

**E2 — The offending button is built unconditionally when the URL is non-empty.**
[BotKeyboards.cs:125-128](../../../src/PcMarket.Bot/Presentation/BotKeyboards.cs#L125-L128):

```csharp
if (!string.IsNullOrWhiteSpace(storefrontUrl))
{
    rows.Add([InlineKeyboardButton.WithUrl("🌐 Open in store", $"{storefrontUrl.TrimEnd('/')}/product/{product.Slug}")]);
}
```

The guard checks only that the string is non-empty, never that it is a URL Telegram will accept.

**E3 — Config supplying it.** `Telegram__StorefrontUrl=http://localhost:8080`, sourced from
`PUBLIC_STOREFRONT_URL` in `.env` via `docker-compose.yml`. Confirmed inside the running container
with `docker compose exec api printenv`.

**E4 — Why the failure is *silent* rather than an error message.**
[BotResponder.cs:29-48](../../../src/PcMarket.Bot/Handlers/BotResponder.cs#L29-L48): for a callback,
`ReplyAsync` first tries `EditMessageText`; that is rejected with the same 400 and swallowed at
`LogDebug` as an expected "edit failed" case. It then falls through to `SendAsync`, which is
rejected identically and swallowed at `LogWarning` (line 72-75). Both paths carry the same invalid
keyboard, so both fail, and the user sees nothing at all. `AcknowledgeAsync` still succeeds, so the
button stops spinning and the tap looks like it simply did nothing.

**E5 — The cart itself is exonerated.** `CartFlow.AddAsync` is only ever reached via the
`BotCommands.AddToCart` callback ([TelegramUpdateHandler.cs:135-136](../../../src/PcMarket.Bot/Handlers/TelegramUpdateHandler.cs#L135-L136)),
and that callback is only attached to buttons on the product-detail keyboard — the message that
never rendered. There is no log line showing an add-to-cart callback arriving. The cart is empty
because nothing was ever added, not because adding failed.

## Hypotheses

| # | Hypothesis | Likelihood | Evidence |
| --- | --- | --- | --- |
| H1 | Product-detail message fails to send because `StorefrontUrl` is a `localhost` URL Telegram rejects | **Confirmed** | E1, E2, E3, E4 — direct error text naming the exact URL, once per tap |
| H2 | Cart add succeeds but cart read uses a different key (guest cart vs linked account) | Eliminated | E5 — the add-to-cart callback never fires; no such log line exists |
| H3 | User misunderstanding — tapping a product is *meant* to open detail, not add to cart | Contributing but not the bug | Correct as a description of intended UX, but E1 proves the detail view never rendered either |
| H4 | Callback payload exceeded Telegram's 64-byte limit | Eliminated | `CallbackData.Of` enforces this at build time; the 400 names a URL, not a payload |
| H5 | All variants out of stock, so no "Add to cart" button was built | Eliminated | Would still have rendered the message; instead the send itself was rejected |

## Approved Actions

- 2026-07-29 15:09 UTC — Read-only code review and `docker compose logs api`. Non-invasive, no
  state change; performed without a separate approval gate.

## Pending Actions

- Await the user's choice of fix option below before making any change.

## Fix Options

### Option A — Harden the keyboard (recommended, code)

Only attach the "Open in store" button when the configured URL is one Telegram will actually
accept: absolute, `http`/`https`, and not a loopback/private host.

- **Steps:** add a validity check in `BotKeyboards.Product`, replacing the bare
  `IsNullOrWhiteSpace` guard; skip the button when it fails.
- **Impact:** in dev the button silently disappears; everything else in the product view works.
  In production with a real domain, nothing changes.
- **Risk:** Low. Strictly removes a button that currently breaks the whole message.
- **Why it matters beyond this bug:** today *any* misconfigured `StorefrontUrl` takes down the
  entire product-detail view for every user, with only a `LogWarning` to show for it. The same
  latent trap exists at [BotKeyboards.cs:217](../../../src/PcMarket.Bot/Presentation/BotKeyboards.cs#L217)
  for the "Pay now" URL button, which would break order detail the same way if a gateway ever
  returned a non-public URL.
- **Verification:** tap a product in the bot; the detail card renders with an Add-to-cart button.

### Option B — Give the storefront a public HTTPS URL (config only)

Point `PUBLIC_STOREFRONT_URL` at a second Cloudflare quick tunnel, so the button is valid.

- **Steps:** add a `tunnel-web` service, set `PUBLIC_STOREFRONT_URL`, recreate `api`.
- **Impact:** the button appears and works in dev.
- **Risk:** Low, but the URL is ephemeral — it breaks again on every tunnel restart, and does
  nothing to stop a future bad value from silently killing the product view.
- **Verification:** same as A.

### Option C — Both (recommended overall)

A for correctness and resilience, B so the button is actually exercised in dev.

### Option D — Surface send failures instead of swallowing them

Independent of the above: `BotResponder.SendAsync` catches every exception and logs a warning, which
is exactly why this bug presented as "nothing happens". Consider replying with a generic error to the
chat when the send fails, so a broken keyboard is visible rather than silent.

## Implementation Notes

**2026-07-29 15:18 UTC — User approved Option C (harden keyboard + dev tunnel) and Option D
(surface send failures).**

1. **`src/PcMarket.Bot/Presentation/PublicUrl.cs`** (new) — `IsReachableByTelegram(string?)`. Requires
   an absolute `http`/`https` URI and rejects loopback, dotless hosts (Compose service names like
   `nginx`), the `*.localhost` dev convention, and RFC1918/link-local addresses. Deliberately
   conservative: a false negative costs one button, a false positive costs the whole message.
2. **`BotKeyboards.Product`** — the `IsNullOrWhiteSpace` guard replaced with
   `PublicUrl.IsReachableByTelegram`. This is the actual bug fix.
3. **`BotKeyboards.OrderDetail`** — same guard applied to the "Pay now" URL button, which had the
   identical latent failure mode. An unreachable gateway URL now degrades to the existing callback
   button (which re-initiates payment) instead of taking the order card down.
4. **`BotResponder.SendAsync`** — `LogWarning` raised to `LogError`, and on failure the message is
   retried once **without** its keyboard. A rejected button can no longer swallow the whole reply;
   the user gets a readable card rather than silence. Guarded against recursion (`keyboard is null`
   returns immediately).
5. **`docker-compose.yml`** — added `tunnel-web` (profile `dev`), a second Cloudflare quick tunnel
   giving the storefront a public URL, host-header-rewritten to `STOREFRONT_HOST`.
6. **`.env`** — `PUBLIC_STOREFRONT_URL=https://jacksonville-centers-cds-particularly.trycloudflare.com`.
7. **`tests/PcMarket.UnitTests/Bot/ProductKeyboardTests.cs`** (new) — 15 regression cases.

API image rebuilt and container recreated at 15:30 UTC.

## Verification Results

| Check | Result |
| --- | --- |
| `dotnet test --filter ~Bot` (unit) | **32 passed**, 0 failed (was 17 before the new tests) |
| `dotnet test --filter ~Phase7Bot` (integration) | **2 passed**, 0 failed |
| Storefront tunnel reachable | `GET /` → **200** |
| Simulated product tap (Kingston FURY — the exact product that failed at 15:02:17 and 15:03:30) | webhook → **200**, **0** send failures, **0** `Wrong HTTP URL` |
| Simulated add-to-cart → view cart | both → **200**, **0** send failures |
| Cart persisted in PostgreSQL | `Carts.Token = 'tg576388865'` → **1 item, 650000.00** |

The decisive comparison: before the fix every product tap logged
`400 ... 'http://localhost:8080/product/...' is invalid: Wrong HTTP URL`. After it, the same taps log
nothing and the cart row exists.

## Next Steps

- **User-facing confirmation:** tap a product in Telegram; the card should render with
  "➕ Add to cart · 650 000 UZS" and a working "🌐 Open in store".
- **Ephemeral tunnels:** both `PUBLIC_API_URL` and `PUBLIC_STOREFRONT_URL` point at quick tunnels
  whose hostnames change on restart. `PUBLIC_API_URL` additionally requires re-registering the
  webhook. See `docs/runbooks/telegram-bot.md`.
- **No production risk from this bug** once `PUBLIC_STOREFRONT_URL` is a real domain — but the
  hardening means a bad value degrades to a missing button instead of a dead product page.
