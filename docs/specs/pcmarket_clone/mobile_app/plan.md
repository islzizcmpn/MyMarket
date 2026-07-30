# mobile_app

Phase 8 of [pcmarket_clone](../plan.md) — the .NET MAUI mobile client.

## Status
Complete — last updated 2026-07-26. All six groups delivered and verified on the `pixel_7_-_api_36_0`
emulator against a local API: guest browse → add to cart → register + OTP → cart merged → COD checkout →
order in `Processing`, with the session surviving an app restart and zero runtime exceptions. Both heads
build clean (0 warnings); the suite is **60 tests green — 43 unit + 17 integration** (was 38).
**One deliberate deviation:** the FCM client SDK is not referenced — `Xamarin.Firebase.Messaging` 125.1.1
constrains AndroidX below what .NET 10 MAUI resolves, so restore fails with `NU1608`. The whole
registration pipeline ships behind `IPushTokenSource`; see [push-setup.md](push-setup.md).

## Purpose
Deliver `PcMarket.Mobile`, a .NET MAUI storefront for Android (with the iOS head kept compiling) that
lets a customer browse the catalog, manage a cart, register/sign in by phone + OTP, check out, and
track orders — reusing the existing `PcMarket.ApiClient` and `PcMarket.Contracts` so no business logic
is duplicated. It also closes the last gap in the Phase 5 notifications fan-out by giving the backend a
real device-token registry and an `IPushSender` seam behind the currently log-only `PushNotificationChannel`.

## Scope
**In scope:**
- Replacing the stock MAUI template in `src/PcMarket.Mobile` with a Shell-based app: DI host, navigation,
  design tokens, and the screens listed in Requirements.
- Native XAML + MVVM (`CommunityToolkit.Mvvm`) view models over the typed API clients — **not** Blazor Hybrid.
- Secure session storage: access/refresh tokens in `SecureStorage`, guest cart token in `Preferences`,
  a `MobileApiTokenProvider : IApiTokenProvider`, and refresh-on-401.
- Guest browsing + guest cart, with merge-on-login (the existing `POST /api/v1/cart/merge`).
- Checkout: address (saved or inline) + payment method → `POST /api/v1/orders` → `POST /api/v1/payments/initiate`,
  opening online payment URLs in the system browser.
- Backend push plumbing: `DeviceToken` entity + migration, `POST /api/v1/users/device-tokens` (+ delete),
  `IPushSender` abstraction, and `PushNotificationChannel` rewired to it; a log-only FCM sender behind a flag.
- Client-side FCM registration and foreground/background notification handling on Android.
- Verification: build + deploy to the `pixel_7_-_api_36_0` emulator against a locally running API and walk
  the full shopper journey; document the iOS build steps.

**Out of scope:**
- A live Firebase project, `google-services.json` with real credentials, or a real FCM HTTP v1 send.
  The sender is stubbed and feature-flagged; swapping it in is a config + one-class change.
- iOS signing, provisioning, device deploy, and App Store submission (no Mac/Apple account available).
  The iOS head must keep compiling; it is not run.
- Windows/MacCatalyst heads as supported targets (they stay in `TargetFrameworks` but are not exercised).
- Admin functionality on mobile, Telegram linking from the app, offline mode, deep links, and
  localization/multi-language (parent plan defers i18n).
- Automated UI tests (Appium/MAUI UITest) — verification is a manual emulator walkthrough.

## Requirements
1. `src/PcMarket.Mobile` builds clean for `net10.0-android` **and** `net10.0-ios` with the solution's
   warnings-as-errors settings, and the stock template artifacts (`MainPage.xaml`, `dotnet_bot.png`) are gone.
2. `MauiProgram.CreateMauiApp` registers `AddPcMarketApiClient(apiRoot)`, a singleton `MobileSession`,
   `IApiTokenProvider` → `MobileApiTokenProvider`, and every page + view model in DI; the API root is
   configurable per build configuration and defaults to `http://10.0.2.2:5055` on Android Debug.
3. Navigation is an `AppShell` `TabBar` with Home, Catalog, Cart, and Account tabs; product detail,
   checkout, and order detail are pushed routes registered via `Routing.RegisterRoute`.
4. **Home** shows new arrivals and the category tree, both from `CatalogApiClient`.
5. **Catalog** lists products with category/brand/price filters, `ProductSort` sorting, and incremental
   paging over `PagedResult<ProductListItemDto>`; a search entry runs `CatalogApiClient.SearchAsync`.
6. **Product detail** renders images, specs, and a variant selector from `ProductDetailDto`, and adds the
   selected `ProductVariantId` to the cart, surfacing out-of-stock/API errors inline.
7. **Cart** lists items with quantity update and remove, shows subtotal and total quantity, and works for
   guests: the `X-Cart-Token` returned in `CartDto.Token` is persisted to `Preferences` and replayed.
8. **Auth**: phone + password registration → OTP verification screen → tokens; phone + password login;
   sign-out revokes the refresh token via `AuthApiClient.LogoutAsync`. On successful auth the stored guest
   cart token is merged via `CartApiClient.MergeAsync` and then cleared.
9. Access/refresh tokens and their expiries persist across app restarts in `SecureStorage`; the session
   proactively refreshes an expired access token before a request and retries once on a 401 `ApiException`.
10. **Checkout** collects a saved `AddressDto` or an inline `ShippingAddressDto`, a `DeliveryType`, and a
    `PaymentMethod`, creates the order, then initiates payment: `RequiresRedirect` opens `PaymentUrl` in the
    system browser; COD lands straight on the order detail screen.
11. **Orders**: list from `OrdersApiClient.ListAsync`, detail with status history, pay-again for
    `AwaitingPayment`, and cancel where allowed.
12. **Profile**: view `UserProfileDto`, and full address CRUD via `UsersApiClient`.
13. A `DeviceToken` entity (UserId, Token, Platform, CreatedAt, LastSeenAt; unique on Token) is persisted,
    registered by the app on login/startup via `POST /api/v1/users/device-tokens`, and removed on sign-out.
14. `IPushSender` exists in `PcMarket.Application/Abstractions/Messaging`; `PushNotificationChannel` resolves
    the user's device tokens and delegates to it. The shipped `LoggingPushSender` logs and reports success,
    gated by a `Push:Enabled` flag, so a missing Firebase account never fails a notification or burns retries.
15. On Android the app requests `POST_NOTIFICATIONS` (API 33+), obtains an FCM registration token, and
    displays incoming order-status notifications; absent `google-services.json` the app must still run,
    logging that push is unavailable rather than crashing.
16. The app runs on the `pixel_7_-_api_36_0` emulator against a local API and completes: browse → add to
    cart as guest → register + OTP → cart merges → checkout (COD) → order appears with status `Processing`.

## Acceptance Criteria
- `dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android` and `-f net10.0-ios` both
  succeed with 0 warnings and 0 errors.
- `dotnet build PcMarket.slnx` and the full `dotnet test` suite (currently 26 unit + 12 integration) stay green,
  plus new unit tests for the mobile session/token provider and the device-token endpoint.
- On the `pixel_7_-_api_36_0` emulator against a locally running API: the home screen renders seeded catalog
  data; a product can be opened and a variant added to the cart while signed out.
- Killing and relaunching the app after signing in leaves the user signed in (tokens survive in `SecureStorage`).
- A guest cart with items, followed by registration + OTP verification, results in those items appearing in
  the authenticated cart (merge verified, guest token cleared).
- A COD checkout produces an order visible in the order list with status `Processing`; its detail screen
  shows the status history from `OrderStatusHistoryDto`.
- An online (Click) checkout opens the gateway URL in the system browser and the order sits in `AwaitingPayment`.
- `POST /api/v1/users/device-tokens` with a bearer token stores exactly one row per token (repeat calls update
  `LastSeenAt`, never duplicate); `DELETE` removes it. Covered by an integration test.
- Advancing an order's status from the admin panel logs a `[Push] → user {id}` line per registered device token
  for that user, proving `PushNotificationChannel` now resolves real recipients.
- `docs/specs/pcmarket_clone/mobile_app/ios-build.md` documents the iOS build/run/signing steps.

## Design Approach
**Client.** Keep the app a thin, stateless view over the API — every rule (stock, pricing, order state) already
lives server-side and is reached through `PcMarket.ApiClient`. Structure mirrors the existing `PcMarket.Web`
session model, adapted from per-circuit to per-app scope:

```
src/PcMarket.Mobile/
  MauiProgram.cs                 DI host: API clients, session, pages, view models
  AppShell.xaml                  TabBar (Home/Catalog/Cart/Account) + RegisterRoute for pushed pages
  Services/
    MobileSession.cs             tokens + cart token + UserId/Roles, Changed event   (cf. Web/Services/WebSession.cs)
    SecureSessionStore.cs        SecureStorage + Preferences persistence             (cf. Web/Services/SessionStore.cs)
    MobileApiTokenProvider.cs    IApiTokenProvider over MobileSession                (cf. Web/Services/WebApiTokenProvider.cs)
    SessionGuard.cs              proactive refresh + single 401 retry
    Format.cs                    UZS/date formatting                                 (cf. Web/Services/Format.cs)
    PushRegistrar.cs             FCM token retrieval + register/unregister
  ViewModels/                    ObservableObject + RelayCommand per screen
  Views/                         one XAML page per view model
  Resources/Styles/              Colors.xaml + Styles.xaml aligned to the storefront palette
```

`ApiClientBase` already pulls the access and cart tokens from `IApiTokenProvider` on every request, so the
mobile side needs no `DelegatingHandler` — registering a singleton provider is the whole integration. Region
entry is a free-text field, matching `PcMarket.Web/Components/Pages/Checkout.razor`, so `PcMarket.Domain`'s
`UzbekistanRegions` stays out of the client's reference graph.

**Backend.** Push is the only server change. `DeviceToken` joins the domain beside `Notification`, gets a
`DbSet` on `IApplicationDbContext`/`PcMarketDbContext` and an `AddDeviceTokens` migration; `UserEndpoints.cs`
gains authenticated register/delete routes; `IPushSender` lands next to `ITelegramMessenger` in
`Application/Abstractions/Messaging`. `PushNotificationChannel` stops inheriting `LoggingNotificationChannel`
and instead follows the exact shape of `TelegramNotificationChannel`: resolve the recipient's tokens, delegate,
and return `true` when there is nothing to send to. This mirrors the Phase 7 `ITelegramMessenger` seam — the
no-op is the default registration and a live sender replaces it later without touching callers.

**Notable decisions.** Native XAML + MVVM over Blazor Hybrid (chosen with the requester): the Web Razor pages
are bound to Interactive Server + `ProtectedSessionStorage` and would need extraction into an RCL first, which
is more churn than writing the screens. `CommunityToolkit.Mvvm` is pinned inline in the `.csproj` because
`PcMarket.Mobile` is opted out of central package management (`ManagePackageVersionsCentrally=false`, a Phase 0
deviation for `$(MauiVersion)`).

## Codebase Research
- **`src/PcMarket.Mobile` is still the stock .NET MAUI template** — `MainPage.xaml` with the counter button,
  `dotnet_bot.png`, and a `MauiProgram.cs` that only configures fonts and debug logging. No DI, no navigation
  beyond `AppShell`, no API usage.
- **It already references what it needs**: `shared/PcMarket.ApiClient` and `src/PcMarket.Contracts` project
  references are wired (Phase 0). `TargetFrameworks` covers android/ios/maccatalyst/windows,
  `SupportedOSPlatformVersion` android = 21.0, ios = 15.0.
- **The Android head builds clean today** — verified 2026-07-26: `dotnet build -f net10.0-android` →
  *Build succeeded, 0 Warning(s), 0 Error(s)* (~90 s). Workloads `android`, `ios`, `maccatalyst`,
  `maui-windows` are installed (manifest 10.0.400).
- **Emulator available**: Android SDK at `C:\Program Files (x86)\Android\android-sdk`; AVDs
  `pixel_7_-_api_36_0` and `phone_xh-dpi_4_7in_-_api_30_0`. None currently booted (`adb devices` empty).
- **`PcMarket.ApiClient` is complete for every screen this phase needs**: `CatalogApiClient`
  (categories/brands/products/product-by-slug/search), `CartApiClient` (get/add/update/remove/**merge**),
  `AuthApiClient` (register/verify-otp/login/refresh/logout), `OrdersApiClient` (create/list/get/cancel),
  `PaymentsApiClient.InitiateAsync`, `UsersApiClient` (profile + address CRUD). Nothing new is required
  except a device-token call.
- **`IApiTokenProvider`** (`shared/PcMarket.ApiClient/IApiTokenProvider.cs`) is the single integration seam —
  `GetAccessTokenAsync` / `GetCartTokenAsync`. `PcMarket.Web/Services/WebApiTokenProvider.cs` is a 13-line
  reference implementation to copy.
- **`AddPcMarketApiClient(apiRootUrl)`** appends `/api/v1/` itself and registers each client through
  `AddHttpClient`, so `IHttpClientFactory` must be available in the MAUI container (it comes transitively
  from the ApiClient project).
- **DTO shapes are fixed and string-serialized for enums**: `ProductDetailDto` carries `Variants`/`Specs`/
  `Images`; `CartDto.Token` is how the guest token comes back; `AuthResponse` carries both tokens with
  expiries plus `UserId`/`Roles`; `CreateOrderRequest` takes `AddressId` **or** an inline `ShippingAddressDto`;
  `PaymentInitiationResponse` has `RequiresRedirect` + `PaymentUrl`.
- **Push has no backend at all.** `src/PcMarket.Infrastructure/Notifications/NotificationChannels.cs:59`
  is a five-line `PushNotificationChannel : LoggingNotificationChannel`; there is no `IPushSender`, no
  `DeviceToken` entity, and no `Push` DbSet. `NotificationSettings.Push` is a bool flag defaulting to `true`.
  `TelegramNotificationChannel` in the same file is the pattern to follow for a recipient-resolving channel.
- **Existing migrations** end at `20260725132703_AddContent`; a new one appends cleanly. Note from the Phase 6
  changelog: EF 10 compares the *compiled* model to the *compiled* snapshot, so rebuild between
  `ef migrations add` and running the API.
- **`IApplicationDbContext`** exposes 16 `DbSet`s; new entities are added there and configured with a per-entity
  `IEntityTypeConfiguration` under `src/PcMarket.Infrastructure/Persistence`.
- **Endpoint style** is minimal APIs grouped per module in `src/PcMarket.Api/Endpoints/*Endpoints.cs`
  (`UserEndpoints.cs` is the one to extend), with `ValidationFilter<T>` for FluentValidation.

## Implementation Checklist

### 8a — App shell, DI, session
- [x] Strip the template: delete `MainPage.xaml(.cs)` and `Resources/Images/dotnet_bot.png`, drop the
      `MauiImage Update` line for it from the `.csproj`. _(Req 1)_
- [x] Add `CommunityToolkit.Mvvm` (pinned inline — project is CPM-opted-out) and, on Android only,
      the FCM package used in 8e. Note: **8.4.2, not 8.4.0** — the Windows head turns `MVVMTK0045` into an
      error under warnings-as-errors, and only 8.4.1+ generates implementations for `[ObservableProperty]`
      partial properties at `LangVersion=latest`. The FCM package was **not** added; see 8e. _(Req 1)_
- [x] `MobileSession` + storage: tokens/expiries in `SecureStorage`, cart token in `Preferences`,
      `Changed` event for badge/tab state. Landed in the new `shared/PcMarket.Mobile.Core` (platform-neutral,
      so the net10.0 test project can reference it) behind `ISessionStorage`, with `MauiSessionStorage`
      binding it to the platform. _(Req 9)_
- [x] `MobileApiTokenProvider` implementing `IApiTokenProvider` over `MobileSession`. It also hydrates the
      session on first use, which keeps the keystore read off the start-up path without any request going
      out unauthenticated. _(Req 2)_
- [x] `SessionGuard`: refresh when `AccessTokenExpiresAt` has passed, retry once on 401 `ApiException`,
      sign out when the refresh itself fails. Refreshes are serialised and keyed on the failing token so
      concurrent screens rotate once instead of revoking each other. _(Req 9)_
- [x] Rewrite `MauiProgram.cs`: `AddPcMarketApiClient(apiRoot)` with the Android-Debug default
      `http://10.0.2.2:5055`, singleton session/guard/cart/auth-flow, transient pages + view models. _(Req 2)_
- [x] `AppShell.xaml`: `TabBar` (Home/Catalog/Cart/Account) + `Routing.RegisterRoute` for the eight pushed
      routes; tab content is assigned from code-behind so the pages come from DI. `AppStyles.xaml` adds the
      storefront look on top of the template dictionaries. _(Req 3)_
- [x] Allow cleartext HTTP to `10.0.2.2` in Debug (`AndroidManifest.xml` +
      `Platforms/Android/Resources/xml/network_security_config.xml`). _(Req 16)_

### 8b — Catalog & cart
- [x] `HomeViewModel` + `HomePage.xaml`: new arrivals + category tree + search entry. _(Req 4)_
- [x] `CatalogViewModel` + `CatalogPage.xaml`: filters (category/brand/price), `ProductSort`, incremental
      paging via `RemainingItemsThreshold`, and free-text search over `SearchAsync`. _(Req 5)_
- [x] `ProductViewModel` + `ProductPage.xaml`: image, specs table, variant picker, add-to-cart with
      inline error/out-of-stock handling. _(Req 6)_
- [x] `CartViewModel` + `CartPage.xaml`: qty update, remove, subtotal; `StoreCart` persists `CartDto.Token`
      for guests so an anonymous cart survives restarts. _(Req 7)_

### 8c — Auth, checkout, orders, profile
- [x] `LoginPage` / `RegisterPage` / `OtpPage` + view models over `AuthApiClient`; `AuthFlow` centralises
      what happens on sign-in — store the session, merge the guest cart, register for push — so no screen
      can get it subtly different. _(Req 8)_
- [x] `CheckoutViewModel` + `CheckoutPage.xaml`: saved-address picker or inline address (free-text region,
      matching the storefront), delivery type, payment method → `OrdersApiClient.CreateAsync`. _(Req 10)_
- [x] Payment step: `PaymentsApiClient.InitiateAsync`; `RequiresRedirect` → `Browser.Default.OpenAsync`,
      otherwise straight to order detail. _(Req 10)_
- [x] `OrdersViewModel` + `OrdersPage.xaml` and `OrderDetailViewModel` + `OrderDetailPage.xaml`: history
      timeline, pay-again for `AwaitingPayment`, cancel where the state machine allows it. _(Req 11)_
- [x] Profile + `AddressesPage`: `UserProfileDto` plus address create/update/delete/default. _(Req 12)_
- [x] Sign-out: `LogoutAsync`, clear `SecureStorage`, unregister the device token; local sign-out happens
      even if the server-side revoke fails. _(Req 8, 13)_

### 8d — Backend device tokens & push seam
- [x] `PcMarket.Domain/Notifications/DeviceToken.cs` + `DeviceTokenConfiguration` (unique index on `Token`),
      `DbSet` on `IApplicationDbContext` + `PcMarketDbContext`, `AddDeviceTokens` migration. _(Req 13)_
- [x] Contracts: `RegisterDeviceTokenRequest(string Token, DevicePlatform Platform)` +
      `RegisterDeviceTokenRequestValidator`. _(Req 13)_
- [x] `POST`/`DELETE /api/v1/users/me/device-tokens` in `UserEndpoints.cs` (auth-required; upsert by token,
      refreshing `LastSeenAt` and reassigning ownership rather than duplicating). _(Req 13)_
- [x] `IPushSender` in `Application/Abstractions/Messaging`; `LoggingPushSender` registered by default,
      gated by the `Notifications:Push` flag. _(Req 14)_
- [x] Rewrite `PushNotificationChannel` to resolve the recipient's device tokens and delegate to
      `IPushSender`, returning `true` when the user has none (mirrors `TelegramNotificationChannel`). _(Req 14)_
- [x] Device-token methods on `UsersApiClient` so the app can register/unregister. _(Req 13)_

### 8e — FCM client registration
- [x] Android: `POST_NOTIFICATIONS` declared in the manifest (API 33+). _(Req 15)_
- [x] `Services/PushRegistrar.cs`: fetch the token, register after login and on start-up when
      authenticated, unregister on sign-out; every failure swallowed so push can never block signing in. _(Req 15)_
- [ ] **Not done — blocked.** Live FCM token retrieval and foreground/background notification display.
      `Xamarin.Firebase.Messaging` 125.1.1 pins AndroidX `Lifecycle 2.9.x`/`Activity 1.10.x` while .NET 10
      MAUI resolves `2.11.x`/`1.13.x`; restore fails with `NU1608`, and taking the dependency means
      suppressing a real version-skew guard for an SDK that cannot be verified here (no Firebase project).
      `PushTokenSource` returns null and logs "push unavailable", which the app handles. Everything
      downstream is real and tested — enabling push is this one method plus a `google-services.json`.
      Documented in [push-setup.md](push-setup.md). _(Req 15)_

### 8f — Build, verify, document
- [x] Unit tests: `MobileSessionTests` (persistence across restart, guest-token lifecycle, corrupt/unreadable
      keystore → signed out, expiry leeway, concurrent hydration) and `SessionGuardTests` (proactive refresh,
      401 retry, single refresh under concurrency, sign-out on rejected refresh, no retry for non-auth
      failures) — 17 new tests, **43 unit total**.
- [x] Integration test `Phase8MobileTests`: auth required, register-twice → one row, token reassigned across
      accounts, delete + repeat, and push fan-out to every device — 5 new tests, **17 integration total**.
- [x] `dotnet build -f net10.0-android` and `-f net10.0-ios` clean (0 warnings); full solution and
      `dotnet test` green. _(Req 1)_
- [x] Booted `pixel_7_-_api_36_0`, deployed, and walked the full acceptance journey against the local API +
      Docker infra. Order `ORD-260726-0B4A67C9` landed in `Processing`; session survived a force-stop
      restart; zero `FATAL EXCEPTION` in logcat across the session. _(Req 16)_
- [x] Wrote [ios-build.md](ios-build.md) (build steps, Mac/pair-to-Mac, signing, the ATS exception the
      simulator will need, what is deferred) and [push-setup.md](push-setup.md).
- [x] Added a "Mobile app" section to `README.md` and marked Phase 8 complete in `../plan.md`.

## Testing Approach
- **Unit** (`tests/PcMarket.UnitTests`): token expiry → refresh decision; `MobileApiTokenProvider` returns the
  current access/cart tokens; guest-token clearing after merge; cart total/badge computation. MAUI's
  `SecureStorage`/`Preferences` are static platform APIs, so session persistence goes behind an
  `ISessionStorage` interface with an in-memory fake for tests.
- **Integration** (`tests/PcMarket.IntegrationTests`, Testcontainers): device-token register is idempotent
  (same token twice → one row, `LastSeenAt` updated), delete removes it, and both require auth (401 anonymous).
- **Manual on the emulator** (the substantive verification, per Req 16 and the acceptance criteria): guest
  browse → add to cart → register + OTP → merge → COD checkout → order status; then relaunch to confirm the
  session survived; then an online (Click) checkout to confirm the browser hand-off.
- **Edge cases to check**: expired access token on a cold start; refresh token rejected (must sign out cleanly,
  not loop); adding an out-of-stock variant; empty cart at checkout; API unreachable (emulator points at
  `10.0.2.2`, not `localhost`) must surface a readable error, not a crash; back-navigation out of the OTP
  screen mid-registration; `google-services.json` missing; notification permission denied.

## Risks
- **Emulator ↔ local API networking** — `localhost` inside the emulator is the emulator; the API must be
  reached at `10.0.2.2:5055` and Android blocks cleartext HTTP by default. *Mitigation*: 8a checklist items
  set both the base URL and a Debug network-security-config; this is the most likely first-run failure.
- **No Firebase project** — FCM cannot be verified end-to-end here. *Mitigation*: the sender is an interface
  with a logging default behind a flag, exactly like Phase 7's `ITelegramMessenger`; the app degrades to a
  warning when `google-services.json` is missing, so nothing else is blocked.
- **iOS head can regress silently** — it is compiled but never run, and MAUI platform code often diverges.
  *Mitigation*: build both TFMs in 8f and keep platform-specific code confined to `Platforms/Android`.
- **Warnings-as-errors vs. MAUI/XAML source generation** — `Directory.Build.props` treats warnings as errors
  and the project uses `MauiXamlInflator=SourceGen`; generated-code or binding warnings could block the build.
  *Mitigation*: the Android head builds at 0 warnings today, so any new warning is from code written here;
  fix rather than suppress.
- **Token refresh races** — several view models can fire requests concurrently and each hit a 401.
  *Mitigation*: serialize refresh in `SessionGuard` behind a `SemaphoreSlim` so only one refresh is in flight.
- **Password-less accounts from the bot** — Phase 7 noted that accounts the bot creates get a random password
  the customer never learns, so those users cannot log into the mobile app. *Mitigation*: out of scope here;
  recorded in Open Questions as the OTP-login/password-reset gap it already is.

## Open Questions
- Should the app support OTP-only login (no password), which would also fix the Phase 7 bot-created-account
  gap? Not required for this phase; it would need a new `/auth` endpoint.
- Which Firebase project/package id will production use? The `ApplicationId` is still the template's
  `com.companyname.pcmarket.mobile` and should be settled before any real FCM or store work.
- Is the api-30 AVD worth a compatibility pass, or is api-36 sufficient for this milestone?
- Should push notifications deep-link into a specific order, or just open the app? Assumed: open order detail.

## Changelog
- 2026-07-26: **Phase complete.** Delivered the MAUI app (12 pages/view models over `PcMarket.ApiClient`),
  a new `shared/PcMarket.Mobile.Core` holding the session/token/refresh logic so it is testable off-device,
  and the backend push registry (`DeviceToken` + `AddDeviceTokens` migration, `/users/me/device-tokens`,
  `IPushSender`, a recipient-resolving `PushNotificationChannel`). 22 new tests → 60 green (43 unit +
  17 integration). Verified live on the `pixel_7_-_api_36_0` emulator: guest browse → cart → register +
  OTP → merge → COD checkout → `Processing`, surviving an app restart.
  Deviations and discoveries worth carrying forward: (1) **FCM client SDK not referenced** — its AndroidX
  constraints conflict with .NET 10 MAUI (`NU1608`), so `IPushTokenSource` ships as a documented stub while
  everything downstream is real; (2) `CommunityToolkit.Mvvm` had to go to **8.4.2** and all
  `[ObservableProperty]` fields became **partial properties**, because the Windows head raises `MVVMTK0045`
  and warnings are errors here; (3) `App` must call `InitializeComponent()` — dropping it compiles fine and
  then crashes at launch with "StaticResource not found", which is exactly what the emulator run caught;
  (4) integration tests mint users via `UserManager`/`ITokenService` rather than the auth endpoints, which
  sit behind a 10-per-window rate limit; (5) the demo catalog's `placehold.co` URLs served **SVG**, which
  Android cannot decode — switched to the `.png` form (browsers hid this, mobile did not).
- 2026-07-26: Initial plan created for Phase 8 (MAUI mobile app) of the pcmarket_clone plan. Decisions taken
  with the requester: native XAML + MVVM (not Blazor Hybrid); full backend push plumbing with a stubbed,
  feature-flagged FCM sender; verification by an Android emulator run against the local API, with iOS
  documented only. Verified during research: the Android head already builds clean (0 warnings), the required
  workloads and two AVDs are present, `PcMarket.ApiClient` covers every screen, and no push infrastructure
  exists server-side.
