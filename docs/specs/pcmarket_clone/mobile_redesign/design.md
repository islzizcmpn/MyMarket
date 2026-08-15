# Design Document

## Overview

Port the storefront's premium design system from `PcMarket.Web/wwwroot/app.css` (1893 lines of CSS
custom properties) into `PcMarket.Mobile`'s XAML resource dictionaries, then restyle all 18 XAML files
on top of it. The app keeps its Phase 8 architecture untouched — same MVVM view models, same
`PcMarket.ApiClient` calls, same navigation graph. **Nothing in `ViewModels/` or `Services/` changes
behaviour;** this is a presentation-layer slice.

The central translation problem is that CSS custom properties and XAML resources are not equivalent
in three ways that shape the whole design:

| CSS mechanism | XAML equivalent | Consequence |
|---|---|---|
| `--token` cascading, re-declared under `[data-theme]` | `AppThemeBinding` on each resource | Tokens must be declared once carrying *both* values; there is no cascade to override |
| `color-mix(in srgb, …)` | none | Every mixed colour must be precomputed to a literal `rgba` |
| Multiple stacked `box-shadow` layers | `Shadow` — exactly one per element | `--shadow-lift`'s three layers collapse to one approximation |
| `:hover` | none on touch | Lift becomes a `Pressed` visual state |
| `IntersectionObserver` scroll reveal | none | Reveal hooks the item container's `Loaded` event |

The strategy that keeps this tractable is **retune, don't rewrite**. The stock template
`Styles.xaml` is 434 lines that reference `Colors.xaml` keys (`Primary`, `Gray500`, …) through
`AppThemeBinding` already. Redefining those keys to the new ramp propagates the brand through every
templated control — buttons, entries, pickers, switches — without touching that file. `AppStyles.xaml`
then carries the storefront-specific styles on top.

## UI

### Home — before and after

```
BEFORE (shipped)                          AFTER
+--------------------------------+        +--------------------------------+
| PcMarket                       |        | PCMarket            [sun/moon] |
+--------------------------------+        +================================+
| +----------------------------+ |        | ##  hero banner (remote)   ## |
| | Build your PC              | |        | ##  + hero-bloom brush     ## |
| | Components, laptops and... | |        | ##                         ## |
| | [Search products] [Search] | |        | ##  Discounts to start     ## |
| +----------------------------+ |        | ##  the work season        ## |
|                                |        | ##  [ Go to catalog ]      ## |
| Categories                     |        | ##  (grad-accent button)   ## |
| [Computers] [Accessories]      |        | +----------------------------+ |
|                                |        | | Search products      [->] | |
| New arrivals                   |        | +----------------------------+ |
| +----------------------------+ |        |                                |
| | [img] Kingston FURY 16GB   | |        | CATEGORIES                     |
| |       Kingston             | |        | +---------+ +---------+       |
| |       650 000 so'm  (blue) | |        | | [photo] | | [photo] |       |
| +----------------------------+ |        | |Computers| |Accessor.|       |
| +----------------------------+ |        | +---------+ +---------+       |
| | [img] Logitech M330        | |        |                                |
| ...                            |        | NEW ARRIVALS                   |
+--------------------------------+        | +------------+ +------------+ |
| Home  Catalog  Cart  Account   |        | |  (glow)    | |  (glow)    | |
+--------------------------------+        | |  [ image ] | |  [ image ] | |
                                          | |            | |            | |
white bg, blue price, plain rows          | | Kingston.. | | Logitech.. | |
                                          | | Kingston   | | Logitech   | |
                                          | | 650 000    | | 320 000    | |
                                          | +------------+ +------------+ |
                                          +--------------------------------+
                                          | Home  Catalog  Cart  Account   |
                                          +--------------------------------+

                                          charcoal bg (#101013), brand-red
                                          price, media glow behind images
```

### Product card — the shared template

Used by Home, Catalog and search results. Mirrors `ProductCard.razor`.

```
+----------------------------------+
|  +----------------------------+  |  <- media panel, media-bg token
|  |        .-~~~~~~~-.         |  |     glow-media radial brush behind
|  |      (   product   )       |  |     vignette-media over
|  |       `-_______-'          |  |
|  +----------------------------+  |
|                                  |
|  Kingston FURY 16GB DDR4         |  <- ink, Play 400, 2-line clamp
|  Kingston                        |  <- muted, small
|  650 000 so'm                    |  <- brand, Play 700
|                                  |
|  [   Add to cart   ]             |  <- grad-accent brush, white text
+----------------------------------+
   surface token, radius 16, shadow

   PRESSED: translates -16px on
   cubic-bezier(.16,1,.3,1) / 500ms
   shadow -> shadow-lift approximation
```

### Catalog — filter pills

```
+--------------------------------+
| < Catalog                      |
+--------------------------------+
| [Search...................]    |
|                                |
| (All) (Laptops) (Memory) (Mice)|  <- pills: surface-2 bg, line border
|  ^^^ active = grad-accent      |     active fills with grad-accent
|                                |
| Sort: [ Newest      v ]        |  <- picker on input-bg
+--------------------------------+
| +------------+ +------------+  |
| |  card      | |  card      |  |  <- shared product card template,
| +------------+ +------------+  |     2-column grid
| +------------+ +------------+  |
| |  card      | |  card      |  |
+--------------------------------+
```

### Account — theme toggle placement

```
+--------------------------------+
| Account                        |
+--------------------------------+
| +----------------------------+ |
| | Aziz                       | |
| | +998 90 000 00 00          | |
| +----------------------------+ |
|                                |
| My orders                   >  |
| Addresses                   >  |
|                                |
| Appearance                     |
| +----------------------------+ |
| | Theme      ( Dark | Light )| |  <- segmented control, persists
| +----------------------------+ |     to Preferences
|                                |
| [        Sign out           ]  |
+--------------------------------+
```

## Architecture

### Resource dictionary layering

```
App.xaml (MergedDictionaries, order matters)
  1. Resources/Styles/Colors.xaml     <- REWRITTEN: token ramp, both themes
  2. Resources/Styles/Brushes.xaml    <- NEW: gradient + glow brushes (consume Colors)
  3. Resources/Styles/Styles.xaml     <- UNTOUCHED template (434 lines);
                                         inherits brand via Colors.xaml keys
  4. Resources/Styles/AppStyles.xaml  <- REWRITTEN: storefront styles (consume Colors + Brushes)
  5. Resources/Styles/Motion.xaml     <- NEW: animation constants + templates
```

Dictionaries load in order and later entries win, so `AppStyles.xaml` can override anything the
template sets without editing it. `StaticResource` resolves at **parse time** against the
dictionaries merged so far, so every dictionary must follow the ones it references —
`Brushes.xaml` after `Colors.xaml`, and both before `AppStyles.xaml`, which consumes them. A
forward reference compiles clean and fails at runtime.

> **Corrected 2026-08-15 during Task 2.** This list originally placed `Brushes.xaml` at position 4,
> after `AppStyles.xaml`. That would have broken Task 5 the moment `AppStyles` referenced the accent
> gradient for its button styles — and broken it at launch, not at build.

### Token mapping

`Colors.xaml` becomes the single source, each entry carrying both themes:

```xml
<!-- app.css --bg / :root[data-theme="light"] --bg -->
<Color x:Key="TokenBg">#101013</Color>
<Color x:Key="TokenBgLight">#f5f4f2</Color>
```

Consumers bind through `AppThemeBinding`. Named `*Light` colours exist only so the theme binding has
something to point at; views never reference them directly.

| `app.css` | XAML key | Dark | Light |
|---|---|---|---|
| `--bg` | `TokenBg` | `#101013` | `#f5f4f2` |
| `--surface` | `TokenSurface` | `#17171b` | `#ffffff` |
| `--surface-2` | `TokenSurface2` | `#1d1d22` | `#faf9f7` |
| `--ink` | `TokenInk` | `#f2f1ef` | `#16161a` |
| `--muted` | `TokenMuted` | `#8e8d97` | `#6c6b74` |
| `--line` | `TokenLine` | `#26262c` | `#e5e3df` |
| `--brand` | `TokenBrand` | `#e0452e` | `#b5241a` |
| `--brand-600` | `TokenBrand600` | `#c1121f` | `#8f1a12` |
| `--accent-hot` | `TokenAccentHot` | `#ff7a3d` | `#e8752f` |
| `--media-bg` | `TokenMediaBg` | `#1b1b20` | `#efedea` |
| `--input-bg` | `TokenInputBg` | `#141418` | `#ffffff` |
| `--ok` / `--danger` | `TokenOk` / `TokenDanger` | `#35c48a` / `#f36868` | `#1a9d6a` / `#d33a3a` |

Stock template keys are retuned in place so `Styles.xaml` inherits the brand:
`Primary → #e0452e`, `Secondary → #1d1d22`, `Tertiary → #c1121f`, and the `Gray*` ramp shifted onto
the graphite hue.

### Precomputed `color-mix` values

`--glow-media`'s dark stops become literals. `color-mix(in srgb, #ff7a3d 30%, transparent)` is
`rgba(255,122,61,.30)`; `color-mix(in srgb, #e0452e 17%, transparent)` is `rgba(224,69,46,.17)`.
Each carries a comment naming the source expression so the two stay traceable.

### Motion layer

```
Services/Motion/
  BrandEasing.cs      Custom Easing instances carrying the web's cubic-bezier coefficients
  CardLift.cs         Attached behaviour: Pressed/Released -> TranslateTo + shadow swap
  RevealBehavior.cs   Attached behaviour: on Loaded, rise 24px + fade in, staggered
  MotionGate.cs       Reads OS animation scale; returns false -> behaviours no-op
```

`BrandEasing` implements the two curves as `new Easing(t => …)` evaluating the cubic-bezier
numerically. MAUI's built-in `Easing` set has no equivalent to `cubic-bezier(.16,1,.3,1)`, and
substituting `CubicOut` would visibly change the character the web tuned for.

## Components and Interfaces

### Media configuration

Extends the existing `AppConfig` pattern (`Services/AppConfig.cs`):

```csharp
public static class AppConfig
{
    public static string ApiRootUrl => /* unchanged */;

    /// <summary>Root for decorative storefront artwork, served by PcMarket.Web rather than bundled.
    /// Debug points at the local storefront, reached over `adb reverse tcp:5193 tcp:5193` on a
    /// physical device; the emulator alias is used when running virtual.</summary>
    public static string MediaRootUrl { get; }
}

/// <summary>Resolves a storefront-relative artwork path to an absolute URL.</summary>
public static class Artwork
{
    public static string Url(string relativePath);   // "home/banner-dark.jpg" -> absolute
    public static string Banner   { get; }
    public static string Category(string slug);      // falls back to a token surface when absent
}
```

### Shared card template

A `DataTemplate` cannot be shared across differently typed collections, so the card is instead a
`ContentView` with a bindable property, which any template can host:

```csharp
public partial class ProductCardView : ContentView
{
    public static readonly BindableProperty ProductProperty;   // ProductListItemDto
    public ProductListItemDto? Product { get; set; }
}
```

Home, Catalog and search bind their `DataTemplate` to this one view, so the card's look is defined
once — the mobile counterpart of `ProductCard.razor`.

### Theme service

```csharp
/// <summary>Applies and persists the light/dark choice. Mirrors the web's
/// localStorage['pcmarket.theme'], defaulting to dark when nothing is stored.</summary>
public sealed class ThemeService
{
    public AppTheme Current { get; }
    public void Apply(AppTheme theme);   // sets Application.UserAppTheme + Preferences + system bars
    public void Restore();               // called from App startup, before the first frame
}
```

## Flows

### Startup theme resolution

1. `App` constructor runs `ThemeService.Restore()` **before** `InitializeComponent()` completes its
   first layout pass.
2. `Preferences.Get("pcmarket.theme", null)` is read.
3. If a value is stored, `Application.Current.UserAppTheme` is set to it; otherwise `AppTheme.Dark`.
4. System bar colours are applied to match.
5. Every `AppThemeBinding` in the merged dictionaries resolves against that value on first paint —
   no flash of the wrong theme, the same guarantee the web's inline IIFE provides.

### Remote artwork load

1. A view binds an `Image.Source` to `Artwork.Banner`.
2. MAUI resolves it as a `UriImageSource` with caching enabled.
3. On success the image paints over its token-coloured container.
4. On failure the container's background — already a token colour — is what remains visible, so the
   layout is unchanged and nothing broken is shown.
5. No retry is attempted; decorative artwork is not worth a retry budget, and the fallback is
   acceptable on its own.

### Card press lift

1. `CardLift` behaviour attaches to the card's root `Border`.
2. `MotionGate.Enabled` is checked once; when false the behaviour detaches and does nothing.
3. On `Pressed`, `TranslateTo(0, -16, 500, BrandEasing.LiftIn)` runs and the shadow swaps to the
   lift approximation.
4. On `Released` or cancel, `TranslateTo(0, 0, 620, BrandEasing.LiftOut)` runs and the shadow reverts.
5. The navigation command fires on `Released`, independent of whether the animation has settled, so
   motion never delays the tap.

## Integration Points

- **`Resources/Styles/Styles.xaml`** — deliberately not edited. It inherits the brand through the
  retuned `Colors.xaml` keys. Any change here would have to be re-made on every MAUI template update.
- **`MauiProgram.cs`** — font registration (Play) and DI registration for `ThemeService`.
- **`App.xaml`** — two new merged dictionaries (`Brushes.xaml`, `Motion.xaml`).
- **`AppConfig.cs`** — gains `MediaRootUrl` alongside the existing `ApiRootUrl`.
- **`PcMarket.Web/wwwroot/images/`** — the source of truth for artwork. Nothing is copied; the app
  fetches from the running storefront.
- **`network_security_config.xml`** — already permits cleartext to `localhost` and `10.0.2.2`, which
  covers the Debug media root. **No change needed** — confirmed 2026-08-15.
- **Dev prerequisite** — a physical device needs `adb reverse tcp:5193 tcp:5193` in addition to the
  existing `5055` forward, and the storefront must be running. This is a new step in the run sequence
  and belongs in the README.

## Security Considerations

- Artwork is fetched over cleartext HTTP **only** in Debug against `localhost`, which the existing
  network security config already scopes. Release builds use HTTPS to the production host; the
  cleartext exemption must not be widened to cover it.
- Remote image URLs are constructed from a compile-time root plus a fixed relative path — no
  user-controlled input reaches URL construction, so there is no injection surface.
- A failed or hostile image response can only produce a decode failure, which Requirement 7.4 already
  routes to the token-colour fallback.
- No token, credential, or personal data is involved in this slice.

## Performance Considerations

- **The motion system is the main risk.** The target is a Redmi Note 11 — a 2022 mid-range device on
  Android 11. `TranslateTo` animates on the UI thread; running it per card in a scrolling
  `CollectionView` can contend with recycling. Mitigation: the reveal animation runs once per item
  container on `Loaded`, not on every recycle, and Requirement 6.3 makes degrading to static the
  accepted outcome rather than shipping jank.
- **Shadows are expensive on Android.** MAUI's `Shadow` maps to an elevation/render-node effect;
  applying it to every card in a long list is measurably worse than applying it to the pressed card
  alone. The static card therefore uses a `line` border for definition and takes the shadow only in
  the lifted state.
- **Radial gradient brushes** are painted once per element and do not re-render on scroll, matching
  the web's "static radial-gradient, only opacity animates" note in `app.css`.
- **Remote artwork** adds network latency to first paint of Home. `UriImageSource` caches to disk by
  default; the cache validity should be left long, since this artwork changes at deploy cadence.
- **APK size** is the requester's stated constraint: fonts add ~60 KB, no photography is bundled, so
  the budget in Requirement 7.6 (under 500 KB) has ample headroom.

## Error Handling

- **Missing `StaticResource`** is the failure mode Phase 8 specifically recorded — it compiles clean
  and crashes at launch. Mitigation: the resource dictionaries land and get deployed *first* (Task 3),
  before any view references them, so a missing key is caught on the earliest device run.
- **Remote image failure** → token-colour fallback, no retry, no user-visible error (Req 7.4).
- **Font registration failure** → MAUI falls back to the system face silently; the on-device
  verification pass must confirm Play is actually rendering rather than assuming registration worked.
- **Theme persistence failure** (`Preferences` unavailable) → fall back to dark and continue, never
  block startup. This mirrors the web's `try/catch` around `localStorage`.
- **Animation on a torn-down page** — `TranslateTo` on a disposed view throws. All motion behaviours
  must cancel on `Unloaded`.
