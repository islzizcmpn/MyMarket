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
| `Telegram:AdminChatId` | `TELEGRAM_ADMIN_CHAT_ID` | Chat receiving new-order alerts — a manager group is the recommended target ("Sending new-order alerts to a manager group"). Unset = no alerts |
| `Telegram:PublicApiUrl` | `PUBLIC_API_URL` | Public base URL; the webhook is registered at `{PublicApiUrl}/api/v1/bot/telegram/webhook` |
| `Telegram:StorefrontUrl` | `PUBLIC_STOREFRONT_URL` | Used for "open in store" deep links |
| `Telegram:PageSize` | — | Products per catalog page (default 6) |

Secrets go in `.env` (gitignored) or Docker secrets — **never** `appsettings.json`, which is in git.
For local `dotnet run`, `.env` is not read at all; use user secrets instead:

```bash
dotnet user-secrets set "Telegram:BotToken" "..." --project src/PcMarket.Api
```

## Bringing the bot up

**Fast path (Windows, after the bot is already configured once):** **double-click
`scripts/start-telegram-bot.cmd`**. It starts Docker Desktop if needed, brings up the stack,
recreates both quick tunnels, waits for each to **register a connection with Cloudflare's edge**, writes
their URLs into `.env`, restarts the API, and re-registers the webhook — the manual steps below,
automated. Safe to re-run any time, e.g. after a PC restart.

The tunnels are recreated rather than reused on purpose: a quick tunnel that has been running for hours
can lose its connection to Cloudflare and never recover, leaving a container that is still `Up` and a
hostname that no longer resolves. Reusing it would register a dead URL — which Telegram accepts without
complaint, leaving a silent bot and a script that reported success.

The gate is cloudflared's own `Registered tunnel connection` line, not an HTTP request from your machine.
A tunnel stuck in a retry loop never prints it, which is exactly the failure worth catching — while a
hostname minted seconds ago often *does not resolve locally yet* (or resolves to an AAAA record on a
machine with no IPv6 route) even though Telegram reaches it without trouble. The script still probes the
URL, but only warns if that probe fails, because local DNS says nothing about what Telegram can see.
Requires `TELEGRAM_ENABLED=true`, `TELEGRAM_BOT_TOKEN` and `TELEGRAM_WEBHOOK_SECRET` already set in
`.env` (steps 1-3 below, one-time setup).

Use the `.cmd`, not the `.ps1`, unless you are already in a terminal. PowerShell's default execution
policy on Windows client is `Restricted`, so double-clicking the `.ps1` (or "Run with PowerShell")
refuses to run it and the window closes before the error can be read. The wrapper passes
`-ExecutionPolicy Bypass` **for that one process only** — it changes no machine setting — and pauses
at the end so the result stays on screen.

From an existing terminal, either of these works:

```powershell
.\scripts\start-telegram-bot.cmd
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\start-telegram-bot.ps1
```

### After changing code, pass `-Build`

```powershell
.\scripts\start-telegram-bot.cmd -Build
```

Without it the stack comes up from the **image that is already on disk**. `docker compose up -d`
recreates the *container* but never rebuilds the *image*, so a plain restart happily runs code from
hours ago — the bot answers normally, and only the behaviour you just wrote is missing. Nothing in the
logs says so; the giveaway is comparing timestamps:

```bash
docker image inspect pcmarket/api:dev --format "{{.Created}}"
```

If that is older than your edits, you are looking at stale code. To rebuild only the API and leave the
running tunnels (and therefore the registered webhook) untouched:

```bash
docker compose --profile dev up -d --build api
```

Schema changes need nothing extra in dev: `DB_MIGRATE_ON_STARTUP=true` means the API applies pending
migrations as it boots. In production that switch is off — see `deploy.md`.

Manual steps, for first-time setup or when the script's assumptions don't hold (non-Windows,
production, troubleshooting):

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
   Telegram at a half-configured host is worse than not pointing it anywhere. It needs an admin JWT,
   fetched from a login call, then used to call `set-webhook` and `webhook-info`. **Repeat this step
   every time the dev tunnel restarts** — the hostname is ephemeral (step 4 above).

   The three commands below do the same thing; pick the one for your terminal. `curl`, `$(...)`
   assignment, and header syntax are not portable between shells — this has tripped up new
   contributors before.

   <details><summary>macOS / Linux / Git Bash — <code>jq</code> installed</summary>

   ```bash
   TOKEN=$(curl -s -X POST http://api.localhost:8080/api/v1/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"phone":"+998900000000","password":"Admin!23456"}' | jq -r .accessToken)

   curl -s -X POST http://api.localhost:8080/api/v1/bot/telegram/set-webhook \
     -H "Authorization: Bearer $TOKEN"

   curl -s http://api.localhost:8080/api/v1/bot/telegram/webhook-info \
     -H "Authorization: Bearer $TOKEN"
   ```

   </details>

   <details><summary>Git Bash — no <code>jq</code> (default on a fresh Windows machine)</summary>

   `jq` is not bundled with Git for Windows, and a `winget install` may land outside Git Bash's `PATH`
   even when it "succeeds". This variant needs nothing beyond what Git Bash already ships with:

   ```bash
   TOKEN=$(curl -s -X POST http://api.localhost:8080/api/v1/auth/login \
     -H 'Content-Type: application/json' \
     -d '{"phone":"+998900000000","password":"Admin!23456"}' | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)

   curl -s -X POST http://api.localhost:8080/api/v1/bot/telegram/set-webhook \
     -H "Authorization: Bearer $TOKEN"

   curl -s http://api.localhost:8080/api/v1/bot/telegram/webhook-info \
     -H "Authorization: Bearer $TOKEN"
   ```

   </details>

   <details><summary>Windows PowerShell</summary>

   PowerShell's `curl` is an alias for `Invoke-WebRequest` (different flags), and `Invoke-RestMethod`
   does not resolve `*.localhost` the way browsers do — hit `127.0.0.1` instead and send the real
   hostname via an explicit `Host` header so Nginx still routes to the right container. Run this as
   one paste; PowerShell variables don't survive between separate command invocations the way `$TOKEN`
   does in bash:

   ```powershell
   $body = @{ phone = "+998900000000"; password = "Admin!23456" } | ConvertTo-Json
   $login = Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/auth/login" -Method Post -ContentType "application/json" -Headers @{ Host = "api.localhost" } -Body $body
   $token = $login.accessToken

   Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/bot/telegram/set-webhook" -Method Post -Headers @{ Host = "api.localhost"; Authorization = "Bearer $token" } | ConvertTo-Json

   Invoke-RestMethod -Uri "http://127.0.0.1:8080/api/v1/bot/telegram/webhook-info" -Method Get -Headers @{ Host = "api.localhost"; Authorization = "Bearer $token" } | ConvertTo-Json
   ```

   </details>

6. **Check the output of `webhook-info`:** `url` should be your public webhook and `lastErrorMessage`
   empty. Then send `/start` to the bot.

## How the webhook is authenticated

The webhook is anonymous by necessity — Telegram cannot present a JWT. Instead `set-webhook` hands
Telegram the shared secret, which it echoes back in `X-Telegram-Bot-Api-Secret-Token` on every update;
the endpoint compares it in constant time and returns 401 on a mismatch. The secret therefore only
ever leaves the host once, at registration.

The other two endpoints (`set-webhook`, `webhook-info`) require the Admin policy.

Handling never throws — failures are reported into the chat — so Telegram always receives a 200 and
does not retry the same update.

## Commands

`/start` `/menu` `/help` `/catalog` `/search <query>` `/cart` `/orders` `/language` `/link` `/unlink`
`/chatid`

Most navigation is inline keyboards rather than commands.

## Sending new-order alerts to a manager group

`Telegram:AdminChatId` takes one chat, and a **private group** is the better thing to point it at than
any one manager's DM: staff are added and removed by group membership, with no redeploy and no config
change, and nobody's phone is the single place an order can land.

1. Create a private group and add the bot to it.
2. Send `/chatid` in the group. The bot answers with the group's id — negative, e.g. `-1001234567890`.
   (The id cannot be read off an invite link, and `getUpdates` is unavailable while a webhook is
   registered, so the chat has to be asked directly.)
3. Put it in `TELEGRAM_ADMIN_CHAT_ID` and restart the API: `docker compose up -d --no-deps api`.
4. Place a test order and confirm the alert lands.

Each manager must still `/link` their own account in a **private** chat with the bot and hold the Admin
or Manager role. Group membership decides who *sees* alerts; the linked account decides who may act on
them, so forwarding an alert grants the recipient nothing.

Privacy mode (on by default) means the bot receives only commands and replies in the group, never the
staff chatter around them. Button presses are delivered regardless, so the alert's action buttons work
without granting the bot admin rights or turning privacy off.

One sharp edge: a **basic group becomes a supergroup** the moment someone enables history-for-new-members,
sets a public link, or adds enough people — and the id changes (`-123…` becomes `-100123…`). Alerts stop
silently. Re-run `/chatid` and update `.env`.

## Languages

The bot speaks Russian, Uzbek and English — the same three the storefront ships in — reachable from the
**🌐 Language** button in the main menu or `/language`. Labels in the panel are written in the language
they select, so a customer who cannot read the current one can still find their own.

Which language a chat gets, in order:

1. the language stored on the **linked account** (`AspNetUsers.Language`) — it lives on the account rather
   than in bot session state, so it survives a Redis flush and is shared with the admin panel, which reads
   and writes the same column through `PUT /api/v1/users/me/language` (the storefront still picks its
   culture from its own cookie, so it does not read this column yet);
2. for a chat with no account yet, the guest choice in Redis under `bot:lang:{telegramUserId}` (180-day
   TTL), which is copied onto the account the moment they link — unless the account already carries one;
3. the language the customer's **own Telegram app** is in, when it is one of the three;
4. Russian.

Only the bot's own wording comes from `BotPhrases`. Category and product names are translated by the
Application layer: the update handler puts the chosen language on `CultureInfo.CurrentUICulture`, which
is what `ILanguageContext` reads, so those names arrive already translated — or fall back to their
English column when the back office has not translated them yet.

New-order alerts are written in the language of the account behind `Telegram:AdminChatId`. A group or
channel belongs to no one account, so alerts posted there are in Russian.

Adding a language is a three-line change to `ContentLanguages.All` plus a translation per entry in
`BotPhrases`; a unit test fails if any phrase is left untranslated.

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
| Bot silent right after a clean-looking script run; `webhook-info` shows a URL and no error | The quick tunnel is **up but disconnected** — cloudflared lost its edge connection and its hostname stopped resolving, while the container stayed `Up`. Confirm with `nslookup <host>` (NXDOMAIN) or `docker compose logs tunnel \| grep "Registered tunnel connection"` (nothing recent, just retry loops), then re-run the script — it now recreates the tunnels and probes the URL before registering |
| `webhook-info` shows `Wrong response from the webhook: 401` | `WebhookSecretToken` changed after registration — re-run `set-webhook` |
| `... 404` | `Telegram:Enabled` is false in the running container, or Nginx is not routing `API_HOST` |
| `set-webhook` returns 503 | Token missing/blank — the client was never constructed |
| `set-webhook` returns 400 | `PublicApiUrl` or `WebhookSecretToken` unset |
| Telegram rejects the URL at registration | Not HTTPS, or a private/unreachable address |
| A button press does nothing at all, log shows `400 ... is invalid: Wrong HTTP URL` | A URL button points somewhere Telegram cannot reach, and it rejects the **whole** message. Usually `PUBLIC_STOREFRONT_URL` left on `localhost`. `PublicUrl.IsReachableByTelegram` now drops such buttons, so this should only appear for URLs it wrongly accepts |
| Bot works, but a button/message you just wrote is missing | The container is running an older image. `docker compose up -d` recreates the container without rebuilding — compare `docker image inspect pcmarket/api:dev --format "{{.Created}}"` against your edits, then re-run with `-Build` (see "After changing code") |
| A new column is missing / EF throws `column ... does not exist` | The image is current but the migration never ran: check `DB_MIGRATE_ON_STARTUP` and `select "MigrationId" from "__EFMigrationsHistory" order by 1 desc limit 3;` |
| Bot answers but forgets context mid-checkout | Redis unavailable or the 30-minute state TTL expired |
| Orders placed, no admin alert | `AdminChatId` unset, or the bot has never been messaged by that chat — Telegram forbids opening a conversation first |
| Alerts stopped after moving to a group | Group ids are negative and change when a group is upgraded to a supergroup; re-read the id with `/chatid` in the group |
| Alerts land in the group but a manager's buttons say "You need a linked manager account" | Group membership only decides who sees alerts. That manager must `/link` in a **private** chat with the bot and hold the Admin or Manager role |

Pending updates queue at Telegram while the API is down and are delivered on reconnect, so a restart
loses nothing.

## Rotating the token

`/revoke` in @BotFather invalidates the old token immediately. Update `.env`, restart the API, then
**re-register the webhook** — revocation drops the existing registration.
