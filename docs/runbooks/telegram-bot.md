# Runbook: Telegram bot

The bot is the third client, alongside the storefront and the mobile app. It is **not** a separate
process: `PcMarket.Bot` holds the update handlers and `PcMarket.Api` hosts the webhook, so the bot
shares the API's configuration, database, and Application services. Nothing extra to deploy.

Conversation state lives in Redis with a 30-minute TTL, so an abandoned checkout drops back to the
main menu on its own rather than lingering.

## Configuration

Everything binds from the `Telegram` section (`TelegramSettings`). The bot is **off by default** —
without `Enabled=true` and a token the webhook returns 404 and every outbound send is a no-op.

| Setting | Env var (compose) | Notes |
| --- | --- | --- |
| `Telegram:Enabled` | `TELEGRAM_ENABLED` | Master switch |
| `Telegram:BotToken` | `TELEGRAM_BOT_TOKEN` | From @BotFather. Secret |
| `Telegram:WebhookSecretToken` | `TELEGRAM_WEBHOOK_SECRET` | Secret. Required — the webhook rejects **every** update while it is unset |
| `Telegram:AdminChatId` | `TELEGRAM_ADMIN_CHAT_ID` | Chat receiving new-order alerts. Unset = no alerts |
| `Telegram:PublicApiUrl` | `PUBLIC_API_URL` | Public base URL; the webhook is registered at `{PublicApiUrl}/api/v1/bot/telegram/webhook` |
| `Telegram:StorefrontUrl` | `PUBLIC_STOREFRONT_URL` | Used for "open in store" deep links |
| `Telegram:PageSize` | — | Products per catalog page (default 6) |

Secrets go in `.env` (gitignored) or Docker secrets — **never** `appsettings.json`, which is in git.
For local `dotnet run`, `.env` is not read at all; use user secrets instead:

```bash
dotnet user-secrets set "Telegram:BotToken" "..." --project src/PcMarket.Api
```

## Bringing the bot up

1. **Create the bot** with @BotFather, `/newbot`. Keep the token.
2. **Generate a webhook secret.** Telegram only accepts `A-Z a-z 0-9 _ -`, so hex — not base64:

   ```bash
   openssl rand -hex 32
   ```

3. **Fill in `.env`** and restart the API:

   ```bash
   docker compose up -d --no-deps api
   ```

4. **Expose a public HTTPS URL.** Telegram will not call a private address or plain HTTP.
   - *Production*: the real domain, TLS terminated at Nginx (see `deploy.md`). Set
     `PUBLIC_API_URL=https://api.example.com`.
   - *Development*: the bundled quick tunnel — no Cloudflare account needed:

     ```bash
     docker compose --profile dev up -d tunnel
     docker compose logs tunnel | grep trycloudflare.com
     ```

     Put that hostname in `PUBLIC_API_URL`, then `docker compose up -d --no-deps api`. The URL is
     **ephemeral** — it changes every time the tunnel restarts, and the webhook must be re-registered
     when it does.

5. **Register the webhook.** This is a deliberate manual step, not something startup does — pointing
   Telegram at a half-configured host is worse than not pointing it anywhere. It needs an admin JWT:

   ```bash
   TOKEN=$(curl -s -X POST http://api.localhost:8080/api/v1/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"phone":"+998900000000","password":"Admin!23456"}' | jq -r .accessToken)

   curl -s -X POST http://api.localhost:8080/api/v1/bot/telegram/set-webhook \
     -H "Authorization: Bearer $TOKEN"
   ```

6. **Verify:**

   ```bash
   curl -s http://api.localhost:8080/api/v1/bot/telegram/webhook-info \
     -H "Authorization: Bearer $TOKEN"
   ```

   `url` should be your public webhook and `lastErrorMessage` empty. Then send `/start` to the bot.

## How the webhook is authenticated

The webhook is anonymous by necessity — Telegram cannot present a JWT. Instead `set-webhook` hands
Telegram the shared secret, which it echoes back in `X-Telegram-Bot-Api-Secret-Token` on every update;
the endpoint compares it in constant time and returns 401 on a mismatch. The secret therefore only
ever leaves the host once, at registration.

The other two endpoints (`set-webhook`, `webhook-info`) require the Admin policy.

Handling never throws — failures are reported into the chat — so Telegram always receives a 200 and
does not retry the same update.

## Commands

`/start` `/menu` `/help` `/catalog` `/search <query>` `/cart` `/orders` `/link` `/unlink`

Most navigation is inline keyboards rather than commands.

**Account linking** has two routes, and they cost different amounts:

- **Shared contact card** (the "📱 Share my phone number" button) — links immediately, no code, **no
  SMS**. Telegram verified that number at signup and delivers it from its own servers, so an OTP would
  only re-prove a proven fact. Always available.
- **Typed number** — unverified input, so it needs an SMS OTP. **Disabled by default**
  (`Telegram:AllowPhoneEntry=false`) because it is the only route in the bot that spends money; with no
  SMS provider funded it would strand the customer waiting for a code that never arrives. Typing a
  number simply points the customer back at the button.

Turning it on (`TELEGRAM_ALLOW_PHONE_ENTRY=true`) is what a funded SMS provider buys you. The one thing
it unlocks: a customer whose PcMarket account is registered to a **different** number than their
Telegram account. Until then such a customer can only create/link an account for their Telegram number,
and reconciling the two is a back-office job.

The distinction is enforced by one check: a shared card is trusted only when its `UserId` equals the
sender's. Telegram lets a user share **any** card from their address book, so without that check
anyone could forward a stranger's contact and link themselves to that person's account. A card that
fails the check is treated as a typed number, not rejected. Covered by `Phase7BotContactLinkTests`.

Because the button path sends no SMS, most linking traffic costs nothing — worth knowing when sizing
an SMS provider balance.

Staff actions (advancing order status from the new-order alert) require *both* a verified link and the
Admin or Manager role — `AdminFlow` re-checks the role on every action rather than trusting the
keyboard it sent earlier.

## Where to look when the bot misbehaves

| Symptom | Where to look |
| --- | --- |
| Bot silent, `webhook-info` shows no URL | `set-webhook` was never called, or was called before `PUBLIC_API_URL` was set |
| `webhook-info` shows `Wrong response from the webhook: 401` | `WebhookSecretToken` changed after registration — re-run `set-webhook` |
| `... 404` | `Telegram:Enabled` is false in the running container, or Nginx is not routing `API_HOST` |
| `set-webhook` returns 503 | Token missing/blank — the client was never constructed |
| `set-webhook` returns 400 | `PublicApiUrl` or `WebhookSecretToken` unset |
| Telegram rejects the URL at registration | Not HTTPS, or a private/unreachable address |
| A button press does nothing at all, log shows `400 ... is invalid: Wrong HTTP URL` | A URL button points somewhere Telegram cannot reach, and it rejects the **whole** message. Usually `PUBLIC_STOREFRONT_URL` left on `localhost`. `PublicUrl.IsReachableByTelegram` now drops such buttons, so this should only appear for URLs it wrongly accepts |
| Bot answers but forgets context mid-checkout | Redis unavailable or the 30-minute state TTL expired |
| Orders placed, no admin alert | `AdminChatId` unset, or the bot has never been messaged by that chat — Telegram forbids opening a conversation first |
| Alerts stopped after moving to a group | Group ids are negative and change when a group is upgraded to a supergroup; re-read the id |

Pending updates queue at Telegram while the API is down and are delivered on reconnect, so a restart
loses nothing.

## Rotating the token

`/revoke` in @BotFather invalidates the old token immediately. Update `.env`, restart the API, then
**re-register the webhook** — revocation drops the existing registration.
