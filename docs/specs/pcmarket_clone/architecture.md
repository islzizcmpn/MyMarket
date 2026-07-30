# Architecture Document: pcmarket_clone

> A C#/.NET clone of [pcmarket.uz](https://pcmarket.uz/) — an Uzbek online PC & electronics
> store — delivered as three coordinated clients (web storefront, cross-platform mobile app,
> and Telegram bot) over a single shared backend.

## Executive Summary

`pcmarket_clone` reproduces the core of a single-seller electronics e-commerce site:
a browsable product catalog with categories/brands/filters, product detail pages,
cart and checkout, order placement and tracking, user accounts, and a back-office admin
for managing inventory, prices, orders, and content. The same capabilities are surfaced
through three front-ends — a **web storefront**, a **.NET MAUI mobile app** (iOS + Android),
and a **Telegram bot** — plus an **admin panel**.

The chosen architecture is a **modular monolith**: one ASP.NET Core backend, internally
partitioned into well-bounded modules (Catalog, Cart, Orders, Payments, Users/Identity,
Notifications, Bot, Admin), exposing a single versioned REST/JSON API. All three clients are
thin consumers of that API and share a common `.Contracts` library of DTOs, plus a `.Domain`
library where it makes sense. This maximizes C# code reuse, keeps the operational surface
small enough for a one-team/small-business launch, and — because the module boundaries are
explicit — preserves a clean path to extract any module into its own service if load ever
demands it.

The system targets **self-hosted Docker on a Linux VPS** with PostgreSQL, Redis, and an
Nginx reverse proxy, keeping recurring cost low while giving full control over the
Uzbekistan-facing payment integrations (**Click, Payme, Uzcard/Humo**, plus **cash on
delivery**). This document specifies the technology stack, component boundaries, data model,
integration contracts (especially the payment gateways and Telegram), security, deployment,
and the key decisions behind them.

## Architectural Approach

**Pattern:** Modular Monolith (single deployable backend, internally modular) with multiple
thin clients over a shared API. Clean/onion layering inside each module
(Domain → Application → Infrastructure → API).

**Core Principle:** *Share the model, not the UI.* Business rules, validation, and data
contracts live once in C# and are reused by every client. Each client owns only its
presentation and interaction concerns. Module boundaries are enforced in code so the monolith
never silently becomes a "big ball of mud," and any module can graduate to a standalone
service without rewriting its callers.

## Technology Stack

### Core Technologies

- **Runtime / Language**: .NET 10 (LTS) / C# 13
  - **Purpose**: Single language across backend, web, mobile, and bot.
  - **Rationale**: LTS gives ~3 years of support — appropriate for a business launch. One
    language means one skill set and true code sharing between all clients.
- **Backend API**: ASP.NET Core 10 Web API (Minimal APIs + Controllers)
  - **Purpose**: The single source of truth; all clients call it.
  - **Rationale**: High-performance, first-class OpenAPI, mature auth, runs great in Linux
    containers.
- **ORM / Data Access**: EF Core 10 with the Npgsql provider
  - **Purpose**: Mapping the domain model to PostgreSQL, migrations, querying.
  - **Rationale**: Productive, migration-driven schema, LINQ; drop to raw SQL/Dapper only for
    hot catalog queries if profiling requires it.
- **Primary Database**: PostgreSQL 17
  - **Purpose**: Transactional store for catalog, orders, users, payments.
  - **Rationale**: Free, excellent full-text search and JSONB (useful for flexible product
    specifications), strong reliability, trivial to run in Docker.
- **Web Storefront**: Blazor Web App (.NET 10) — Static SSR for catalog/product pages,
  Interactive Server for cart/checkout/account.
  - **Purpose**: Customer-facing store.
  - **Rationale**: Server-side rendering gives the SEO and fast first paint an e-commerce
    catalog needs, while interactive islands handle cart/checkout. All in C#, reusing
    `.Contracts` and Razor components that can be shared with the MAUI app.
- **Mobile App**: .NET MAUI 10 (iOS + Android; Windows target optional)
  - **Purpose**: Native mobile storefront + order tracking + push notifications.
  - **Rationale**: One C# codebase for both platforms, native controls/performance, shares
    models and API client with the backend. (Per decision, cross-platform from day one.)
- **Telegram Bot**: `Telegram.Bot` (v22.x) hosted as an ASP.NET Core webhook endpoint inside
  the backend (Bot module).
  - **Purpose**: Browse catalog, search, place/track orders, receive order status pushes, and
    an admin channel for new-order alerts.
  - **Rationale**: Mature, well-maintained C# Telegram client; webhook hosting reuses the same
    process, config, and DB — no separate deployment needed at this scale.

### Supporting Technologies

- **Cache / Sessions / Rate-limit store**: Redis 7
  - **Purpose**: Catalog & config caching, distributed cache for output/response caching,
    Telegram conversation state, rate-limiting counters, SignalR backplane (if scaled out).
  - **Rationale**: Fast, simple, standard; keeps hot catalog reads off PostgreSQL.
- **Real-time**: ASP.NET Core SignalR
  - **Purpose**: Live order-status updates to web/admin; live admin new-order feed.
  - **Rationale**: Native .NET, integrates with the auth already in place.
- **Object / Media Storage**: MinIO (S3-compatible) in Docker, fronted by Nginx/CDN
  - **Purpose**: Product images, banners, invoices/receipts.
  - **Rationale**: Keeps large binaries out of PostgreSQL and off the app containers; S3 API
    means a painless move to managed object storage or a CDN later.
- **Background Jobs**: Hangfire (PostgreSQL storage)
  - **Purpose**: Payment reconciliation, order-timeout handling, notification fan-out,
    scheduled catalog re-indexing, abandoned-cart reminders.
  - **Rationale**: Reliable, DB-backed, has a dashboard; no extra infrastructure required.
- **Auth**: ASP.NET Core Identity + JWT access tokens + rotating refresh tokens
  - **Purpose**: Accounts for web/mobile; role-based admin; service auth for the bot.
  - **Rationale**: Stateless JWT suits mobile and multiple clients; Identity handles password
    hashing, lockout, confirmation flows.
- **Validation**: FluentValidation
  - **Purpose**: One set of validation rules for API inputs, reused by clients.
- **Mapping**: Mapster (or hand-written mappers) between Domain entities and `.Contracts` DTOs.
- **Logging/Tracing/Metrics**: Serilog → Grafana Loki; OpenTelemetry → Tempo; Prometheus +
  Grafana dashboards.
- **API Docs**: OpenAPI (built-in .NET 10 support) + Scalar/Swagger UI.

### Infrastructure

- **Hosting**: Self-hosted Linux VPS (Ubuntu LTS), single node to start.
- **Containerization**: Docker + Docker Compose (all services as containers).
- **Reverse Proxy / TLS**: Nginx with Let's Encrypt (auto-renew via `certbot`/`acme`).
- **CI/CD**: GitHub Actions → build & test → build images → push to registry → SSH deploy /
  `docker compose pull && up -d`.

## System Architecture

### High-Level Architecture

```
                         ┌───────────────────────────────────────────┐
                         │                Clients                     │
                         │                                            │
   ┌──────────────┐      │  ┌────────────┐  ┌───────────┐  ┌───────┐ │
   │  Web browser │◀────▶│  │ Blazor Web │  │ .NET MAUI │  │Telegram│ │
   │  (customers) │      │  │ Storefront │  │  Mobile   │  │  App   │ │
   └──────────────┘      │  └─────┬──────┘  └─────┬─────┘  └───┬────┘ │
                         └────────┼───────────────┼────────────┼──────┘
                                  │ HTTPS/JSON     │ HTTPS/JSON │ webhook
                                  ▼                ▼            ▼
                         ┌──────────────────────────────────────────────┐
                         │                Nginx (TLS, routing)           │
                         └───────────────────────┬──────────────────────┘
                                                 ▼
      ┌───────────────────────────────────────────────────────────────────────┐
      │                 ASP.NET Core Backend (Modular Monolith)                 │
      │                                                                         │
      │  API layer  ──  AuthN/AuthZ (JWT)  ──  Validation  ──  Rate limiting    │
      │  ┌────────┐ ┌──────┐ ┌────────┐ ┌──────────┐ ┌───────┐ ┌────────────┐   │
      │  │Catalog │ │ Cart │ │ Orders │ │ Payments │ │ Users │ │Notification│   │
      │  └────────┘ └──────┘ └────────┘ └──────────┘ └───────┘ └────────────┘   │
      │  ┌────────┐ ┌────────────────────────────────────────────────────┐     │
      │  │  Bot   │ │  Admin (module + API for admin panel)              │     │
      │  └────────┘ └────────────────────────────────────────────────────┘     │
      │        │  Hangfire (background jobs)     │  SignalR hubs               │
      └────────┼─────────────────┬───────────────┼─────────────────────────────┘
               │                 │               │
        ┌──────▼─────┐    ┌──────▼──────┐   ┌─────▼─────┐   ┌──────────────┐
        │ PostgreSQL │    │    Redis    │   │   MinIO   │   │  External:   │
        │ (primary)  │    │ cache/state │   │  media    │   │ Click, Payme,│
        └────────────┘    └─────────────┘   └───────────┘   │ Telegram API,│
                                                            │ SMS/push     │
                                                            └──────────────┘
```

### Solution / Project Layout

```
PcMarket.sln
├─ src/
│  ├─ PcMarket.Domain            // entities, value objects, domain events, enums
│  ├─ PcMarket.Contracts         // DTOs + request/response models (shared by ALL clients)
│  ├─ PcMarket.Application        // use-cases, CQRS handlers, validation, interfaces
│  ├─ PcMarket.Infrastructure     // EF Core, repositories, Redis, MinIO, email/SMS
│  ├─ PcMarket.Payments           // Click, Payme, Uzcard/Humo, Cash providers (IPaymentProvider)
│  ├─ PcMarket.Api                // ASP.NET Core host: API, SignalR, Hangfire, Bot webhook
│  ├─ PcMarket.Web                // Blazor Web App storefront (SSR + interactive)
│  ├─ PcMarket.Admin             // Admin panel (Blazor Server) — can share with Web
│  ├─ PcMarket.Bot                // Telegram update handlers (referenced by Api)
│  └─ PcMarket.Mobile             // .NET MAUI app (iOS/Android)
├─ shared/
│  └─ PcMarket.ApiClient          // typed HttpClient over Contracts, used by Mobile & Bot
└─ tests/
   ├─ PcMarket.UnitTests
   └─ PcMarket.IntegrationTests
```

**Code-reuse map:** `Contracts` is referenced by `Api`, `Web`, `Admin`, `Mobile`, `Bot`, and
`ApiClient`. `Domain` + `Application` + `Infrastructure` are backend-only. `ApiClient` (typed
`HttpClient`) is shared by `Mobile` and `Bot` so the exact same request/response types are used
everywhere. Razor components used in `Web` can be shared into MAUI Blazor views if later desired.

### Component Breakdown

#### Catalog module
**Responsibility:** Categories, brands, products, variants, attributes/specifications
(stored as JSONB for flexibility), stock levels, pricing, search & filtering, media
references.
**Technology:** ASP.NET Core + EF Core; PostgreSQL full-text search (`tsvector`) with Redis
caching of hot listing queries.
**Interfaces:** `GET /api/v1/catalog/categories`, `/products`, `/products/{slug}`,
`/search?q=&filters=…`.
**Dependencies:** PostgreSQL, Redis, MinIO (image URLs).

#### Cart module
**Responsibility:** Cart lifecycle for both authenticated users and guests, line items, price
recalculation, stock validation at add/checkout time.
**Technology:** Persisted in PostgreSQL for logged-in users; guest carts keyed by cart token
in Redis (with TTL) and merged on login.
**Interfaces:** `/api/v1/cart` (GET/POST/PATCH/DELETE), `/cart/merge`.
**Dependencies:** Catalog (price/stock), Redis, PostgreSQL.

#### Orders module
**Responsibility:** Order creation from cart, order states (Created → AwaitingPayment/COD →
Paid → Processing → Shipped → Delivered → Cancelled/Refunded), delivery details, order
history, invoices.
**Technology:** EF Core; domain events raised on state changes; Hangfire for timeouts
(e.g., auto-cancel unpaid orders).
**Interfaces:** `/api/v1/orders`, `/orders/{id}`, `/orders/{id}/cancel`.
**Dependencies:** Cart, Payments, Notifications.

#### Payments module
**Responsibility:** Abstracts every payment method behind a single `IPaymentProvider`
contract; handles gateway callbacks/webhooks, verification, idempotent status updates, and
reconciliation.
**Technology:** Provider implementations for **Click** (Merchant API — Prepare/Complete
callbacks), **Payme** (Merchant API — JSON-RPC `CheckPerformTransaction`,
`CreateTransaction`, `PerformTransaction`, `CancelTransaction`, `CheckTransaction`),
**Uzcard/Humo** (via the Click/Payme rails or a bank merchant API), and **Cash on Delivery**
(no gateway; order marked COD, settled on delivery).
**Interfaces:** `/api/v1/payments/{provider}/callback` (server-to-server webhooks, per each
gateway's spec), `/payments/initiate`.
**Dependencies:** Orders, PostgreSQL (payment/transaction ledger), external gateways.

#### Users / Identity module
**Responsibility:** Registration/login (phone-number-first, common in UZ; email optional),
profiles, addresses, roles (Customer, Admin, Manager), Telegram account linking.
**Technology:** ASP.NET Core Identity + JWT + refresh tokens; SMS OTP for phone verification.
**Interfaces:** `/api/v1/auth/*`, `/api/v1/users/me`, `/users/me/addresses`.
**Dependencies:** PostgreSQL, SMS provider, Notifications.

#### Notifications module
**Responsibility:** Unified outbound messaging — order confirmations, status changes,
payment results — over channels: Telegram, mobile push, SMS, email.
**Technology:** Channel abstraction (`INotificationChannel`); Hangfire for delivery/retry;
Firebase Cloud Messaging for MAUI push; Telegram sendMessage; SMS provider (e.g. Eskiz/
Play Mobile — common in UZ).
**Dependencies:** Users, Orders, external providers.

#### Bot module
**Responsibility:** Telegram interactions — browse categories, search, view product, add to
cart, checkout, track orders, link account; plus an **admin channel** that receives new-order
alerts and lets managers advance order status.
**Technology:** `Telegram.Bot` update handlers invoked from a webhook controller in `Api`;
conversation/session state in Redis; reuses `ApiClient` + Application services directly.
**Interfaces:** `POST /api/v1/bot/telegram/webhook` (secret-token protected).
**Dependencies:** Catalog, Cart, Orders, Users, Redis.

#### Admin panel + module
**Responsibility:** Back-office — CRUD for catalog/pricing/stock/media, order management,
customer lookup, banners/CMS blocks, basic sales dashboards, refunds.
**Technology:** Blazor Server app (`PcMarket.Admin`) behind Admin-role auth, calling the same
Application layer/API. Real-time new-order feed via SignalR.
**Dependencies:** all backend modules (guarded by role-based authorization).

### Data Architecture

#### Data Models (core entities)

```
Category(Id, ParentId?, Name, Slug, SortOrder, IsActive)
Brand(Id, Name, Slug, LogoUrl)
Product(Id, CategoryId, BrandId, Name, Slug, Description,
        Specs JSONB, IsActive, CreatedAt)
ProductVariant(Id, ProductId, Sku, Attributes JSONB, Price, OldPrice?,
               StockQty, IsActive)
ProductImage(Id, ProductId, VariantId?, Url, SortOrder, IsPrimary)

User(Id, Phone, Email?, PasswordHash, FullName, Role, TelegramUserId?, CreatedAt)
Address(Id, UserId, Region, City, Street, Details, IsDefault)

Cart(Id, UserId?, Token?, CreatedAt, UpdatedAt)
CartItem(Id, CartId, ProductVariantId, Qty, UnitPriceSnapshot)

Order(Id, Number, UserId, Status, PaymentMethod, PaymentStatus,
      DeliveryType, AddressSnapshot JSONB, Subtotal, DeliveryFee, Total,
      Currency='UZS', CreatedAt)
OrderItem(Id, OrderId, ProductVariantId, NameSnapshot, UnitPrice, Qty)
OrderStatusHistory(Id, OrderId, FromStatus, ToStatus, ChangedBy, ChangedAt)

PaymentTransaction(Id, OrderId, Provider, ProviderTxnId, State, Amount,
                   RawPayload JSONB, CreatedAt, PerformedAt?, CancelledAt?)

Notification(Id, UserId, Channel, Type, Payload JSONB, Status, SentAt?)
```

**Notes:** Money is stored as integer minor units (UZS tiyin) or `decimal(18,2)` — never
`float`. Product `Specs` and variant `Attributes` use JSONB so the catalog can carry
heterogeneous electronics specs without schema churn. Orders snapshot prices/names/address so
history stays correct even when the catalog changes.

#### Data Storage

- **Relational (PostgreSQL)**: catalog, users, orders, payment ledger, notifications — needs
  transactions, relations, and full-text search.
- **Cache/ephemeral (Redis)**: hot catalog reads, guest carts, bot session state, rate-limit
  counters — needs speed and TTLs, not durability.
- **Object storage (MinIO/S3)**: images, banners, generated invoices — large binaries kept
  out of the DB and app containers.

#### Data Flow (checkout + payment example)

```
Client → POST /orders (from cart)
  → Orders module: validate stock, snapshot prices, create Order (AwaitingPayment)
  → Payments module: initiate with selected provider
      • Cash  → order set to COD/Processing, admin notified
      • Click/Payme → return payment URL / invoice params to client
Gateway → server-to-server callback → /payments/{provider}/callback
  → verify signature/amount, idempotently record PaymentTransaction
  → on success: Order → Paid; raise OrderPaid event
  → Notifications: Telegram + push + SMS to customer; SignalR feed to admin
  → Hangfire: schedule fulfillment tasks / auto-cancel timers cleared
```

## Integration Architecture

### External Integrations

- **Click (Merchant API)**: Server-to-server **Prepare** and **Complete** callbacks;
  verify MD5 sign hashes and amounts; respond with the gateway's required JSON error codes.
  Idempotent handling keyed by Click transaction id.
- **Payme (Merchant API, JSON-RPC over HTTPS)**: Implement `CheckPerformTransaction`,
  `CreateTransaction`, `PerformTransaction`, `CancelTransaction`, `CheckTransaction`,
  `GetStatement`; authenticate via the Payme `Authorization` Basic header (merchant key);
  map to the `PaymentTransaction` ledger with strict idempotency and Payme's error-code
  contract.
- **Uzcard / Humo**: Delivered through the Click/Payme rails (recommended for a small
  business) or a bank's direct merchant API if a bank contract exists; modeled as additional
  `IPaymentProvider` implementations so the rest of the system is unaffected.
- **Cash on Delivery**: No external call; order flagged COD and reconciled on delivery.
- **Telegram Bot API**: Webhook-based; outbound `sendMessage`/`sendPhoto`; inline keyboards
  for catalog navigation and order actions; secret-token header validation on the webhook.
- **SMS (OTP + notifications)**: A UZ SMS provider (e.g. Eskiz / Play Mobile) behind
  `ISmsSender`.
- **Push (mobile)**: Firebase Cloud Messaging behind `IPushSender` for the MAUI app.

### Internal Communication

- **Patterns**: REST/JSON between clients and backend (versioned `/api/v1`); SignalR for
  real-time; in-process domain events + Hangfire for async work inside the monolith.
- **Contracts**: All request/response shapes live in `PcMarket.Contracts` and are compiled
  into every client — no drift between server and client models.
- **Data Formats**: JSON (System.Text.Json); UTC timestamps; money as minor units/decimal.

## Security Architecture

### Authentication & Authorization
- Phone-first accounts with SMS OTP verification; passwords hashed by ASP.NET Core Identity.
- JWT access tokens (short-lived) + rotating refresh tokens (revocable, stored hashed).
- Role-based authorization (Customer/Manager/Admin); admin panel and admin bot actions gated
  by role and, for the bot, by verified Telegram-account linking.
- Payment webhooks authenticated by each gateway's own signature/credential scheme (not JWT).

### Data Security
- **In Transit**: TLS everywhere (Nginx + Let's Encrypt); HSTS; webhook endpoints HTTPS-only.
- **At Rest**: PostgreSQL disk encryption at the VPS level; secrets never in the DB in pl; 
  payment raw payloads stored for audit but PANs/card data never stored (handled by gateways).
- **Secrets Management**: `.env`/Docker secrets injected at runtime, kept out of source
  control; separate secrets per environment; rotate gateway keys via config, no code change.

### Security Boundaries
- Only Nginx is internet-exposed; app, DB, Redis, MinIO sit on an internal Docker network.
- Global rate limiting (ASP.NET Core rate limiter + Redis) on auth, checkout, and bot
  endpoints.
- Strict input validation (FluentValidation) and idempotency keys on payment and order
  creation to prevent double-charge/double-order.
- Admin panel optionally IP-restricted; audit log of admin actions and order status changes.

## Scalability Strategy

### Horizontal Scaling
- The stateless API scales to N containers behind Nginx; JWT (stateless) + Redis-backed
  sessions/SignalR backplane make multi-instance safe.
- Because modules are cleanly bounded, a hot module (e.g. Catalog or Payments) can later be
  extracted into its own independently scaled service with minimal caller changes.

### Vertical Scaling
- PostgreSQL and Redis scale up on the VPS first (CPU/RAM/disk); read replicas can be added
  for catalog-heavy read load before any sharding is considered.

### Performance Optimization
- **Caching**: Redis + response/output caching for catalog listings and product pages;
  Blazor static SSR for cacheable catalog pages; ETag/last-modified on product APIs.
- **Database**: Indexes on slugs, category/brand FKs, order lookups; PostgreSQL FTS index for
  search; JSONB GIN indexes for spec filters; pagination everywhere.
- **Media**: Served from MinIO/Nginx (optionally a CDN later), resized/cached image variants.
- **Load Balancing**: Nginx round-robin across API containers.

### Capacity Planning
- **Expected Load**: Hundreds of orders/month, low-thousands of daily catalog visitors — a
  single well-provisioned VPS comfortably handles this.
- **Growth Path**: Add API replicas → PostgreSQL read replica → extract hot module to its own
  service → managed DB/object storage, in that order, as traffic grows.

## Reliability & Resilience

### High Availability
- **Target**: Best-effort single-node to start; documented path to a second node + shared
  Postgres/Redis for HA when justified.
- **Redundancy**: Multiple API containers on one host removes single-process failure; DB and
  Redis run with persistence enabled.

### Disaster Recovery
- **Backups**: Nightly `pg_dump` + WAL archiving to off-VPS storage; MinIO bucket replication
  or scheduled sync; retention 30 days.
- **Recovery**: Documented restore runbook; `docker compose` brings the full stack up from
  images + restored volumes.

### Error Handling
- **Retry**: Hangfire automatic retries for notifications and reconciliation; Polly for
  transient HTTP failures to gateways/SMS/push.
- **Idempotency**: All payment callbacks and order creation are idempotent (keyed by
  provider txn id / client idempotency key) — safe against duplicate webhooks.
- **Fallbacks**: If push/SMS fails, Telegram/email fallback; if search index is unavailable,
  fall back to basic `ILIKE` query; gateway outage → offer Cash on Delivery.

## Monitoring & Observability

### Logging
- **Strategy**: Structured logs (Serilog) with correlation ids across API/bot/jobs; shipped
  to Grafana Loki; payment and order events logged as audit records.

### Metrics
- **Key Metrics**: Request latency/error rates, checkout success rate, payment
  success/failure by provider, order throughput, queue/job health, DB connections.
- **Tools**: Prometheus + Grafana dashboards; health-check endpoints (`/health`) per
  dependency.

### Tracing
- **Distributed Tracing**: OpenTelemetry across API → DB → gateways → jobs, exported to
  Tempo/Jaeger; each Telegram update and payment callback carries a trace id.

### Alerting
- **Alerts**: Payment failure spikes, webhook signature failures, job backlog, disk/DB
  saturation, TLS-cert expiry → Telegram admin channel + email.

## Deployment Architecture

### Environment Strategy
- **Development**: `docker compose` locally; local PostgreSQL/Redis/MinIO; Telegram test bot;
  gateway sandbox credentials.
- **Staging**: Mirrors production on the VPS (or a second cheap VPS) with sandbox payment
  keys; used to validate gateway callbacks end-to-end.
- **Production**: VPS with production compose stack, real gateway keys, TLS, backups, and
  monitoring.

### Deployment Strategy
- **Approach**: Rolling restart of API containers behind Nginx (build once, promote the same
  image across environments).
- **Pipeline**: GitHub Actions — restore/build/test → build & tag Docker images → push to
  registry (GHCR) → SSH to VPS → `docker compose pull && docker compose up -d` → run EF Core
  migrations as a gated step.
- **Rollback**: Re-deploy the previous image tag; migrations written to be
  backward-compatible (expand/contract) so rollback is safe.

### Configuration Management
- Environment-specific settings via environment variables / Docker secrets; feature flags for
  enabling individual payment providers; no secrets in source control.

## Migration Strategy

### Current State
Greenfield — no existing system to migrate from. `pcmarket.uz` is the *reference* for
features and UX, not a data source.

### Migration Approach
- Not applicable for data. For **catalog seeding**, provide an admin import (CSV/JSON) for
  bulk-loading categories/products/prices/images so the store can be populated quickly.

### Data Migration
- EF Core migrations manage all schema evolution; expand/contract pattern for zero-downtime
  changes; seed data scripts for reference tables (regions, delivery zones).

### Documentation
- API contract published via OpenAPI; module READMEs; runbooks for deploy, backup/restore,
  and gateway onboarding, kept in `docs/`.

## Operational Considerations

### Cost Estimation
- **Infrastructure**: One VPS (app + PostgreSQL + Redis + MinIO + Nginx) — the dominant cost;
  self-hosting keeps recurring spend low per the chosen approach.
- **Scaling Costs**: Add a larger VPS or a second node only when metrics justify it; managed
  DB/object storage optional later.
- **Optimization**: Aggressive caching and image optimization reduce compute/bandwidth.

### Maintenance Requirements
- **Routine**: OS/dependency patching, backup verification, TLS renewal (automated),
  dependency updates on the .NET LTS cadence.
- **Update Strategy**: Stay on .NET LTS; rebuild images on security updates.
- **Technical Debt**: Monolith module boundaries must be respected (enforced via project
  references / architecture tests) to keep the extract-to-service path open.

### Team Requirements
- **Skills**: C#/.NET, ASP.NET Core, EF Core/PostgreSQL, Blazor, .NET MAUI, Docker/Linux,
  Telegram Bot API, UZ payment-gateway integration.
- **Team Size**: Small (1–3 developers) is sufficient for launch given the shared codebase.

## Risks & Mitigations

### Technical Risks
- **Payment-gateway callback correctness (Click/Payme)**: Subtle idempotency/signature/amount
  rules → **Mitigation**: strict per-provider conformance to their API spec, sandbox testing
  in staging, a full transaction ledger, and idempotent handlers.
- **Monolith eroding into a big ball of mud**: **Mitigation**: enforced module boundaries via
  project references and architecture (NetArchTest) tests; per-module ownership of data.
- **MAUI platform quirks (iOS build/signing, push)**: **Mitigation**: cross-platform from day
  one with CI builds per platform; FCM for push; thin UI over shared API client.
- **Single-node VPS as a single point of failure**: **Mitigation**: backups + fast
  rebuild-from-image runbook; documented HA path (second node + shared DB/Redis).

### Operational Risks
- **Gateway/SMS provider outages**: **Mitigation**: Cash-on-delivery fallback; retries;
  alerting; provider abstraction so a provider can be swapped.
- **Self-hosting ops burden**: **Mitigation**: everything in Docker Compose, automated CI/CD,
  monitoring/alerting, and runbooks to keep operations low-touch.

### Business Risks
- **Regulatory/PCI exposure**: **Mitigation**: never store card data; delegate all card
  handling to Click/Payme; store only transaction references.

## Future Considerations

### Planned Enhancements
- Multi-seller/marketplace mode, loyalty/promo codes, reviews & ratings, wishlist,
  recommendation engine, delivery-partner integration, multi-language (UZ/RU/EN) storefront.

### Evolution Path
- Extract hot modules (Catalog, Payments) into independent services behind the same
  contracts; move to managed PostgreSQL/object storage/CDN; add a second node for HA — all
  without changing client code, because clients depend on `.Contracts`, not internals.

### Technology Radar
- Managed Kubernetes if service extraction happens; a dedicated search engine (Meilisearch/
  Elasticsearch) if catalog search outgrows PostgreSQL FTS; a CDN for media at higher traffic.

## Decision Log

### Key Architectural Decisions

1. **Modular monolith over microservices**
   - **Context**: Small business launch, small team, cost-sensitive self-hosting.
   - **Options Considered**: Microservices; modular monolith; classic layered monolith.
   - **Outcome**: Modular monolith with enforced boundaries.
   - **Consequences**: Fast to build/operate and cheap to run; scales vertically first;
     retains a clean extract-to-service path; requires discipline to keep boundaries intact.

2. **Single shared API consumed by all three clients**
   - **Context**: Web + MAUI + Telegram must stay behaviorally consistent.
   - **Options Considered**: Separate backends per client; one shared API + `.Contracts`.
   - **Outcome**: One versioned REST API with a shared contracts/DTO library and typed
     `ApiClient`.
   - **Consequences**: Maximum C# reuse, no model drift; API versioning becomes the
     coordination point for all clients.

3. **.NET MAUI cross-platform for mobile (per stakeholder decision)**
   - **Outcome**: One C# codebase for iOS + Android, sharing models and API client.
   - **Consequences**: Native feel + code reuse; requires per-platform CI and Apple signing.

4. **Blazor Web App (SSR + interactive) for the storefront**
   - **Context**: E-commerce needs SEO/fast first paint plus interactive cart/checkout.
   - **Outcome**: Static SSR for catalog/product pages, interactive server for cart/checkout.
   - **Consequences**: All-C# web with good SEO; interactive parts require a live server
     connection.

5. **Payment provider abstraction (`IPaymentProvider`) covering Click, Payme, Uzcard/Humo,
   Cash**
   - **Outcome**: Each method is a pluggable provider; the rest of the system is
     payment-agnostic.
   - **Consequences**: New rails add without touching Orders/Checkout; per-provider webhook
     conformance is isolated and testable.

6. **PostgreSQL + Redis + MinIO on self-hosted Docker**
   - **Context**: Cost control and full data ownership.
   - **Outcome**: Free, container-friendly stack with an S3-compatible media store.
   - **Consequences**: Low recurring cost, more ops responsibility — offset by Compose,
     CI/CD, backups, and monitoring.

7. **Telegram bot hosted in-process via webhook**
   - **Outcome**: Bot module inside the API using `Telegram.Bot`, state in Redis.
   - **Consequences**: No separate deployment; reuses auth/DB/services; can be split out later
     if needed.

## Appendices

### References
- pcmarket.uz — feature/UX reference: https://pcmarket.uz/
- .NET 10 / ASP.NET Core / EF Core / .NET MAUI documentation (Microsoft Learn)
- Click Merchant API documentation
- Payme Merchant API (JSON-RPC) documentation
- Telegram Bot API documentation; `Telegram.Bot` library

### Glossary
- **COD**: Cash on Delivery — order paid in cash at delivery time.
- **Modular Monolith**: A single deployable app internally divided into independent,
  well-bounded modules.
- **SSR**: Server-Side Rendering — HTML rendered on the server for SEO/first paint.
- **UZS / tiyin**: Uzbek som and its minor unit; money stored as minor units or `decimal`.
- **Idempotency**: Property ensuring repeated identical requests (e.g. duplicate payment
  webhooks) produce the same result without double effects.
