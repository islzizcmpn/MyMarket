# pcmarket_clone

## Status
**Complete — all phases 0–9 done, last updated 2026-07-26.** Phase 9 containerizes the system: multi-stage
Dockerfiles for API/Web/Admin, a full `docker-compose.yml` (nginx + api + web + admin + postgres + redis +
minio, only nginx exposed), host-based Nginx routing with WebSocket support and a MinIO media pass-through,
GitHub Actions CI publishing images to GHCR, a manual deploy workflow with a **gated** migration step, tested
backup/restore scripts, and three runbooks. Verified from an empty volume set: `cp .env.example .env &&
docker compose up -d --build` brings every service to healthy, and the full shopper journey — browse → guest
cart → register + OTP → cart merge → COD checkout → `ORD-260726-930F369F` in `Processing` — runs through
Nginx. **60 tests green — 43 unit + 17 integration**; solution builds clean.
Outstanding across the project (all blocked on credentials/hardware, not code): no live Click/Payme merchant
keys, no BotFather token + public HTTPS webhook, no Firebase project (mobile FCM client SDK stubbed — see
[mobile_app/push-setup.md](mobile_app/push-setup.md)), no Mac/Apple account for iOS, and the CI/deploy
workflows have never run (no GitHub runner or VPS here).
Earlier: Phase 8 delivers the .NET MAUI app (Android verified on an emulator, iOS compiling only) plus the
backend push registry; detail in [mobile_app/plan.md](mobile_app/plan.md).
Earlier: Phases 0–7 complete. Phase 7 delivers the Telegram bot: the
`PcMarket.Bot` module (webhook handler, Redis conversation state, catalog/cart/checkout/order/account/admin
flows) behind a secret-token-validated `POST /api/v1/bot/telegram/webhook`, account linking by phone + OTP,
an admin new-order channel with role-guarded status buttons, and the notifications Telegram channel wired to
the live bot. **38 tests green — 26 unit + 12 integration** (incl. 2 new `Phase7BotTests`), verified on
2026-07-26 against Docker Postgres 17 + Redis; the API also boots clean with `/health` reporting
postgres/redis/minio all Healthy. Everything builds clean. Still outstanding: no live verification against a
real BotFather token + public webhook (needs a token and an HTTPS tunnel).
Earlier phases: Phases 0–6 complete, incl. banners/CMS blocks. Phase 6 delivers the admin API (`/api/v1/admin/*`, Admin/Manager-gated) for catalog/order/content management, dashboard stats, media upload, catalog import, and an audit trail (`AuditLogEntry`); content (`Banner`/`CmsBlock`) with public `/api/v1/content/*` feeding the storefront; an `AdminApiClient` + `ContentApiClient`; and the `PcMarket.Admin` Blazor Server panel (login, dashboard w/ live SignalR new-order feed, product editor w/ stock/price, categories/brands/content CRUD, order management w/ status-advance/refund, JSON import, audit viewer). Two migrations added (`AddAuditLog`, `AddContent`). Verified live in-browser: admin login + dashboard (feed "connected") + product management; storefront home renders seeded banners + CMS block. 19 tests green — 9 unit + 10 integration; full solution builds clean. Deferred: Web static-SSR/SEO; live notification-channel creds.)

## Purpose
Implement the full `pcmarket_clone` system defined in [architecture.md](architecture.md): a C#/.NET
modular-monolith backend serving a Blazor web storefront, a .NET MAUI mobile app (iOS + Android),
and a Telegram bot, with Uzbekistan payment rails (Click, Payme, Uzcard/Humo, Cash on Delivery),
running on self-hosted Docker. This plan turns the architecture into an ordered, trackable build.

## Scope
**In scope:**
- Solution scaffold matching the architecture's project layout (`PcMarket.*`).
- Domain model, EF Core + PostgreSQL persistence, and migrations for all core entities.
- Backend REST API (`/api/v1`) for Catalog, Cart, Orders, Payments, Users/Auth, Notifications, Admin.
- Payment provider abstraction with Click, Payme, Uzcard/Humo, and Cash providers + webhook handlers.
- Auth (phone-first Identity + JWT + refresh tokens, SMS OTP).
- Blazor Web storefront (SSR catalog/product + interactive cart/checkout/account).
- Blazor Server admin panel (catalog/order/content management).
- Telegram bot (browse, search, cart, checkout, order tracking, admin new-order channel).
- .NET MAUI mobile app (catalog, cart, checkout, order tracking, push).
- Cross-cutting: Redis caching, Hangfire jobs, SignalR, MinIO media, Serilog logging, health checks.
- Docker Compose stack (API, Postgres, Redis, MinIO, Nginx) + GitHub Actions CI.

**Out of scope (deferred / see architecture "Future Considerations"):**
- Multi-seller/marketplace, loyalty/promo codes, reviews/ratings, wishlist, recommendations.
- Multi-language storefront (structure for it, but ship one primary language first).
- Managed-cloud migration, Kubernetes, dedicated search engine, CDN.
- iOS App Store / Google Play submission (build + local run only until signing accounts exist).
- Full HA (second node); single-node deploy first.

## Requirements
1. A `PcMarket.sln` with the projects from architecture §"Solution / Project Layout", wired with the
   documented reference directions (Contracts shared widely; Domain/Application/Infrastructure backend-only).
2. All core entities from architecture §"Data Models" modeled in `PcMarket.Domain`, persisted via EF Core
   to PostgreSQL 17, with migrations and a seed path (reference tables + demo catalog import).
3. Money stored as `decimal(18,2)` or integer minor units — never floating point; timestamps UTC.
4. Product `Specs` and variant `Attributes` stored as JSONB; slugs unique; FTS index for search.
5. REST API versioned under `/api/v1` covering the endpoints listed per module in the architecture,
   documented via OpenAPI, with FluentValidation on inputs.
6. `IPaymentProvider` abstraction with four implementations (Click, Payme, Uzcard/Humo, Cash); gateway
   webhook endpoints that verify signatures/amounts and update a `PaymentTransaction` ledger **idempotently**.
7. Order lifecycle enforced as a state machine (Created → AwaitingPayment/COD → Paid → Processing →
   Shipped → Delivered → Cancelled/Refunded) with `OrderStatusHistory` recorded on every transition.
8. Auth: phone-first registration/login, ASP.NET Core Identity password hashing, SMS OTP verification,
   short-lived JWT access tokens + rotating revocable refresh tokens, roles Customer/Manager/Admin.
9. Cart supports guest (Redis token) and authenticated (Postgres) carts with merge-on-login.
10. Blazor Web storefront renders catalog/product via static SSR and handles cart/checkout/account
    interactively, consuming the API and shared `PcMarket.Contracts`.
11. Blazor Server admin panel (Admin/Manager roles) for catalog/pricing/stock/media/order management,
    with a live SignalR new-order feed.
12. Telegram bot (webhook, secret-token protected) supporting catalog browse/search, cart, checkout,
    order tracking, account linking, and an admin channel for new-order alerts + status advance.
13. .NET MAUI app (Android + iOS targets) for catalog, cart, checkout, and order tracking, using the
    shared `PcMarket.ApiClient`, with FCM push registration.
14. Notifications module fanning out order/payment events over Telegram, push, SMS, email behind channel
    abstractions, delivered via Hangfire with retries.
15. `docker-compose.yml` bringing up API + Postgres + Redis + MinIO + Nginx (TLS) locally and on the VPS,
    with configuration via env vars/secrets and per-provider feature flags; GitHub Actions build/test/image pipeline.

## Acceptance Criteria
- `dotnet build PcMarket.sln` succeeds; `dotnet test` passes with the seeded test suite green.
- `docker compose up` starts the full stack; API `/health` reports all dependencies healthy.
- A shopper can, against a running stack: browse categories, open a product, add to cart as guest,
  register/login (phone + OTP stub), have the guest cart merge, check out, and receive an order in
  `AwaitingPayment` (online) or `Processing` (COD).
- A Click **and** a Payme sandbox payment flow drive an order to `Paid`; replaying the same webhook does
  not double-apply (idempotency verified by test).
- An admin can log into the admin panel and create/edit a product, adjust stock/price, and advance an order's status; the change is reflected in the storefront.
- The Telegram bot (against test bot token) lists categories, searches, adds to cart, places an order, and reports its status; a new order posts an alert to the admin channel.
- The MAUI app builds for Android and runs against the API: catalog loads, cart works, an order can be placed and its status viewed.
- OpenAPI document is served and lists all `/api/v1` endpoints.
- No card/PAN data is ever persisted; only transaction references + raw gateway payloads for audit.

## Design Approach
Build bottom-up so each layer is runnable before the next depends on it, and so the API is demoable
early. Order of construction: (1) solution scaffold + shared libraries, (2) domain + EF Core persistence,
(3) cross-cutting infrastructure (Redis, MinIO, Serilog, Hangfire, auth), (4) backend modules feature by
feature (Catalog → Cart → Orders → Payments → Notifications), (5) the three clients (Web → Admin → Bot →
Mobile) against the now-stable API, (6) containerization + CI. Payments and Orders are the highest-risk
area (idempotent webhooks, state machine) and get dedicated integration tests.

Key structural decisions inherited from the architecture: clean layering per module (Domain →
Application → Infrastructure → API); `PcMarket.Contracts` as the single DTO source shared by every
client; `IPaymentProvider` keeping checkout payment-agnostic; guest carts in Redis, user carts in
Postgres; JWT stateless auth so multi-instance and mobile work uniformly. New files are essentially the
entire tree under `src/`, `shared/`, `tests/`, plus `docker-compose.yml`, `Dockerfile`s, `nginx/`, and
`.github/workflows/`. Nothing is modified (greenfield).

Phasing note: this is a large system. The checklist is grouped into phases; phases 0–5 deliver a working
web storefront + API, and 6–9 add the remaining clients and ops. Each phase should build and, where
applicable, pass tests before moving on.

## Codebase Research
- **Greenfield.** Project root (`c:\MyProjects\Market`) contains only `docs/`, `.github/`, `.claude/`,
  `.cursor/` — no `.sln`, `.csproj`, or source yet. Not a git repository.
- **.NET 10 SDK present**: `dotnet --version` → `10.0.400-preview.0.26322.102`, matching the architecture's
  .NET 10 target. `dotnet new` templates for `web`, `blazor`, `maui`, `classlib`, `xunit` are available.
- **Authoritative source is [architecture.md](architecture.md)** — it fixes the project layout, entity list,
  module responsibilities/endpoints, payment provider contracts (Click Prepare/Complete; Payme JSON-RPC
  `CheckPerformTransaction`/`CreateTransaction`/`PerformTransaction`/`CancelTransaction`/`CheckTransaction`),
  data-storage split (Postgres/Redis/MinIO), and security model. Follow it rather than inventing structure.
- **Conventions to establish (none exist yet):** nullable enabled, implicit usings, `Directory.Build.props`
  for shared settings, one EF `DbContext` (`PcMarketDbContext`) with module-grouped configurations,
  `snake_case` DB naming via Npgsql convention, API route prefix `/api/v1`.
- **External API references** to consult during Payments/Bot work: Click Merchant API, Payme Merchant API
  (JSON-RPC), Telegram Bot API + `Telegram.Bot` v22.x (all listed in architecture §Appendices/References).

## Implementation Checklist

### Phase 0 — Solution scaffold & tooling
- [x] `git init`; add `.gitignore` (dotnet), `Directory.Build.props` (nullable, implicit usings, warnings-as-errors), `.editorconfig`.
- [x] Create `PcMarket.slnx` (.NET 10 XML solution format) and all projects per architecture layout under `src/`, `shared/`, `tests/`.
- [x] Wire project references (Contracts shared to clients; Domain→∅; Application→Domain+Contracts; Infrastructure→Application; Payments→Application; Bot→Application+Contracts; ApiClient→Contracts; Api→Infrastructure+Payments+Bot; Web/Admin/Mobile→ApiClient+Contracts; tests→relevant src).
- [x] Add central package management (`Directory.Packages.props`) pinning EF Core, Npgsql, FluentValidation, Mapster, Serilog, Hangfire, Telegram.Bot, StackExchange.Redis, Minio, JWT/Identity, xUnit, Testcontainers, NetArchTest.
- [x] Confirm `dotnet build` succeeds on the scaffold (all projects except the MAUI head, whose Android/iOS build is deferred to Phase 8 per plan). Notes: Mobile opted out of CPM (uses `$(MauiVersion)`); NuGet audit codes NU19xx demoted to warnings due to the transitive `Microsoft.OpenApi` 2.x advisory (GHSA-v5pm-xwqc-g5wc) that has no in-major fix.

### Phase 1 — Domain & persistence
- [x] Model all entities from architecture §Data Models in `PcMarket.Domain` (Category, Brand, Product, ProductVariant, ProductImage, Address, Cart, CartItem, Order, OrderItem, OrderStatusHistory, PaymentTransaction, Notification) + enums (OrderStatus, PaymentStatus, PaymentMethod, DeliveryType, PaymentProvider, PaymentTransactionState, NotificationChannel/Status/Type). Note: the "User" from the data model is realized as `ApplicationUser : IdentityUser<Guid>` in Infrastructure (ASP.NET Core Identity); domain entities reference it by `UserId` only, keeping `PcMarket.Domain` free of the Identity dependency. Roles are Identity roles (Customer/Manager/Admin constants).
- [x] Add value objects/helpers: `MoneyConstants` (decimal(18,2), UZS), `SlugGenerator`, `UzbekistanRegions`; `Entity` domain-event buffer + `Order` state machine raising OrderPlaced/StatusChanged/Paid events.
- [x] Implement `PcMarketDbContext` (EF Core 10, Npgsql, extends `IdentityDbContext`) with per-entity `IEntityTypeConfiguration`; JSONB for Specs/Attributes/Payload (converter) and ShippingAddress (`OwnsOne().ToJson()`) and RawPayload; unique slug/SKU/order-number indexes; generated `tsvector` FTS column + GIN indexes on SearchVector and Specs; global decimal precision; domain-event buffers ignored.
- [x] Create initial EF migration (`InitialCreate`, 20 tables) + design-time factory (`POSTGRES_CONNECTION` env, dev default); verified it applies to a real Postgres 17 (Docker) — JSONB, generated tsvector, and GIN indexes materialize correctly.
- [x] Seed reference data (roles, admin user via `DbSeeder`; regions as `UzbekistanRegions`) + idempotent JSON catalog importer (`CatalogImporter`) with a sample `demo-catalog.json`. Covered by 2 Testcontainers integration tests + 9 domain unit tests (all green). Note: naming uses default quoted PascalCase (no snake_case convention package) so the FTS computed-column SQL stays stable.

### Phase 2 — Cross-cutting infrastructure
- [x] Serilog structured logging (bootstrap + host logger, `Enrich.FromLogContext`) + `CorrelationIdMiddleware` (X-Correlation-ID); `GlobalExceptionHandler` (IExceptionHandler) → ProblemDetails (DomainException→400, else logged 500).
- [x] Redis connection (`IConnectionMultiplexer`) + `ICacheService`/`RedisCacheService` (JSON, GetOrSet); output caching registered.
- [x] MinIO client + `IMediaStorage`/`MinioMediaStorage` (upload/get/presigned/delete, ensure-bucket).
- [x] Hangfire with PostgreSQL storage + server; dashboard at `/hangfire` guarded by `HangfireDashboardAuthorizationFilter` (Admin role).
- [x] Identity (phone-first, from Phase 1) + JWT issuance (`ITokenService`/`JwtTokenService`, HS256 via `JsonWebTokenHandler`) + rotating **refresh tokens** stored as SHA-256 hashes (`RefreshToken` entity + `AddRefreshTokens` migration + `IRefreshTokenStore`/`RefreshTokenStore`); roles seeded (Phase 1); `ISmsSender`/`NoOpSmsSender` (dev logger). `ICurrentUser`/`CurrentUser` from JWT.
- [x] API host wiring in `PcMarket.Api`: versioned `/api/v1` group (+ `/ping`), OpenAPI + Scalar UI, FluentValidation registration (from Application assembly), rate limiting (global per-IP + `auth` policy), CORS `clients` policy, `/health` checks (Postgres/Redis/MinIO), startup migrate+seed. **Smoke-tested**: API booted against Docker Postgres/Redis/MinIO, `/health` = Healthy for all three, `/api/v1/ping` OK, `/openapi/v1.json` served, Hangfire server started.
- [x] Extra deliverables: root `docker-compose.yml` (dev infra: Postgres/Redis/MinIO with healthchecks), `.env.example`, and `README.md` (build/run/test/config guide). Note: compose currently covers infra only; app services (api/web/admin/nginx) are added in Phase 9.

### Phase 3 — Catalog, Cart, Users APIs
- [x] Catalog endpoints (`/api/v1/catalog`): categories tree, brands, product list (category+descendants/brand/price filter, sort, paginate), product by slug, FTS `/search`. Redis caching of category tree + brands. FTS query isolated behind `IProductSearchQuery` → `PgProductSearchQuery` (raw `websearch_to_tsquery` + `ts_rank`) so Application stays provider-agnostic.
- [x] Contracts DTOs for catalog/cart/auth/users + hand-written mapping extensions (chose explicit mappers over Mapster for clarity); FluentValidation validators + `ValidationFilter<T>` endpoint filter.
- [x] Auth endpoints (`/api/v1/auth`): register (phone+password → OTP via `ISmsSender`, code cached), verify-otp (issues tokens), login, refresh (with rotation — old token revoked), logout; rate-limited via the `auth` policy. `IAuthService`/`AuthService` in Infrastructure over Identity `UserManager`.
- [x] Users endpoints (`/api/v1/users`, auth-required): `me` profile (via `UserManager`), addresses CRUD (`AddressService`, single-default + ownership).
- [x] Cart endpoints (`/api/v1/cart`): get/add/update/remove, guest cart via `X-Cart-Token` header (persisted in Postgres by token), `merge` on login; stock + price-snapshot validation. Note: guest carts are stored in Postgres (durable) rather than Redis — simpler and correct; a Redis mirror can be added later if needed.
- [x] Tests: 8 integration (WebApplicationFactory + Testcontainers Postgres/Redis) covering catalog tree/list/detail, FTS search, full register→verify→login→refresh→reuse-revoked flow, guest cart totals + token round-trip, over-stock 400, and guest→user merge; plus 9 unit. All green. Two bugs found & fixed during testing: (1) non-nullable value-type query params were required → made optional; (2) constructor-generated Guid keys needed `ValueGeneratedNever()` or insert-then-update in one unit of work threw a concurrency error. Also: `GlobalExceptionHandler` now includes exception detail in Development only.

### Phase 4 — Orders & Payments
- [x] Order creation from cart (stock check, price/name/address snapshotting) → AwaitingPayment or COD. (`OrderService.CreateAsync`; reserves stock, snapshots name/price/address, delegates the initial transition to the resolved provider.)
- [x] Order state-machine service enforcing valid transitions + writing `OrderStatusHistory`; cancel endpoint. (Transitions enforced by the domain `Order` aggregate; `OrderService.CancelAsync` restores stock; `POST /orders/{id}/cancel`.)
- [x] `IPaymentProvider` abstraction + `PaymentTransaction` ledger; `/payments/initiate`. (Abstraction + resolver in Application; `PaymentService`; `POST /payments/initiate`.)
- [x] Cash provider (COD → Processing, admin notified). (`CashPaymentProvider` → Processing; admin notification deferred to the Phase 5 Notifications module.)
- [x] Click provider: Prepare/Complete callbacks, MD5 sign + amount verification, idempotent ledger updates, required response codes. (`ClickPaymentProvider` initiate + `ClickCallbackService`/`ClickSignature`; `POST /payments/click/callback`.)
- [x] Payme provider: JSON-RPC methods (CheckPerformTransaction/CreateTransaction/PerformTransaction/CancelTransaction/CheckTransaction/GetStatement), Basic-auth header check, error-code contract, idempotency. (`PaymePaymentProvider` initiate + `PaymeRpcService`; `POST /payments/payme/callback`.)
- [x] Uzcard/Humo provider(s) via Click/Payme rails behind the same abstraction; feature-flag each provider. (`UzcardPaymentProvider`/`HumoPaymentProvider` ride the Click rail; every rail is `Enabled`-flagged in the `Payments` config.)
- [x] Hangfire jobs: auto-cancel unpaid orders (timeout), payment reconciliation. (`OrderMaintenanceService` + recurring jobs registered in `PaymentJobsExtensions`.)
- [x] Integration tests for both Click and Payme sandbox flows **including duplicate-webhook idempotency**. (`Phase4PaymentTests`: full checkout → webhook → `Paid`, with Click Complete and Payme Perform each replayed and asserted to leave exactly one settled ledger entry.)

### Phase 5 — Notifications, real-time, web storefront
- [x] Notifications module: `INotificationChannel` (Telegram/Push/SMS/Email), event handlers on OrderCreated/OrderPaid/StatusChanged, Hangfire fan-out with retries. (Domain-event dispatch added — `IDomainEventDispatcher`/`IDomainEventHandler<T>` in Application, reflection dispatcher in Infrastructure, invoked from `PcMarketDbContext.SaveChangesAsync` post-commit; `OrderNotificationHandler` enqueues `NotificationDeliveryService` via Hangfire (`HangfireNotificationJobScheduler`) which fans out over four dev/stub channels. Retries: Hangfire `AutomaticRetry` + a bounded per-channel retry loop — Polly not added as a dependency but can drop in behind `TrySendAsync`.)
- [x] SignalR hubs: order-status to customer, new-order feed to admin. (`OrderStatusHub` (per-user group) + `AdminOrderHub` (admin group); `SignalRRealtimeNotifier` pushes from the event handler; JWT delivered via query string for the hub handshake.)
- [x] `PcMarket.Web` Blazor Web App: layout, home/banners, category + product-list pages, product detail, search. (Header/search/cat-nav/footer layout, hero home + new-arrivals, `/catalog[/{slug}]` with brand/price/sort filters + paging, `/product/{slug}` with variant select + add-to-cart, `/search`. Verified rendering live against the API. **Deviation:** rendered globally Interactive Server (prerender off) rather than static SSR — simpler and avoids the SSR + `ProtectedSessionStorage` JS-interop conflict; SSR-for-SEO is a noted refinement.)
- [x] Web interactive: cart, checkout (address + payment method selection → initiate payment), account (orders, profile, addresses), auth pages. (`/cart`, `/checkout` → create order → initiate payment/redirect, `/account` (profile + orders + addresses + sign-out), `/account/orders/{id}` (pay/cancel), `/login`, `/register` (phone → OTP). Per-circuit `WebSession` persisted to `ProtectedSessionStorage`; guest cart merged on login.)
- [x] Web consumes API via `PcMarket.ApiClient`; SEO basics. (`PcMarket.ApiClient` built — six typed clients over `ApiClientBase` with per-request token/cart-token headers (`IApiTokenProvider`), string-enum JSON, `ApiException`. Page `<title>`s set. **Deferred with SSR:** sitemap, meta descriptions, canonical slugs.)

### Phase 6 — Admin panel
- [x] `PcMarket.Admin` Blazor Server, Admin/Manager auth; dashboard with live new-order SignalR feed. (Dark-themed admin app; phone+password login gated to Admin/Manager roles; dashboard stat cards + a `HubConnection` to `/hubs/admin` (JWT via query string) showing a live new-order feed — verified "connected" in-browser.)
- [x] Catalog management: categories/brands/products/variants CRUD, image upload to MinIO, price/stock edits. (Admin API `/api/v1/admin/*` behind an `AdminPanel` role policy + `AdminCatalogService`; product editor with inline variant price/stock/active rows, spec key-values, and image URLs; MinIO upload via `POST /admin/media` + a public `GET /api/v1/media/{key}` read-through. Products in orders are soft-deactivated instead of hard-deleted.)
- [x] Order management: list/filter, detail, status advance, refund trigger; customer lookup. (`AdminOrderService`; status advance goes through the order state machine so it emits the customer's Phase-5 notifications; customer info resolved via `IUserDirectory` without leaking Identity into Application.)
- [x] Content: banners/CMS blocks; catalog CSV/JSON import UI. (`Banner` + `CmsBlock` entities (`AddContent` migration); admin `/content` page manages both; public `GET /api/v1/content/banners` + `/blocks/{key}` feed the storefront home (banner strip + `home-intro` block), consumed via a new `ContentApiClient`; demo banners + intro block seeded in Development. JSON catalog import UI shipped — `/import` posts to `POST /admin/catalog/import` reusing the idempotent `CatalogImporter`.)
- [x] Admin action audit log. (`AuditLogEntry` domain entity + `AddAuditLog` migration; `IAuditLogger` records every catalog/order mutation with actor phone; `/audit` viewer.)

### Phase 7 — Telegram bot
- [x] Bot module + webhook controller in `Api` (secret-token validated); Redis conversation state. (`PcMarket.Bot` on `Telegram.Bot` 22.x: `TelegramClientAccessor` (single client, degrades to disabled without a token), `TelegramUpdateHandler` routing messages/commands/callbacks to five flows, `ConversationStore` over `ICacheService` (Redis) with a 30-min TTL. `POST /api/v1/bot/telegram/webhook` authenticates by constant-time compare of `X-Telegram-Bot-Api-Secret-Token`; admin-only `set-webhook`/`webhook-info` register and inspect it.)
- [x] Customer flows: /start + account linking, browse categories (inline keyboards), search, product view, add to cart, checkout, order tracking. (Category tree → paged product lists → product detail → add-to-cart, FTS search (any free text searches), cart with per-item remove, three-step checkout (region button → city → street) → payment-method choice → order + payment URL, order list/detail with pay/cancel. Guest carts keyed `tg{telegramUserId}`, merged on link. Linking is phone + OTP; an unknown phone is registered through `IAuthService` first.)
- [x] Admin channel: new-order alerts, inline actions to advance order status (role-guarded via linked account). (`TelegramAdminAlertHandler` on `OrderPlacedEvent` posts to `Telegram:AdminChatId` with a "Manage order" button; status buttons come from `Order.AllowedFrom` so they can never offer an illegal transition, and go through `AdminOrderService` — same history, audit, and customer notifications as the panel. Authorization reads the *linked account's* roles, so forwarding an alert grants nothing.)
- [x] Wire bot outbound into Notifications Telegram channel. (`ITelegramMessenger` in Application; Infrastructure registers a no-op, `AddBot` registers the live one after it, so `TelegramNotificationChannel` now delivers to the customer's linked chat — falling back to logging, and reporting success, when there is no token or no link, so neither burns the notification's retries.) **Not done: verification against a sandbox bot token** — needs a BotFather token and a public HTTPS tunnel, neither available in this environment.

### Phase 8 — MAUI mobile app
See the detailed plan in [mobile_app/plan.md](mobile_app/plan.md).
- [x] `PcMarket.Mobile` MAUI app (Android + iOS targets), DI + `PcMarket.ApiClient`, secure token storage. (Shell `TabBar` app, native XAML + `CommunityToolkit.Mvvm`; tokens in `SecureStorage`, guest cart token in `Preferences`. Session/refresh logic lives in a new `shared/PcMarket.Mobile.Core` — platform-neutral so the net10.0 test project can exercise it — with `SessionGuard` doing proactive refresh + one 401 retry, serialised so concurrent screens can't revoke each other's refresh token.)
- [x] Screens: home/catalog, product detail, cart, auth (phone+OTP), checkout, order list/tracking, profile. (12 pages/view models; guest cart merges into the account on sign-in via a shared `AuthFlow`; checkout gates anonymous users to sign-in and returns them to checkout; online rails hand off to the gateway in the system browser.)
- [x] FCM push registration + handling for order-status notifications. **Server side is real; the FCM client SDK is not wired.** (Added `DeviceToken` + `AddDeviceTokens` migration, `POST`/`DELETE /api/v1/users/me/device-tokens` (idempotent), `IPushSender`, and rewrote `PushNotificationChannel` to resolve the recipient's devices — all covered by tests. `Xamarin.Firebase.Messaging` cannot be referenced yet: its AndroidX constraints conflict with .NET 10 MAUI's and restore fails `NU1608`, so `IPushTokenSource` returns null and the app logs "push unavailable". See `mobile_app/push-setup.md`.)
- [x] Build + run on Android emulator against local API; document iOS build steps (deferred signing). (Verified on `pixel_7_-_api_36_0`: guest browse → add to cart → register + OTP → cart merged → COD checkout → order `ORD-260726-0B4A67C9` in `Processing` → survives a force-stop restart, zero fatal exceptions. iOS head compiles clean but has never been run — no Mac/Apple account; steps in `mobile_app/ios-build.md`.)

### Phase 9 — Containerization, CI, docs
- [x] `Dockerfile`s for Api, Web, Admin; multi-stage builds. (SDK 10.0 build stage → `aspnet:10.0` runtime; project files copied first so the restore layer caches; non-root `$APP_UID`; curl added only to answer the compose healthcheck. Repo-root build context with a `.dockerignore` that excludes `bin`/`obj`, `tests/`, `docs/`, and the MAUI head. All three images verified building.)
- [x] `docker-compose.yml`: api, web, admin, postgres, redis, minio, nginx; healthchecks; volumes; internal network; `.env.example`. (Eight services on an internal `pcmarket` bridge with only nginx published; `depends_on` gated on health so the API never boots before Postgres/Redis/MinIO are ready. Added a `minio-init` one-shot that creates the media bucket and sets its anonymous-download policy — without it the Nginx `/media/` route 403s, since the app creates the bucket private. Two `tools`-profile services: `migrate` (gated schema step) and `certbot`.)
- [x] Nginx config: TLS (Let's Encrypt), reverse proxy/routing to web/admin/api, static/media pass-through. (Host-based routing via env-substituted templates — `localhost`/`admin.localhost`/`api.localhost` locally, real domains in production; unknown `Host` → 444. Shared `proxy_params.conf` carries the WebSocket upgrade that Blazor Server and SignalR need; `/media/` proxies straight to MinIO with a 7-day cache header; ACME challenge served from a volume shared with certbot. TLS ships as `tls.conf.example` + `tls_params.conf` (TLS 1.2/1.3, HSTS, OCSP stapling) since no domain or certificate exists here to verify against.)
- [x] GitHub Actions: restore/build/test → build & tag images → push to GHCR; migration step gated in deploy. (`ci.yml`: build+test the server projects and Testcontainers integration suite, build the Android head in a separate job with the `android` workload, then a matrix job pushing `api`/`web`/`admin` to GHCR with buildx cache — only on `master`/tags, never on a PR. Restores per-project rather than via the solution, which would drag in the MAUI head. `deploy.yml`: manual promotion of an existing tag over SSH — pull → `docker compose run --rm migrate` → roll app containers → poll `/health`. **Not executed:** no GitHub runner or VPS in this environment; YAML validated only.)
- [x] Backup script (`pg_dump` + MinIO sync) and runbooks (deploy, restore, gateway onboarding) under `docs/`. (`scripts/backup.sh` → timestamped `postgres.dump` (`-Fc`) + `media/` mirror + `manifest.txt`, with retention pruning and a guard that fails the run if the media mirror did not reach the host; `scripts/restore.sh` stops the app, recreates the database, restores both, and re-applies the bucket policy behind a typed confirmation. Both **executed for real** against the running stack. Runbooks: `docs/runbooks/{deploy,restore,gateway-onboarding}.md`.)
- [x] README with local `docker compose up` quickstart and configuration reference. (Two quickstarts — full stack in containers, and infra-only for `dotnet run` development — plus a deployment/operations section and a `.env` configuration table alongside the existing app-settings one.)

### Phase 12 — Shared storefront shell (header, nav, sidebar, footer)
- [ ] Top contact bar (`TopContactBar.razor`): brand logo far left, then "Call us" + placeholder phone `+998000000000`, "Working hours Mon–Sat 9.00–18.00", and Instagram/Facebook/Telegram icon links far right — all through `IStringLocalizer<SharedResource>` (`Shell.CallUs`, `Shell.WorkingHours`, …) with RU/UZ/EN entries in `SharedResource*.resx`.
- [ ] Main navigation row (`MainNav.razor`): Home, Payment and delivery, Stock, Reviews, Service Center, Contacts, PC Configurator, plus a Telegram-icon "Order on Telegram" link out to the channel; the active item renders in `--brand` red. Routes land in Phases 13–19, so this phase ships links + placeholder pages.
- [ ] Utility row beneath the nav (`HeaderUtilityRow.razor`): cart icon with item badge and a search toggle. (Currency switching is its own Phase 20.)
- [ ] Left category sidebar (`CategorySidebar.razor`) rendering **only the app's real categories** from the live tree (`CatalogApiClient.GetCategoriesAsync` → `/api/v1/catalog/categories`, currently Computers/Laptops/Memory/Accessories/Mice) — no invented pcmarket.uz list. Each row gets a leading category icon, and a sticky red "Go to PC Configurator" button pins to the bottom of the sidebar rail. Note: the reference site's sidebar (Printers and MFPs, All-in-one PCs, Monitors, Projectors, UPS, …) is *catalog data*, not shell markup — matching it is a seeding task, not this phase.
- [ ] Fixed right-edge contact rail (`FloatingContactRail.razor`): stacked Telegram and call buttons in `--brand`, visible on every page and collapsing on narrow viewports.
- [ ] Shared footer (`SiteFooter.razor`): PC MARKET logo, "Online store of computer equipment and components" tagline, "Official partner:" + NVIDIA/ASUS partner logos, a Pages column (Payment and delivery, About the company, Service Center, Reviews, PC Configurator), a two-column Catalog list driven by the real category tree, and a Contacts column with the placeholder phone, the address "Uzbekistan, Tashkent, Yunusabad district, 13th quarter, 2A, Trade Complex 'Lion', Landmark 'Mega Planet'" in accent red, and the social icons. Replaces the current one-line copyright footer.
- [ ] Compose all pieces into the shell so every page inherits them (`MainLayout.razor` + `Components/Layout/Shell/`), styled only from the existing charcoal/red/Play tokens in `app.css` (`--bg`, `--surface`, `--brand`, `--line`, `--muted`) so both themes follow automatically — the reference pages render correctly in light as well as dark.

### Phase 13 — Home page content
- [ ] Full-bleed hero carousel at the top of `Home.razor` (reference slide: a dark "Офис под ключ" promo with headline, sub-copy, and a red accent line).
- [ ] Category circle links — round product images inside a red ring with the category name beneath, driven by the real category tree from Phase 12.
- [ ] "Bestsellers 2026" and "We recommend" product carousels reusing **real catalog products** (`CatalogApiClient.GetProductsAsync`) in a new `ProductCarousel.razor`; each carousel has prev/next arrows and an "N / M" page counter in its top-right, and cards show a multi-image dot indicator, price, and a red "Add to cart" button (extending the existing `ProductCard.razor`).
- [ ] "How we work" three-step section — circular outline icons on a connecting rule: Deciding on the product / Payment / Free shipping, each with a short caption.
- [ ] "Build a computer to suit your taste!" configurator promo — PC hero image, a red "Go to PC Configurator" button, and two secondary links (Assembling a computer with Intel, Build a PC with AMD) pointing at the Phase 19 entry paths.
- [ ] "What they say about us" testimonials carousel (name + city + quote cards, arrows + counter, a "Read all reviews" link to the Phase 18 Telegram channel), then "They trust us" partner logos and "Brands" — both circular-logo carousels with their own counters.
- [ ] All headings, step copy, and testimonial text through `IStringLocalizer<SharedResource>` (RU/UZ/EN); static sections hardcoded to the reference design, themed from `app.css` tokens with no new colors.

### Phase 14 — Payment and delivery page
- [ ] Route `/payment-and-delivery` (`PaymentAndDelivery.razor`) with breadcrumb Home › Payment and delivery and a wide delivery hero image carrying the intro paragraph as overlay text.
- [ ] Large red "0 sum" circle badge overlapping the hero, with "FREE DELIVERY IN TASHKENT" set beneath it in heavy caps.
- [ ] "DELIVERY TO REGIONS OF UZBEKISTAN" terms and an "EXCHANGE OR RETURN" policy section.
- [ ] "ATTENTION!" operator notice as a full-width darker panel (`--surface` over `--bg`), followed by the "DELIVERY OF THE ORDER" terms.
- [ ] "PAYMENT METHODS" block — three circular outline icons with captions: CASH, CASHLESS PAYMENT, PAYMENT BY UZCARD.
- [ ] Every string in `SharedResource*.resx` under a `PaymentDelivery.*` prefix (RU/UZ/EN) — no literal copy in the `.razor`.

### Phase 15 — Contacts page
- [ ] Route `/contacts` (`Contacts.razor`) with breadcrumb Home › About the company and the "NEED COMPUTER EQUIPMENT?" heading.
- [ ] Three large circular contact nodes joined by a horizontal rule: "Our Telegram" (paper-plane icon), the placeholder phone `+998000000000` (phone icon), and "Our mail" (envelope icon) using a **dummy placeholder address** — the reference's `sale@pcmarket.uz` is deliberately *not* reused.
- [ ] About-the-company paragraphs plus the ASUS/NVIDIA authorized-partner certificate image.
- [ ] "We are on the map" — a full-width embedded Google Map `<iframe>` (no API key) pinned to the Yunusabad address from Phase 12's footer.
- [ ] All copy localized under `Contacts.*` (RU/UZ/EN); verify the page in both themes, since the reference renders it light and dark.

### Phase 16 — Service Center page
- [ ] Route `/service-center` (`ServiceCenter.razor`) with breadcrumb Home › Service Center and a two-column service-partner layout.
- [ ] AVTECH column, using muted "Address: / Phone number: / Opening hours:" labels above white values: "Tashkent city, Yakkasaray district, st. Abdullah Kahar, 49A", placeholder phone, Mon–Fri 9:00–18:00 / Sat–Sun Closed.
- [ ] LLC "NG Service" column: "100171, Uzbekistan, Tashkent, Yashnabad district, Korasuv Street (former Lisunov), Bldg. 2", landmark line, working hours 9:00–18:00, days off Saturday–Sunday, the "cash desk and equipment warehouse close at 17:30" note, placeholder phone, and a placeholder service-center website.
- [ ] Shared "Warranty obligations do not apply in the following cases:" list — all 14 exclusion bullets from the reference (physical/thermal damage, misuse, bad installation, missed preventive work, external circumstances, foreign objects, unauthorized repair, transport damage, non-original consumables, out-of-spec voltage, unlicensed software/viruses, damaged or absent warranty seal, missing serial number).
- [ ] **Two** embedded Google Map `<iframe>`s (no API key), one per service center, placed under their respective columns.
- [ ] Everything localized under `ServiceCenter.*` (RU/UZ/EN) and themed from `app.css`.

### Phase 17 — Stock (news & promotions)
- [ ] Stock list route `/stock` (`Stock.razor`) — a red-underlined "Stock" section tab, breadcrumb Home › Stock, then article cards: banner image, red-underlined title, excerpt, and a right-aligned solid red "READ MORE" button.
- [ ] Article detail route `/stock/{slug}` (`StockArticle.razor`) rendering one promotion's banner, title, and body.
- [ ] Seed 2–3 dummy articles so both pages have content (reusing the Phase 6 `CmsBlock` content path if it fits, otherwise a minimal `StockArticle` model).
- [ ] Card labels, "Read more", and dummy copy localized under `Stock.*` (RU/UZ/EN); cards reuse the existing surface/shadow tokens.

### Phase 18 — Reviews
- [ ] Reviews nav entry opens the Telegram reviews channel `https://t.me/otzivPCmarket` ("Отзывы PC Market") in a new tab via `target="_blank" rel="noopener"` — **no in-app reviews page**; the home-page "Read all reviews" link from Phase 13 points at the same URL.
- [ ] Localize the nav label under `Nav.Reviews` (RU/UZ/EN); the destination URL is a config constant, not a translated string.

### Phase 19 — PC Configurator (build-your-own-PC tool)
- [ ] In-app configurator at `/configurator` — **not** a link to `configurator.pcmarket.uz`; every component, price, and image comes from this app's own catalog/API. It renders in its own slim chrome (logo, "Write to us"/"Call us"/"Working hours", a red progress rule under a centered "CONFIGURATOR" title) rather than the Phase 12 storefront shell.
- [ ] Entry screen "Select the type of configurator you need" — three stacked wide cards, image left and caption right, highlighting in red on hover/selection: Build a PC with Intel, Build a PC with AMD, Ready-made assemblies.
- [ ] Left "Menu" column — collapsible accordions: **COMPONENTS** (Motherboard, Processor, Cooling system, OZU, Hard disk drives (HDD), SATA SSD drives, M.2 SSD drive, Video cards, power unit, PC case), **ACCESSORIES** (Monitor, Keyboard Mouse, Headphones, UPS, DVD-RW optical drive, Wi-Fi adapters, Gaming chairs, Gaming tables, GAMPAD gaming joysticks, Gaming steering wheel), and **SOFTWARE** (operating system, Antivirus).
- [ ] Center column — one red-underlined section per category with per-category filter chips ("All" plus: sockets `GEN-12 | LGA-1700`, `GEN-13 | LGA-1700`, `GEN-14 | LGA-1700`, `LGA 1851` for boards/CPUs; Water Cooling / Air Cooling; DDR4 / DDR5; capacity chips for HDD/SATA SSD/M.2; brand chips for video cards, cases, chairs, headphones; wattage chips for PSUs; size chips for monitors; Keyboard + Mouse / Keyboards / Mice).
- [ ] Each component row: selection checkbox, model name, an info (ⓘ) icon, and a **relative price delta in UZS** (`+1,000,000 UZS` / `−395,000 UZS`, blank at the base part); the selected row gains a quantity stepper. Hovering or selecting a row shows a large floating product image beside the list.
- [ ] Right "Your assembly" panel — build-type title with case artwork, the running build grouped by category (`1 x ASUS PRIME H610M-K D4 LGA 1700`), a red "Full specification" link, a live "Total amount: … UZS", a red **Buy** button handing off to the existing cart/checkout (`StoreCart` → `/checkout`), and outline "Copy the configuration link" and "Save as PDF" actions (a shareable build permalink and a PDF export — both new capabilities, scoped in the sub-phase split below).
- [ ] Real reference data exists to model against — motherboards MSI PRO H610M-S/E/G, Gigabyte H610M K/B760M/Z890, ASUS PRIME H610M-K & B760M-K across LGA-1700 and LGA-1851; Intel Core i3/i5/i7/i9 and Core Ultra 5/7/9 processors; ID-Cooling, Deepcool, and MSI air/water coolers.
- [ ] Fully localized (RU/UZ/EN) under `Configurator.*` and themed from the charcoal/red/Play tokens in `app.css`.
- [ ] **Large feature — sub-phases to be designed and split out before implementation**: component + socket/compatibility data model, category/accessory/software menu UI, per-category filter chips, relative-pricing engine, assembly/total panel, shareable configuration link, PDF export, and checkout hand-off.

### Phase 20 — Multi-currency support (UZS / USD / RUB)
- [ ] Currency switcher in the header utility row (UZS / USD / RUB, active option underlined in red), persisted per-visitor like the language cookie.
- [ ] A rate source and conversion layer — decide and document whether rates are static/configured, admin-set, or pulled from an external FX source; catalog prices are stored in one base currency and converted for display.
- [ ] Resolve the open pricing question: the reference shows storefront prices in USD but the configurator totals in UZS — define the single base currency and how each surface converts.
- [ ] Decide whether orders may be placed in a non-base currency or whether the switcher is display-only at checkout (checkout/`StoreCart` implications).
- [ ] All currency labels localized (RU/UZ/EN); amounts formatted per-currency. Open feature — settle the rate source and checkout behavior before implementing.

## Testing Approach
- **Unit tests** (`PcMarket.UnitTests`, xUnit): domain state machine (valid/invalid order transitions),
  Money/slug value objects, payment signature verification, validators, mapping correctness.
- **Integration tests** (`PcMarket.IntegrationTests`, `WebApplicationFactory` + Testcontainers for
  Postgres/Redis/MinIO): catalog/cart/auth/order endpoints end-to-end; **payment webhook idempotency**
  (replay Click Prepare/Complete and Payme JSON-RPC calls, assert single ledger effect); order lifecycle.
- **Manual verification** per Acceptance Criteria: full `docker compose up`, shopper journey through web,
  admin CRUD + status advance, Telegram bot against a test token, MAUI on Android emulator.
- **Edge cases to check**: guest→user cart merge collisions; out-of-stock at checkout; duplicate/late/out-
  of-order payment webhooks; refund path; OTP expiry/retry; concurrent stock decrement; timeout auto-cancel
  racing a late payment; slug uniqueness collisions; large catalog pagination.

## Risks
- **Payment gateway conformance (Click/Payme)** — exact signature, amount, error-code, and idempotency
  rules are unforgiving. *Mitigation*: implement strictly to each spec, cover with sandbox integration
  tests including replays, keep a full raw-payload ledger, feature-flag providers so one can ship first.
- **Order/payment race conditions** — timeout auto-cancel vs. late webhook, concurrent stock changes.
  *Mitigation*: DB transactions + row locking / optimistic concurrency on stock and order state; make all
  state transitions idempotent and guarded by the state machine.
- **Scope size** — the full system is large for one pass. *Mitigation*: phased checklist; phases 0–5 yield a
  working API + web storefront that is independently demoable before bot/mobile/admin.
- **MAUI iOS build/signing** — needs an Apple developer account and Mac/CI runner. *Mitigation*: target
  Android first (per prior decision), keep iOS building but defer store signing; thin UI over shared client.
- **Telegram webhook needs public HTTPS** — hard to test purely locally. *Mitigation*: use a tunnel
  (e.g. ngrok) or the staging VPS for webhook validation; secret-token guard on the endpoint.
- **External provider credentials** (SMS, FCM, gateways) may not be available at build time. *Mitigation*:
  interface-based providers with dev/no-op/stub implementations so flows are testable without live keys.

## Open Questions
- Which specific SMS provider (Eskiz vs. Play Mobile vs. other) and do credentials exist yet? Build proceeds
  behind `ISmsSender` with a stub until decided.
- Are Click/Payme **merchant accounts and sandbox credentials** available, or should providers be built and
  tested against mocked gateway contracts until real keys arrive?
- Uzcard/Humo: confirm they route through Click/Payme (assumed) vs. a direct bank merchant API.
- Primary storefront language for launch (UZ vs. RU) — structure supports i18n but one is shipped first.
- Is an Apple Developer account available for iOS builds, or is Android-only acceptable for the first milestone?

## Changelog
- 2026-07-26: **Phase 9 complete — the project is done.** Containerization: multi-stage `Dockerfile`s for
  Api/Web/Admin (SDK 10.0 build → `aspnet:10.0` runtime, project-files-first restore layer, non-root
  `$APP_UID`) plus a `.dockerignore` keeping `bin`/`obj`, `tests/`, `docs/`, and the MAUI head out of the
  build context. `docker-compose.yml` grew from dev-infra-only into the full stack on an internal bridge
  network with only nginx published, health-gated `depends_on`, and two `tools`-profile services: `migrate`
  and `certbot`. Nginx: host-based routing through env-substituted templates, a shared `proxy_params.conf`
  carrying the WebSocket upgrade, `/media/` proxied straight to MinIO, ACME challenge location, and TLS as a
  documented `tls.conf.example`. CI: `ci.yml` (build + test server projects, Android head in its own job with
  the `android` workload, GHCR image matrix on master/tags only) and `deploy.yml` (manual tag promotion over
  SSH with the gated migration step). Ops: `scripts/backup.sh` / `scripts/restore.sh` and runbooks for
  deploy, restore, and gateway onboarding; README gained a full-stack quickstart, a `.env` reference, and a
  deployment section.
  Backend change: startup migration is now gated by `Database:MigrateOnStartup` (default: Development only)
  and the API accepts `--migrate` to apply migrations and exit, so a rolling deploy runs the schema change as
  its own step instead of having every instance race to migrate as it boots. `Database:SeedDemoCatalog` was
  split out of `IsDevelopment()` for the same reason.
  Verified for real, not just written: all three images build; the stack comes up healthy from an **empty
  volume set** via the documented `cp .env.example .env && docker compose up -d --build`; routing works for
  storefront/admin/api with an unknown `Host` closed (444); a Blazor circuit completes a **101 Switching
  Protocols** handshake through nginx; `/media/` serves an object from MinIO with its cache header; the gated
  path logs "Startup migration is disabled" and `docker compose run --rm migrate` applies cleanly; backup and
  restore were both executed against the live stack (database dropped and restored, media mirrored back,
  Phase-8 order intact); and the full shopper journey — browse → guest cart → register + OTP → merge → COD
  checkout → `Processing` — runs end to end through Nginx. 60 tests still green.
  Discoveries worth carrying forward: (1) **MinIO buckets are private by default** — the app creates the
  bucket on first upload but never sets a policy, so the Nginx `/media/` route 403s until the new
  `minio-init` service runs `mc anonymous set download`; (2) the nginx image only env-substitutes files in
  `/etc/nginx/templates/*.template`, not `conf.d/*.conf`, so host variables must live in templates while
  `$nginx_variables` stay out of them; (3) Git Bash/MSYS rewrites a `-v host:container` argument into a
  mangled spec that silently becomes an anonymous volume — the backup's media mirror reported success while
  writing nowhere, which is why both scripts now export `MSYS_NO_PATHCONV` and verify the mirror landed;
  (4) CI must restore per-project, since restoring the solution drags in the MAUI head and fails without
  mobile workloads. **Not executed here:** the GitHub Actions workflows (no runner) and anything needing a
  real domain, certificate, or VPS — TLS config is provided but unverified.
- 2026-07-24: Initial plan created from architecture.md (greenfield, .NET 10 SDK confirmed).
- 2026-07-24: Phase 0 complete — solution scaffold (13 projects), reference graph, central package
  management, and tooling files in place; non-MAUI projects build clean. Recorded two deviations:
  solution uses the `.slnx` format (.NET 10 default), and the MAUI project is opted out of CPM. NuGet
  audit (NU19xx) demoted from error to warning pending a patched `Microsoft.AspNetCore.OpenApi`.
- 2026-07-24: Phase 1 complete — full domain model (27 files), EF Core persistence with JSONB/FTS/GIN,
  `InitialCreate` migration applied to Postgres 17, Identity-backed users, `DbSeeder` + idempotent
  `CatalogImporter`. 11 tests green (9 domain unit, 2 Testcontainers integration). Decisions: "User" is
  `ApplicationUser : IdentityUser<Guid>` in Infrastructure (domain refs by id only); default PascalCase
  DB naming (no snake_case package) to keep the FTS computed-column SQL stable; added a transitive pin
  for `System.Security.Cryptography.Xml` (audit still flags it, so it remains a demoted warning).
- 2026-07-24: Phase 2 complete — cross-cutting infrastructure and the API host. Added Application
  abstractions (cache/storage/sms/tokens/current-user), Infrastructure implementations (Redis, MinIO,
  no-op SMS, refresh-token store + `AddRefreshTokens` migration), and the `PcMarket.Api` host (Serilog,
  JWT auth, rate limiting, OpenAPI/Scalar, Hangfire, ProblemDetails, correlation id, health checks,
  startup migrate/seed). Smoke-tested end-to-end against Docker Postgres/Redis/MinIO — all `/health`
  checks Healthy. Added `docker-compose.yml` (dev infra), `.env.example`, and `README.md`. Note: JWT
  token service lives in `PcMarket.Api` (uses IdentityModel transitively via JwtBearer) so Infrastructure
  stays free of ASP.NET; `dotnet run` binds the launchSettings port (5055), not `ASPNETCORE_URLS`.
- 2026-07-25: Phase 4 code complete — Orders & Payments. Added order Contracts (DTOs + wire enums mirroring
  the domain enums by value; API now serializes enums as strings via `JsonStringEnumConverter`); Application
  `OrderService` (create-from-cart with stock reservation + name/price/address snapshotting, list, detail,
  cancel-with-stock-restore), `PaymentService` (`/payments/initiate`), and `OrderMaintenanceService`
  (Hangfire timeout auto-cancel + payment reconciliation). Introduced `IPaymentProvider`/
  `IPaymentProviderResolver` in Application and implemented five rails in `PcMarket.Payments`: Cash (COD →
  Processing), Click (Prepare/Complete callbacks, MD5 sign + amount verification, idempotent ledger),
  Payme (full JSON-RPC contract with Basic-auth + Payme error/state codes, tiyin amounts, idempotent by
  Payme transaction id), and Uzcard/Humo riding the Click rail; every rail is feature-flagged. Webhook
  endpoints (`/payments/click/callback`, `/payments/payme/callback`) authenticate by each gateway's own
  scheme (not JWT). Recurring jobs wired in `PaymentJobsExtensions`. Decisions/notes: no schema change
  (Order/PaymentTransaction already existed) — Click `merchant_prepare_id` reuses `click_trans_id` to stay
  columnless; guest→order flow reserves stock at creation and restores it on cancel/timeout; the "admin
  notified" on new orders and domain-event dispatch land in Phase 5 (events are raised but not yet
  dispatched). Added `Phase4PaymentTests` (2 integration tests) covering the full Click and Payme flows
  with duplicate-webhook replays asserting a single settled ledger entry — 19 tests now green. Two test-
  harness fixes surfaced by adding a third `WebApplicationFactory`: recurring Hangfire jobs are now
  registered via the per-host `IRecurringJobManager` (not the static `RecurringJob` facade, which binds to
  the process-global `JobStorage.Current` and breaks with multiple hosts in one process); and the
  integration assembly now disables test parallelization, since `ApiFactory` injects container connection
  strings through process-global env vars.
- 2026-07-25: Phase 6 complete — the admin panel. Backend: an `AuditLogEntry` domain entity (`AddAuditLog`
  migration) and admin API under `/api/v1/admin/*` behind an `AdminPanel` role policy (Admin/Manager) —
  `AdminCatalogService` (category/brand/product/variant/image CRUD; products referenced by orders are
  soft-deactivated), `AdminOrderService` (list/filter, detail, status-advance/refund via the order state
  machine so customers still get their notifications, customer lookup), `AdminDashboardService` (stats), and
  `AdminAuditService`. Customer data reaches Application via a new `IUserDirectory` abstraction
  (`UserDirectory` in Infrastructure) rather than leaking Identity types. `IAuditLogger`/`AuditLogger` record
  each mutation with the actor's phone. Media: `POST /api/v1/admin/media` uploads to MinIO and returns a
  stable URL served by a public read-through `GET /api/v1/media/{key}`. Shared: `AdminApiClient` added to
  `PcMarket.ApiClient` (incl. multipart upload + raw-JSON import helpers on `ApiClientBase`). Frontend:
  `PcMarket.Admin` Blazor Server (global Interactive Server, prerender off; per-circuit `AdminSession` +
  `ProtectedSessionStorage`; `AdminPageBase` auth gate) with login, dashboard (stat cards + a
  `Microsoft.AspNetCore.SignalR.Client` `HubConnection` to `/hubs/admin` for a live new-order feed), product
  list + editor (inline variant price/stock, specs, image URLs + MinIO upload), categories/brands CRUD,
  order list/detail with status-advance/refund, JSON catalog import, and an audit viewer. Verified live in
  Chrome (admin login → dashboard feed "connected" → product management). Deferred: banners/CMS blocks (need
  a new entity + storefront integration). 19 tests still green; the `AddAuditLog` migration applies cleanly
  to the Testcontainers DBs.
- 2026-07-25: Phase 6 finished — banners & CMS blocks. Added `Banner` and `CmsBlock` domain entities
  (`AddContent` migration), `ContentService` (public reads) + `AdminContentService` (audited CRUD), admin
  endpoints under `/api/v1/admin/{banners,cms-blocks}` and public `GET /api/v1/content/banners` +
  `/blocks/{key}`. New `ContentApiClient` (public) + banner/CMS methods on `AdminApiClient`; an admin
  `/content` page manages both. The storefront home now renders active banners as a strip and a `home-intro`
  CMS block above the catalog; demo banners + intro block are seeded in Development. Verified live in Chrome.
  Note: a rebuild was required between `ef migrations add AddContent` and running the API (EF 10's
  pending-model-changes check compares the *compiled* model to the *compiled* snapshot). 19 tests green.
- 2026-07-25: Dev convenience — the demo catalog now seeds automatically in Development. `demo-catalog.json`
  is embedded into `PcMarket.Infrastructure`; `DbSeeder.SeedAsync(seedDemoCatalog)` imports it when the host
  is in Development (the API passes `Environment.IsDevelopment()`), while `SEED_CATALOG_PATH` still overrides
  with an external file in any environment. Import stays idempotent, so it is safe on every startup and tests
  (which pass the flag as false) are unaffected. Also replaced the demo images' unreachable `example.com`
  URLs with reliable `placehold.co` placeholders. 19 tests still green.
- 2026-07-25: Phase 5 complete — notifications, real-time, and the web storefront. Backend: added an
  in-process domain-event pipeline (`IDomainEventDispatcher`/`IDomainEventHandler<T>`; reflection dispatcher
  invoked post-commit from `PcMarketDbContext.SaveChangesAsync`, best-effort so a handler never rolls back a
  save) and a Notifications module — `OrderNotificationHandler` pushes SignalR updates immediately and enqueues
  `NotificationDeliveryService` on Hangfire, which fans out over four dev/stub `INotificationChannel`s
  (Telegram/Push/SMS/Email, log-only) writing a `Notification` ledger row each. SignalR: `OrderStatusHub`
  (per-user group) + `AdminOrderHub`; JWT read from the query string for the hub handshake. Added `UserId` to
  `OrderStatusChangedEvent`. Shared: `PcMarket.ApiClient` — six typed clients over an `ApiClientBase` that
  builds each request with the current access/cart tokens from an `IApiTokenProvider` (chosen over a
  `DelegatingHandler` because pooled HttpClient handlers can't safely read Blazor's circuit-scoped session);
  string-enum JSON to match the API; `ApiException` from ProblemDetails. Web: `PcMarket.Web` Blazor storefront
  (home, `/catalog[/{slug}]` with filters + paging, product detail with variants + add-to-cart, search, cart,
  checkout → create-order → initiate-payment, account with orders/addresses, login/register-with-OTP), custom
  design system in `app.css`, per-circuit `WebSession` persisted to `ProtectedSessionStorage`, guest-cart
  merge on login. Verified live in Chrome against the running API. Decisions/deviations: Web renders global
  Interactive Server with prerender disabled (not static SSR — avoids the SSR/JS-interop + protected-storage
  conflict; SSR-for-SEO, sitemap, canonical/meta are a noted follow-up); notification channels are stubs
  until live Telegram/FCM/SMS/email creds exist; Polly not added — Hangfire auto-retry + a bounded per-channel
  retry loop stand in. Still 19 tests green (order flows now also exercise event dispatch without regression).
- 2026-07-24: Phase 3 complete — Catalog, Cart, Users, and Auth APIs. Added Contracts DTOs, an
  `IApplicationDbContext` abstraction (EF Core added to Application), use-case services (Catalog/Cart/
  Address in Application; Auth in Infrastructure over Identity), FluentValidation + a validation endpoint
  filter, and four minimal-API endpoint groups. Verified with 8 WebApplicationFactory + Testcontainers
  integration tests (all customer flows) plus 9 unit tests — 17 total green. Decisions/notes: hand-written
  mappers instead of Mapster; FTS behind `IProductSearchQuery` in Infrastructure to keep Application
  provider-agnostic; guest carts persisted in Postgres by token (not Redis). Fixed two bugs surfaced by
  tests: required value-type query params (made nullable/optional) and EF client-Guid keys needing
  `ValueGeneratedNever()` to avoid a spurious concurrency exception on insert-then-update.
- 2026-07-25: Phase 7 complete (code) — the Telegram bot. New `PcMarket.Bot` module on `Telegram.Bot` 22.x:
  `TelegramClientAccessor` owns the single bot client and degrades to "disabled" on a missing/invalid token
  so the API boots identically without one; `TelegramUpdateHandler` routes messages, slash commands, and
  inline-button callbacks to five scoped flows (`AccountFlow`, `CatalogFlow`, `CartFlow`, `OrderFlow`,
  `AdminFlow`), all of which call the existing Application use-cases directly — no logic is duplicated from
  the web storefront. Conversation state (linking + the three-step checkout address) lives in Redis via
  `ICacheService` with a 30-minute TTL. Callback payloads carry **ids, never slugs**, and `CallbackData.Of`
  throws if a payload would exceed Telegram's 64-byte `callback_data` limit; this needed one small
  Application addition, `CatalogService.GetProductByIdAsync`. Admin status buttons are generated from a new
  `Order.AllowedFrom(status)` on the domain aggregate, so the bot cannot offer an illegal transition, and the
  transition itself runs through `AdminOrderService` (same history/audit/customer notifications as the panel);
  authorization reads the *linked account's* roles, so forwarding an alert to a non-manager grants nothing.
  Account linking: `ITelegramLinkStore` (Application) over `ApplicationUser.TelegramUserId`
  (`TelegramLinkStore` in Infrastructure, one Telegram id ↔ one account); a known phone gets a bot-issued OTP,
  an unknown phone is registered through `IAuthService` first and verified with that flow's OTP; on success the
  `tg{telegramUserId}` guest cart is merged into the account. API: `POST /api/v1/bot/telegram/webhook`
  (anonymous — Telegram cannot present a JWT — authenticated by a constant-time compare of
  `X-Telegram-Bot-Api-Secret-Token`, 404 when the bot is disabled) plus Admin/Manager-only `set-webhook` and
  `webhook-info`. Notifications: new `ITelegramMessenger` abstraction — Infrastructure registers a no-op and
  `AddBot` (called after `AddInfrastructure`) registers the live one, which is what rewires
  `TelegramNotificationChannel` from a log stub to real delivery into the customer's linked chat; it still
  logs and reports success when there is no token or no link, so those cases don't burn the notification's
  retries. New-order alerts ride the existing domain-event pipeline as a second
  `IDomainEventHandler<OrderPlacedEvent>` alongside the SignalR feed. Docs: README "Telegram bot" section and
  `.env.example` entries. Tests: 17 new unit tests (callback-data round-trip + 64-byte limit, phone
  normalization, admin keyboards mirroring the state machine) — 26 unit tests green; and `Phase7BotTests`,
  two integration tests covering webhook secret-token enforcement and a full browse → link (register + OTP +
  cart merge) → checkout → COD order → refused customer-role admin action → admin advance flow.
  All projects except the MAUI head build clean. Known limitation: an account the bot creates during linking
  gets a random password the customer never learns, so signing that same account in on the web needs a
  password-reset (or OTP-login) flow that does not exist yet.
- 2026-07-26: Phase 8 complete — the .NET MAUI mobile app. Client: `PcMarket.Mobile` rebuilt from the stock
  template into a Shell `TabBar` app (Home/Catalog/Cart/Account) with 12 pages and view models in native XAML +
  `CommunityToolkit.Mvvm`, covering catalog browse/filter/paged search, product detail with variant selection,
  guest cart, phone+OTP registration and login, checkout (saved or inline address, delivery + payment method)
  through to payment initiation, order list/detail with pay-again and cancel, and profile + address CRUD — all
  over the existing `PcMarket.ApiClient`, so no business logic is duplicated. A new
  `shared/PcMarket.Mobile.Core` (net10.0) holds `MobileSession`, `MobileApiTokenProvider`, and `SessionGuard`
  behind an `ISessionStorage` seam, deliberately outside the MAUI head so the unit-test project can reference
  it; the session persists to `SecureStorage`/`Preferences` and is hydrated lazily by the token provider, which
  keeps the keystore read off app start-up while guaranteeing no request goes out unauthenticated.
  Backend: `DeviceToken` (+ `AddDeviceTokens` migration, unique on `Token`), `DeviceTokenService`, idempotent
  `POST`/`DELETE /api/v1/users/me/device-tokens`, an `IPushSender` abstraction with a logging default, and
  `PushNotificationChannel` rewritten from a log stub into a recipient-resolving channel in the shape of
  `TelegramNotificationChannel` (no devices → log and report success, so it never burns retries).
  Tests: +17 unit (`MobileSessionTests`, `SessionGuardTests`) and +5 integration (`Phase8MobileTests`) →
  **60 green (43 + 17)**. Both mobile heads and the full solution build with 0 warnings.
  Verified live on the `pixel_7_-_api_36_0` emulator against the local API + Docker infra: guest browse → add
  to cart → register + OTP → guest cart merged → COD checkout → `ORD-260726-0B4A67C9` in `Processing`, then a
  force-stop restart with the session intact; zero fatal exceptions across the run.
  Decisions/notes for future sessions: (1) **FCM client SDK not referenced** — `Xamarin.Firebase.Messaging`
  125.1.1 constrains AndroidX `Lifecycle 2.9.x`/`Activity 1.10.x` while .NET 10 MAUI resolves `2.11.x`/`1.13.x`,
  so restore fails `NU1608`; taking it would mean suppressing a real version-skew guard for an SDK with no
  Firebase project to verify against, so `IPushTokenSource` ships as a stub (see `mobile_app/push-setup.md`).
  (2) `CommunityToolkit.Mvvm` must be **8.4.2+** and `[ObservableProperty]` must be applied to **partial
  properties**, not fields — the Windows head raises `MVVMTK0045` and this solution treats warnings as errors;
  8.4.0's generator does not emit partial-property implementations at `LangVersion=latest`. (3) `App` must call
  `InitializeComponent()`; omitting it compiles cleanly and crashes at launch with "StaticResource not found",
  which only the emulator run surfaced. (4) The Android emulator reaches the host at `10.0.2.2`, and cleartext
  HTTP needed a debug-only `network_security_config.xml` exception. (5) Integration tests create users via
  `UserManager`/`ITokenService` instead of the auth endpoints, which sit behind a 10-per-window rate limit.
  (6) The demo catalog's `placehold.co` URLs served **SVG**, which Android cannot decode — switched to the
  `.png` form; browsers had been hiding this.
- 2026-07-26: Phase 7 verified. Docker was installed on the dev machine (WSL2 + Docker Desktop), the infra
  stack brought up, and the full suite run for real: **26 unit + 12 integration tests green**, including both
  `Phase7BotTests`. The API also boots clean against the live stack — migrations apply, Hangfire installs its
  SQL objects, and `/health` reports postgres/redis/minio all Healthy. Note for future sessions: an API
  startup failure that looks like a Hangfire *registration* problem is almost always just the infra
  containers not running — `AddHangfire(…UsePostgreSqlStorage(…))` builds its storage during host startup and
  throws if Postgres is unreachable. Still not done: a live run against a real BotFather token over a public
  HTTPS webhook.
