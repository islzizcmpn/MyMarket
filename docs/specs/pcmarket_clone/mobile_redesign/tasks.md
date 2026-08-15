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

- [ ] 3. Wire dictionaries and prove them on-device
  - Register `Brushes.xaml` in `App.xaml`'s `MergedDictionaries` **at position 2, directly after
    `Colors.xaml` and ahead of `AppStyles.xaml`** — see the correction note in design.md.
  - Build, deploy to the Redmi Note 11, launch, and confirm no `StaticResource not found`.
  - This is a deliberate checkpoint, not a formality — it costs one deploy and de-risks the next
    eight tasks.
  - _Requirements: 9.1, 9.3_

- [ ] 4. Bundle the Play typeface
  - Add `Play-Regular.ttf` and `Play-Bold.ttf` (OFL) to `Resources/Fonts/`, register in
    `MauiProgram.CreateMauiApp` as `PlayRegular` / `PlayBold`.
  - Set Play as the default face and build the type scale on size + letter-spacing, since Play has no
    weights between 400 and 700.
  - Confirm on-device that Play is actually rendering — registration failure is silent.
  - _Requirements: 2.1, 2.2, 2.3, 2.5_

- [ ] 5. Rewrite `AppStyles.xaml` as the storefront style layer
  - Replace the current 87-line file: `Card`, `H1`/`H2`/`H3`, `MutedText`, `Price`, `ErrorText`,
    `SuccessText`, `PrimaryButton`, `GhostButton`, `Quantity` all rebuilt on tokens.
  - Add the styles the redesign needs: `Pill`, `PillActive`, `MediaPanel`, `StatusBadge`, and input
    styles on `input-bg` with a `brand` focus state.
  - Remove the local `Accent`/`Danger`/`Success`/`Muted`/`CardLight`/`CardDark` colour definitions —
    they are superseded by tokens.
  - _Requirements: 1.6, 5.2, 5.3_

- [ ] 6. Theme service and toggle
  - `ThemeService` with `Apply` / `Restore`, persisting to `Preferences` under `pcmarket.theme`,
    defaulting to dark; register in DI.
  - Call `Restore()` from `App` before the first layout pass so there is no wrong-theme flash.
  - Segmented Dark/Light control on `AccountPage`, wired to `AccountViewModel`.
  - Wrap `Preferences` access in `try/catch` — blocked storage falls back to dark, never blocks start.
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ] 7. Theme the shell and system bars
  - `AppShell.xaml`: tab bar on `surface`, selected `brand`, unselected `muted`; page background `bg`.
  - Android status/navigation bar colours following the active theme, updated when it changes.
  - Themed navigation bar for the pushed routes.
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 8.5_

- [ ] 8. Remote artwork plumbing
  - Add `MediaRootUrl` to `AppConfig` (Debug → `http://localhost:5193`, emulator alias when virtual,
    Release → production storefront over HTTPS) and an `Artwork` resolver.
  - Wire `UriImageSource` with caching; token-coloured container behind every remote image so a
    failure degrades to a flat surface.
  - Document the new `adb reverse tcp:5193 tcp:5193` dev step in `README.md`.
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
