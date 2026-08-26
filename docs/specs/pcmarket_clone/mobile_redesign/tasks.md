# Implementation Plan

Ordered by dependency. Tasks 1–3 must land and be deployed to the device before any view work
begins — Phase 8's changelog records that a missing `StaticResource` compiles clean and crashes at
launch, so the token layer needs its own device run before 18 files start depending on it.

- [x] 1. Port the token ramp into `Colors.xaml`
  - Rewrite `src/PcMarket.Mobile/Resources/Styles/Colors.xaml` with the full token set from
    `app.css`, each key carrying a dark value and a `*Light` companion (see the mapping table in
    design.md). **Done** — 27 token pairs (54 `Color` resources) covering surfaces, ink, brand,
    status, and hero/error-UI. The companion is declared even where both themes share a value
    (`TokenAccent`), so consumers bind uniformly without checking which tokens are theme-invariant.
  - Retune the stock template keys in place — `Primary`, `Secondary`, `Tertiary`, `Gray100`–`Gray950` —
    so the untouched 434-line `Styles.xaml` inherits the brand. **Done**, and verified: all 18 keys
    the template references still resolve.
  - Comment each token with the `app.css` property it came from. **Done**, with the leading double
    hyphen stripped — XML forbids it inside a comment (see the deviation note below).
  - _Requirements: 1.1, 1.2, 1.5_

- [x] 2. Add `Brushes.xaml` for gradients and glows
  - New dictionary with `LinearGradientBrush` for `grad-accent` / `grad-accent-hot` and
    `RadialGradientBrush` for `glow-media`, `hero-bloom`, `vignette-media`. **Done** — 8 brushes,
    including `BrushHeroVignette` and a `BrushHeroPanel` fallback for when remote artwork has not
    loaded.
  - Precompute every `color-mix()` stop to a literal `rgba`, commenting the source expression.
    **Done** — note XAML orders alpha first, so `rgba(255,122,61,.30)` is `#4DFF7A3D`.
  - Must be merged **after** `Colors.xaml` — brushes forward-referencing colours fail at runtime.
  - _Requirements: 1.3, 1.4_

- [x] 3. Wire dictionaries and prove them on-device
  - Register `Brushes.xaml` in `App.xaml`'s `MergedDictionaries` **at position 2, directly after
    `Colors.xaml` and ahead of `AppStyles.xaml`** — see the correction note in design.md. **Done**,
    with the ordering rule recorded as a comment in `App.xaml` so it is not undone by accident.
  - Build, deploy to the Redmi Note 11, launch, and confirm no `StaticResource not found`. **Done**
    in both themes: clean logcat, and token values confirmed live by pixel sampling.
  - This is a deliberate checkpoint, not a formality — it costs one deploy and de-risks the next
    eight tasks. **It earned its keep.** The first pass reported success against stale Phase 8
    assemblies: `adb install -r` of the Debug APK does not deploy managed code, because
    `EmbedAssembliesIntoApk=false` by default. Deploy with `-t:Install` and verify by pixel, never by
    eye — see the CRITICAL section in research.md. Every later task's verification depended on
    catching this.
  - _Requirements: 9.1, 9.3_

- [x] 4. Bundle the Play typeface
  - Add `Play-Regular.ttf` and `Play-Bold.ttf` (OFL) to `Resources/Fonts/`, register in
    `MauiProgram.CreateMauiApp` as `PlayRegular` / `PlayBold`. **Done**, with `OFL.txt` alongside as
    the licence requires, and the csproj `MauiFont` glob narrowed to `*.ttf` so the licence text is
    not bundled as a typeface.
  - Set Play as the default face and build the type scale on size + letter-spacing, since Play has no
    weights between 400 and 700. **Done** — implicit styles in `AppStyles.xaml` re-set the nine text
    control types the untouched template puts on OpenSans. Bold is selected by naming the `PlayBold`
    file rather than by `FontAttributes="Bold"`, which would synthesise weight over an already-bold
    face. `CharacterSpacing` tightens H1/H2; prices stay at 0.
  - Confirm on-device that Play is actually rendering — registration failure is silent. **Done**,
    confirmed by cropping the 24pt heading and comparing letterforms before/after.
  - **Deviation — fonts are subsetted.** The upstream TTFs are 410 KB together and packed to
    **+499 KB**, breaching Requirement 2.5's 150 KB budget threefold. Subsetted with fontTools to
    Latin + Cyrillic with hinting stripped: 105 KB on disk, **50 KB packed — Requirement 2.5 passes**.
    Greek and Vietnamese dropped; Cyrillic deliberately kept for the localization slice. Command and
    rationale in research.md.
  - _Requirements: 2.1, 2.2, 2.3, 2.5_

- [x] 5. Rewrite `AppStyles.xaml` as the storefront style layer
  - Replace the current 87-line file: `Card`, `H1`/`H2`/`H3`, `MutedText`, `Price`, `ErrorText`,
    `SuccessText`, `PrimaryButton`, `GhostButton`, `Quantity` all rebuilt on tokens. **Done** — and
    the ten key names are unchanged, so all 18 views re-skinned without being edited.
  - Add the styles the redesign needs: `Pill`, `PillActive`, `MediaPanel`, `StatusBadge`, and input
    styles on `input-bg` with a `brand` focus state. **Done**, plus `InputField`/`InputFieldFocused`,
    `StatusBadgeText` and `Segmented` — see the deviation note below.
  - Remove the local `Accent`/`Danger`/`Success`/`Muted`/`CardLight`/`CardDark` colour definitions —
    they are superseded by tokens. **Done**; `AppStyles.xaml` now contains no literal colour at all.
  - **Discovery that reshaped the file — an implicit style REPLACES the template's, it does not
    layer over it.** Resource lookup finds exactly one implicit style per control type and the last
    dictionary merged wins, so the old one-setter `Style TargetType="Label"` was silently discarding
    the template's `TextColor` and leaving labels on the platform default. It only looked right
    because Android's default happens to be near-white under night mode. Every implicit style is now
    complete. This also means the implicit `Page`, `Border`, `Button` and input styles here own
    those types outright; the template keeps the ones not named (ActivityIndicator, CheckBox,
    Switch, Slider, …), which still inherit the brand through the retuned keys in `Colors.xaml`.
  - **Deviation — the input focus ring is split across two styles.** MAUI inputs have no border
    property, so the `line` border of Requirement 5.2 is drawn by an `InputField` `Border` wrapper.
    Swapping it to the `brand` ring needs the child's `IsFocused`, which a shared style cannot reach
    (`x:Reference` resolves against the view's name scope, not the dictionary's). The style layer
    ships both states and the view wires the swap with a `DataTrigger`; the inputs themselves also
    carry a `Focused` state tinting the fill with `brand-050`, so an unwrapped input still signals
    focus. `Quantity` went 38 to 40 px square: the template's 44 px minimum was overriding the
    declared 38 anyway, so the number was never real.
  - _Requirements: 1.6, 5.2, 5.3_

- [x] 6. Theme service and toggle
  - `ThemeService` with `Apply` / `Restore`, persisting to `Preferences` under `pcmarket.theme`,
    defaulting to dark; register in DI. **Done** — singleton, injected into `App` and
    `AccountViewModel`.
  - Call `Restore()` from `App` before the first layout pass so there is no wrong-theme flash.
    **Done**, ahead of `InitializeComponent()` so `UserAppTheme` is settled before the merged
    dictionaries load and every `AppThemeBinding` resolves right on the first paint.
  - Segmented Dark/Light control on `AccountPage`, wired to `AccountViewModel`. **Done** — two
    `Pill` buttons in a `Segmented` track, the selected one filled by a `DataTrigger`. Offered
    signed out as well as in: it is about the app, not the account.
  - Wrap `Preferences` access in `try/catch` — blocked storage falls back to dark, never blocks start.
    **Done** on both read and write.
  - **Addition — `ApplySystemBars()` beyond the two methods design.md names.** The platform window
    does not exist while `App`'s constructor runs, so the bars cannot be painted from `Restore()`.
    They are painted on `Window.Activated` instead, which also re-asserts them after a resume,
    because Android restores its own bar colours behind the app.
  - Verified on device: toggling repaints every open screen with no restart, the choice survives a
    force-stop, and a first run with no stored value renders dark **while the OS was in light
    mode** — which is the only way to prove Requirement 8.4 rather than assume it.
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [x] 7. Theme the shell and system bars
  - `AppShell.xaml`: tab bar on `surface`, selected `brand`, unselected `muted`; page background `bg`.
    **Done.** The chrome colours are set on the `Shell` element rather than through a style, so they
    outrank the template's `Shell` style, which is merged ahead of `AppStyles.xaml` and paints the
    tab bar black in dark theme. Page background is the implicit `Page` style in `AppStyles.xaml`,
    overriding the template's `OffBlack` (the `surface` step) down to `bg`.
  - Android status/navigation bar colours following the active theme, updated when it changes.
    **Done** via a partial `SystemBars` class with an Android-only implementation; the other heads
    get an empty partial method that compiles away. Bar fill resolves from `TokenSurface` out of the
    merged dictionaries, so `Colors.xaml` stays the single source.
  - Themed navigation bar for the pushed routes. **Done** — Shell owns the nav bar for product,
    checkout and order detail as well as the tabs, so the same setters cover them.
  - Also retuned `Platforms/Android/Resources/values/colors.xml` and the `MauiSplashScreen` colour
    off the template purple, which was flashing violet on every cold start ahead of the charcoal UI.
    The `MauiIcon` background is still `#512BD4` — left alone deliberately, see research.md.
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 8.5_

- [x] 8. Remote artwork plumbing
  - Add `MediaRootUrl` to `AppConfig` (Debug → `http://localhost:5193`, emulator alias when virtual,
    Release → production storefront over HTTPS) and an `Artwork` resolver. **Done** — `MediaRootUrl`
    mirrors `ApiRootUrl`'s shape exactly, down to the emulator branch, and `Services/Artwork.cs`
    exposes `Url` / `Banner` / `Category` / `Source`.
  - Wire `UriImageSource` with caching; token-coloured container behind every remote image so a
    failure degrades to a flat surface. **Done** — `Artwork.Source` builds the cached source and a
    `RemoteImageConverter` (`RemoteImage`) puts it in reach of XAML; all four existing remote images
    (Home, Catalog, Cart, Product) now sit inside a `MediaPanel` border.
  - Document the new `adb reverse tcp:5193 tcp:5193` dev step in `README.md`. **Done** — the tunnel
    and `scripts/start-mobile-dev.ps1 -WithStorefront` were already written up; the resolution table
    now carries the artwork column and names `MediaRootUrl` and `Artwork` alongside it.
  - **`Category(slug)` carries the storefront's stand-in map, and that is not cosmetic.** The naive
    `cat-{slug}.jpg` rule 404s for `computers` — the art set has no such file, and the web resolves
    it to `feat-case-1.jpg` on disk. Mobile cannot stat a remote folder, so the map is mirrored
    rather than re-derived. Verified against the running storefront: `banner-dark.jpg`,
    `cat-accessories.jpg` and `feat-case-1.jpg` all 200, `cat-computers.jpg` 404s.
  - **Deviation — cache validity is split by configuration.** Requirement 7.5 wants a repeat visit
    not to refetch, and design.md wants the validity left long; taken literally in Debug that means a
    replaced storefront file stays invisible for a month. Debug uses 5 minutes, Release 30 days.
  - Verified on device: media panel samples `#1B1B20` dark and `#EFEDEA` light — exact `media-bg` in
    both themes — with the remote image painting over it, clean logcat, and three entries in the
    platform image cache still serving after a relaunch.
  - **Requirement 7.4 proved by inducing the failure, not by inspection.** Emptying the Glide cache and
    taking the device offline made every product image fail at once; each panel read a uniform
    `#1B1B20` where image content had been, thumbnails and the 220dp product panel alike, layout
    unmoved and no broken-image glyph. Glide logs the failure as a warning that goes nowhere — zero
    `FATAL EXCEPTION`. Restoring the network brought all three images back, which is what rules out a
    code fault dressed up as the fallback. `adb reverse` rides USB and survives airplane mode, so the
    catalogue kept loading over the tunnel while only the images failed — the isolation that makes the
    reading unambiguous. Method and its two traps are in research.md.
  - Requirement 7.3 is code-level only: no Release build was run against the production host.
  - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [ ] 9. Motion layer
  - `BrandEasing` implementing the web's two cubic-bezier curves numerically; `MotionGate` reading the
    OS animation scale; `CardLift` and `RevealBehavior` attached behaviours.
  - All behaviours cancel on `Unloaded` — animating a torn-down view throws.
  - Approximate `--shadow-lift`'s three layers with MAUI's single shadow; record the approximation.
  - _Requirements: 6.1, 6.2, 6.4, 6.5_

- [ ] 10. Shared `ProductCardView`
  - `ContentView` with a bindable `Product`, implementing the card from design.md: media panel on
    `media-bg` with the glow brush, name / brand / price, `grad-accent` CTA, `CardLift` attached.
  - Static card carries a `line` border rather than a shadow; the shadow appears only when lifted,
    because per-card shadows in a long list are measurably expensive on Android.
  - _Requirements: 4.3, 6.1_

- [ ] 11. Restyle Home
  - Hero block with remote banner artwork under the `hero-bloom` and `hero-vignette` brushes,
    replacing the plain "Build your PC" card.
  - Category tiles using remote category artwork instead of text chips.
  - New arrivals switched to `ProductCardView` with staggered reveal.
  - _Requirements: 4.1, 4.2, 4.3, 6.2_

- [ ] 12. Restyle Catalog and Product
  - Catalog: filter/sort pills on `surface-2` with `grad-accent` active fill; two-column grid of
    `ProductCardView`; paging preserved.
  - Product: image area on `media-bg` with the media glow, variant selector and add-to-cart on
    `grad-accent`.
  - _Requirements: 4.4, 4.5_

- [ ] 13. Restyle Cart and Checkout
  - Cart: `surface` line-item cards with `line` separators, `grad-accent` checkout CTA.
  - Checkout: token-styled inputs, address picker, delivery and payment selectors.
  - _Requirements: 4.6, 5.1, 5.2_

- [ ] 14. Restyle the remaining screens
  - Orders, OrderDetail, Account, Addresses, Login, Register, Otp.
  - Status badges as filled pills using the matching status token.
  - Verify spacing, radius and type scale read as consistent with the core four.
  - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [ ] 15. Build, verify on-device, document
  - `dotnet build -f net10.0-android` and `-f net10.0-ios` clean at 0 warnings; full `dotnet test`
    green (no view-model behaviour changed).
  - Deploy to the Redmi Note 11 with `adb reverse` for both `5055` and `5193`; walk every screen in
    both themes; confirm zero `FATAL EXCEPTION` in logcat.
  - Capture `adb shell screencap` of Home, Catalog, Product and Cart in both themes; compare against
    the storefront at `localhost:5193`.
  - Measure the APK delta against the pre-redesign baseline and confirm it is under 500 KB.
  - Record any motion effect degraded to static under Requirement 6.3, and update
    `mobile_app/plan.md` to point at this spec.
  - _Requirements: 2.4, 6.3, 7.6, 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_
