# Requirements Document

## Introduction

`PcMarket.Mobile` shipped in Phase 8 with `Resources/Styles/` described as "aligned to the storefront
palette". That alignment no longer holds. The storefront was subsequently rebuilt on the premium
charcoal/red design system (documented in [../plan.md](../plan.md) as a layer over Phases 11–15), and
the mobile app never followed. It is still on the stock .NET MAUI template: `Primary #512BD4` (purple)
in `Resources/Styles/Colors.xaml`, a `#1F6FEB` blue accent in `AppStyles.xaml`, OpenSans type, and the
434-line untouched template `Styles.xaml`. Verified on a Redmi Note 11 on 2026-08-15: white page,
blue prices, unstyled list rows — a different product to look at than the web storefront.

This spec covers closing that gap: porting the web's token system to XAML, bundling the brand
typeface, restyling all 18 XAML files, reproducing the motion system, and serving the storefront's
decorative artwork to the app over HTTP.

**Scope decisions taken with the requester (2026-08-15):**
- **Both themes, dark default** — mirror the web, which defaults to dark and offers a toggle.
- **All 18 XAML files** — no screen is left looking half-migrated.
- **Full motion parity attempted** — card lift, scroll reveals and accent glows, subject to the
  performance gate in Requirement 6.
- **Artwork over HTTP, font bundled** — the requester's constraint is APK size. The two Play `.ttf`
  files are ~60 KB total and must be bundled (MAUI cannot use webfonts); the 36 photographic assets
  under `PcMarket.Web/wwwroot/images/` are fetched at runtime instead of shipped.

**Out of scope** (separate planned slices): checkout location sharing, RU/UZ/EN localization, and the
six web-only screens (Configurator, Stock, Service Center, Contacts, Payment & Delivery, Search).
No API, contract, or backend change is in scope — this slice is client-side presentation only.

**Assumption:** every string stays hardcoded English. The restyle must not make the later
localization slice harder, which in practice means no new literal copy baked into styles or
converters, and no layout that assumes English string lengths.

---

## Requirements

### Requirement 1 — Design token system

**User Story:** As a developer, I want the web's design tokens expressed once as XAML resources so
that the mobile app's look is retuned from one place, exactly as `app.css` does for the web.

#### Acceptance Criteria

1. WHEN `Resources/Styles/Colors.xaml` is loaded THEN it SHALL define the full charcoal/red token set
   as `Color` resources — surfaces (`bg`, `surface`, `surface-2`, `media-bg`, `input-bg`), ink
   (`ink`, `muted`, `line`), brand (`brand`, `brand-600`, `brand-700`, `brand-200`, `brand-050`,
   `accent`, `accent-hot`), and status (`ok`, `danger` and their `-700`/`-200`/`-050` steps).
2. WHEN a token has both a dark and a light value in `app.css` THEN the XAML resource SHALL carry both
   via `AppThemeBinding`, using the dark value as the `Default`.
3. WHEN a web token is a gradient (`--grad-accent`, `--grad-accent-hot`, `--hero-bloom`,
   `--glow-media`, `--vignette-media`) THEN it SHALL be expressed as a named `LinearGradientBrush` or
   `RadialGradientBrush` resource with the same stops and angles.
4. IF a web token uses `color-mix()` THEN the ported value SHALL be the precomputed literal `rgba`,
   since XAML has no equivalent function, AND a comment SHALL record the source expression.
5. WHEN the stock MAUI template keys (`Primary`, `Secondary`, `Tertiary`, `Gray100`–`Gray950`) are
   referenced by the untouched template `Styles.xaml` THEN they SHALL be retuned to the new ramp
   rather than deleted, so the 434-line template dictionary inherits the brand without being rewritten.
6. WHEN the redesign is complete THEN no `.xaml` file under `Views/` SHALL contain a literal hex colour;
   every colour SHALL resolve through a token resource.

### Requirement 2 — Brand typography

**User Story:** As a customer, I want the app to use the same typeface as the storefront so that the
two read as one brand.

#### Acceptance Criteria

1. WHEN the app starts THEN `Play-Regular.ttf` and `Play-Bold.ttf` SHALL be registered in
   `MauiProgram.CreateMauiApp` under the aliases `PlayRegular` and `PlayBold`.
2. WHEN any text renders THEN it SHALL use Play by default, matching `app.css`'s `font-family` rule.
3. WHEN a heading renders THEN hierarchy SHALL be carried by size and letter-spacing rather than
   intermediate weights, because Play ships only 400 and 700 — the same constraint `app.css` documents.
4. IF the Play font files cannot be added for a licensing or availability reason THEN OpenSans SHALL
   remain and the deviation SHALL be recorded, rather than substituting a visually different face.
5. WHEN the font is bundled THEN the added APK size SHALL be under 150 KB.

### Requirement 3 — Themed shell and navigation

**User Story:** As a customer, I want the app chrome — tab bar, page backgrounds, navigation bar — to
carry the brand rather than platform defaults.

#### Acceptance Criteria

1. WHEN any page renders THEN its background SHALL be the `bg` token, not the platform default white.
2. WHEN the tab bar renders THEN its background SHALL be the `surface` token, the selected tab SHALL
   use `brand`, and unselected tabs SHALL use `muted`.
3. WHEN the Android system bars render THEN the status bar SHALL match the shell background and its
   icon contrast SHALL follow the active theme.
4. WHEN a pushed route (product, checkout, order detail) shows a navigation bar THEN it SHALL be
   themed from the same tokens as the tab bar.
5. WHEN the app is in light theme THEN every rule above SHALL resolve to the light token values with
   no hardcoded exceptions.

### Requirement 4 — Core storefront screens

**User Story:** As a customer, I want Home, Catalog, Product and Cart to look like the storefront so
that the mobile experience does not feel like a lesser version of the site.

#### Acceptance Criteria

1. WHEN Home renders THEN it SHALL present a hero block using the remote banner artwork with the
   `hero-bloom` and `hero-vignette` brushes over it, replacing the current plain "Build your PC" card.
2. WHEN Home renders THEN categories SHALL be image tiles using the remote category artwork, not
   bare text chips.
3. WHEN a product card renders on any screen THEN it SHALL follow one shared template — media panel
   on `media-bg` with the `glow-media` brush behind the image, then name, brand, and price in `brand` —
   mirroring `PcMarket.Web/Components/Shared/ProductCard.razor`.
4. WHEN Catalog renders THEN filter and sort controls SHALL be styled as pill controls on `surface-2`,
   and the product grid SHALL use the shared card template.
5. WHEN Product detail renders THEN the image area SHALL sit on `media-bg` with the media glow, and
   the variant selector and add-to-cart button SHALL use the `grad-accent` brush.
6. WHEN Cart renders THEN line items SHALL use `surface` cards with `line` separators, and the
   checkout CTA SHALL use the `grad-accent` brush.

### Requirement 5 — Secondary screens

**User Story:** As a customer, I want the account, auth and order screens to match the rest of the
app so that no part of it looks unfinished.

#### Acceptance Criteria

1. WHEN Checkout, Orders, OrderDetail, Account, Addresses, Login, Register or Otp renders THEN it
   SHALL use the token palette with no stock-template colours remaining.
2. WHEN any `Entry`, `Picker` or `Editor` renders THEN it SHALL sit on the `input-bg` token with a
   `line` border and a `brand` focus state.
3. WHEN a form validation error shows THEN it SHALL use the `danger` token, and a success state SHALL
   use `ok`.
4. WHEN an order status badge renders THEN it SHALL be a filled pill using the status token that
   matches its state.
5. WHEN these screens are compared against the core four THEN spacing, corner radius and type scale
   SHALL be visibly consistent.

### Requirement 6 — Motion system

**User Story:** As a customer, I want the app to feel as considered as the storefront, with motion
that reinforces rather than distracts.

#### Acceptance Criteria

1. WHEN a product card is pressed THEN it SHALL animate on the web's lift curve — rise on
   `cubic-bezier(.16, 1, .3, 1)` over 500 ms, settle back on `cubic-bezier(.65, 0, .35, 1)` over
   620 ms — implemented as custom `Easing` functions carrying the same coefficients.
2. WHEN a list of cards first appears THEN each item SHALL rise `24px` and fade in over `620 ms`,
   staggered per item, reproducing the web's scroll-reveal entrance.
3. WHEN motion runs on the target device (Redmi Note 11, Android 11) THEN the interaction SHALL hold
   a smooth frame rate; IF a given effect cannot hold it THEN that effect SHALL be reduced to its
   static form and the reduction SHALL be recorded, rather than shipping visible jank.
4. WHEN the OS reports that animations are disabled THEN all motion SHALL be skipped and the static
   visual state SHALL render.
5. WHEN `--shadow-lift`'s three stacked shadows are ported THEN they SHALL be approximated by MAUI's
   single-shadow model, and the approximation SHALL be documented — MAUI has no multi-shadow support.

### Requirement 7 — Remote artwork

**User Story:** As a developer, I want the storefront's decorative imagery fetched over HTTP so that
the app matches the web without carrying 36 photographs in the APK.

#### Acceptance Criteria

1. WHEN the app needs decorative artwork THEN it SHALL resolve the URL from a configurable media root,
   following the existing `AppConfig.ApiRootUrl` pattern.
2. WHEN running a Debug build against a local storefront THEN the media root SHALL default to
   `http://localhost:5193`, reachable via `adb reverse tcp:5193 tcp:5193`.
3. WHEN running a Release build THEN the media root SHALL point at the production storefront host over
   HTTPS.
4. IF a remote image fails to load THEN the containing view SHALL fall back to a token-coloured
   surface and remain usable, never showing a broken-image placeholder or crashing.
5. WHEN images load THEN they SHALL be cached by the platform so a repeat visit to a screen does not
   refetch, and the app SHALL remain responsive while they are in flight.
6. WHEN the APK is built THEN its size increase over the pre-redesign baseline SHALL be under 500 KB,
   the requester's explicit constraint.

### Requirement 8 — Theme switching and persistence

**User Story:** As a customer, I want to choose light or dark and have the app remember it, as the
storefront does.

#### Acceptance Criteria

1. WHEN the app starts for the first time THEN it SHALL render in dark theme, matching the web default.
2. WHEN the user toggles the theme from the Account screen THEN every open screen SHALL update
   immediately without a restart.
3. WHEN the choice is made THEN it SHALL persist to `Preferences` and survive an app restart, mirroring
   the web's `localStorage['pcmarket.theme']`.
4. IF no choice has been stored THEN dark SHALL be used, rather than following the OS theme.
5. WHEN the theme changes THEN the system bars SHALL update with it.

### Requirement 9 — Build and on-device verification

**User Story:** As a developer, I want the redesign verified on real hardware, because Phase 8 proved
that XAML faults here surface at runtime rather than at compile time.

#### Acceptance Criteria

1. WHEN `dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android` runs THEN it
   SHALL succeed with 0 warnings and 0 errors under the solution's warnings-as-errors settings.
2. WHEN the same build runs for `-f net10.0-ios` THEN it SHALL also succeed, keeping the iOS head
   compiling as Phase 8 requires.
3. WHEN the app is deployed to the Redmi Note 11 with the API on `:5055` via
   `adb reverse tcp:5055 tcp:5055` THEN every restyled screen SHALL render without a
   `StaticResource not found` or any other runtime XAML fault.
4. WHEN each screen is exercised THEN `adb logcat` SHALL show zero `FATAL EXCEPTION` entries.
5. WHEN verification completes THEN `adb shell screencap` captures of Home, Catalog, Product and Cart
   SHALL exist for both themes, compared against the web storefront at `localhost:5193`.
6. WHEN the existing test suite runs THEN it SHALL stay green — this slice touches presentation only
   and SHALL NOT change view-model behaviour.
