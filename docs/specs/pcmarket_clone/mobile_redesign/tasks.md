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

- [x] 9. Motion layer
  - `BrandEasing` implementing the web's two cubic-bezier curves numerically; `MotionGate` reading the
    OS animation scale; `CardLift` and `RevealBehavior` attached behaviours. **Done** — all four under
    `Services/Motion/`, with the Android half of `MotionGate` in `Platforms/Android/` following the
    `SystemBars` partial-method shape, so the other heads compile the call away and motion stays on.
  - All behaviours cancel on `Unloaded` — animating a torn-down view throws. **Done**, on `Unloaded`
    and again on detach, and each animation is additionally wrapped: in a recycling list the teardown
    can beat the handler, and a lost animation must not surface as a crash.
  - Approximate `--shadow-lift`'s three layers with MAUI's single shadow; record the approximation.
    **Done** — the values live in a new `Resources/Styles/Motion.xaml`, merged fifth, and `CardLift`
    builds the shadow from them, so it is retuned in the dictionary rather than in code. Only the wide
    dark drop survives; the 1px light top edge and the warm brand-tinted layer are the two that read
    as *lit*, so the mobile card reads as raised rather than as picked out by a light. CSS's `-36px`
    spread has no MAUI equivalent either, so the blur is cut in its place instead of being carried at
    full width, and the offset is set to the lift distance so the card appears to leave its shadow
    behind on the page.
  - **The curves are verified, not just transcribed.** A cubic-bezier is a parametric curve, not a
    function of time, so each sample solves `x(t) = progress` by Newton-Raphson with a bisection
    fallback and then reads `y(t)` — the same solve a browser runs. Swept at 1000 points, both curves
    are monotonic and stay in range; `lift-in` is at 0.494 by 10% and 0.972 by half, which is the
    front-loaded easeOutExpo the web tuned for and nothing like `CubicOut`; `lift-out` is symmetric
    about 0.5 to seven places.
  - **Deviation — `TranslateTo` and `FadeTo` are obsolete in MAUI 10.** design.md names both, and
    under the solution's warnings-as-errors they fail the build outright. Ported to
    `TranslateToAsync` and `FadeToAsync`, which are the same methods renamed.
  - **Addition — an implicit `Shadow` style, and it is why `Motion.xaml` is merged last.** The stock
    template declares one globally with `Brush` bound White in *both* themes, which against the
    charcoal ramp is a pale halo rather than a shadow — and being implicit it would have reached the
    lift shadow too. Overridden here rather than by editing the untouched template. Note also that
    MAUI 10 types `VisualElement.Shadow` as non-nullable while its default is no shadow at all, so
    the lift is dropped with `ClearValue`, never by assigning null.
  - **Verified on device, and it took two fixes to get there.** The lift was measured, found dead,
    root-caused twice and re-measured:
    1. **`PointerGestureRecognizer` never fires for touch on Android.** The platform wiring is
       present — `GesturePlatformManager.InitializePointerHandler` exists and
       `PlatformPointerEventArgs` exposes a `MotionEvent` — so this looked safe from the metadata
       alone, and it is not: a finger raises neither `PointerPressed` nor `PointerReleased` on the
       Redmi Note 11. A `Button`'s `Pressed`/`Released` pair does. `CardLift` therefore no longer
       listens for anything; it exposes `Raise()` and `Release()` and the view drives it.
    2. **The lift was cancelling itself through its own `Unloaded` handler.** Assigning `Shadow`
       re-attaches the platform view when the card is inside a `CollectionView`, which raises
       `Unloaded` for the card *under the finger* — with `Handler` and `Parent` both still live.
       Design.md's "cancel on Unloaded" rule then reset the card one frame into the rise, so the
       animation ran to completion against a translation that had already been zeroed. Proven by
       removing the shadow assignment: `TranslationY` went from 0 to -16 with nothing else changed.
       `Unloaded` alone is not a teardown signal here; a null `Handler` is, and that is what the
       behaviour now tests.
    Measured after both fixes: the pressed card's artwork rises **42 px (15.3 dp against 16 dp
    expected, the 2 px being the anti-aliased edge row)**, the neighbouring card in the same row
    moves **0 px**, and after press → navigate → back the card is at its exact resting position, so
    nothing is stranded raised. Zero `FATAL EXCEPTION` throughout.
  - **Deviation — the input surface is a `Button` at `Opacity="0"`, not a transparent one.** A
    Button still paints its platform pressed state, and stretched over a whole card that state is a
    square black scrim: measured, it greyed the artwork from `#EEF0FF` to `#B5B7C2` and squared off
    the card's rounded corners for as long as the finger was down. Android hit-tests on bounds and
    ignores alpha, so a fully transparent Button receives every event while painting nothing. The
    lift is the press feedback; the button has no business drawing its own.
  - `RevealBehavior` hides an item for its entrance, so it arms off `HandlerChanged` as well as
    `Loaded`: an entrance that never armed would leave the item invisible for good, which is not a
    failure worth saving one hook over.
  - _Requirements: 6.1, 6.2, 6.4, 6.5_

- [x] 10. Shared `ProductCardView`
  - `ContentView` with a bindable `Product`, implementing the card from design.md: media panel on
    `media-bg` with the glow brush, name / brand / price, `grad-accent` CTA, `CardLift` attached.
    **Done.** The glow spans the whole card rather than sitting inside the media panel, which is what
    the ported brush is centred for: product photography is opaque, so a light behind the image would
    simply be hidden by it, and a core on the image's lower edge is what lets the spill read across
    the card body. The `vignette-media` fade layer sits over the image; its second layer is left off,
    the reduction `Brushes.xaml` already names as valid.
  - Static card carries a `line` border rather than a shadow; the shadow appears only when lifted,
    because per-card shadows in a long list are measurably expensive on Android. **Done** — the
    `Card` style was already shadowless for exactly this reason, and `CardLift` is the only thing in
    the app that ever sets one.
  - **Deviation — the card carries a second bindable property, `Command`.** design.md lists `Product`
    alone, which leaves the host to attach the tap on the outside while the lift is driven from
    inside. Android dispatches touch to the innermost view that takes it, so that split makes "does
    tapping a card open the product" a dispatch question rather than a decision. One invisible
    `Button` over the card now reports press, release *and* click, so the three can never disagree.
    The card still decides nothing: the host supplies the command and the card passes its `Product`
    as the parameter. Verified on device — the tap opens the product page.
  - **Deviation — the CTA reads "View", not "Add to cart".** design.md's diagram carries that label
    over from `ProductCard.razor`, where the button is a *ghost* at rest and the card's hover rule is
    what fills it with the accent. Mobile has no hover; the press is the equivalent, and the press
    opens the product. A real add-to-cart here would need the quick-add round trip the web card
    makes, which is a new capability rather than a restyle and would put a network call behind a
    view — against this slice's own presentation-only scope. So the bar is painted `grad-accent` as
    Task 10 asks, reads what the tap actually does, and is deliberately not a `Button`: a Button
    there would swallow the tap and split one action into two.
  - **Deviation — the media panel is a fixed 150 high, not 1:1.** MAUI has no aspect-ratio, and the
    usual workaround binds `HeightRequest` to the element's own `Width`, which sets a request during
    arrange and forces a second measure pass *per card* inside a virtualizing list. Against this
    spec's own warning about per-card cost on the target device, a fixed height that reads square
    enough at the two-column width is the better trade. It is not square at every screen width.
  - _Requirements: 4.3, 6.1_

- [x] 11. Restyle Home
  - Hero block with remote banner artwork under the `hero-bloom` and `hero-vignette` brushes,
    replacing the plain "Build your PC" card. **Done** — four layers bottom to top: `BrushHeroPanel`,
    the remote banner, bloom, vignette, with the copy over them. The panel is not decoration behind an
    image that always arrives, it *is* the Requirement 7.4 fallback: an unreachable storefront draws
    no image and the gradient underneath is what remains, copy still legible, layout unmoved. Bloom
    under vignette as on the web, so the light reads as falling inside a darkened frame rather than as
    a wash laid over one. Hero copy sits on `White` rather than `ink`, because the artwork behind it
    is a dark photograph in both themes and must not follow the page.
  - **Addition — `BrushHeroScrim`, because the hero was unreadable once real artwork loaded.** With
    the banner finally painting, the headline landed on a lit monitor and the bloom and vignette that
    Task 11 names are effects, not legibility layers. The storefront solves this with a third overlay
    layer this port had skipped. Added to `Brushes.xaml` and **rotated**: the web runs it 90deg
    because its hero is a 21:8 band with copy in the left third, while this hero is a full-width
    228dp block with copy along the bottom, so a left-to-right ramp would darken an empty left edge
    and leave the headline over the bright half. Same stops and colour, turned bottom to top, and
    placed under the bloom exactly as the web orders them. Verified by recovering the alpha from
    device pixels — 0.85 / 0.81 / 0.43 / 0.29 / 0.15 bottom to top, tracking the CSS .93 / .80 / .45
    / .14 ramp across the full hero.
  - Category tiles using remote category artwork instead of text chips. **Done** — a new
    `CategoryArtConverter` resolves the tile from the slug through `Artwork.Category`, which carries
    the storefront's stand-in map, so Computers shows the same photograph on both clients instead of
    404ing on the naive rule. Each tile is a `MediaPanel` inside a `Card`, so a category with no art
    shows the token surface rather than nothing.
  - New arrivals switched to `ProductCardView` with staggered reveal. **Done** — a two-column
    `GridItemsLayout` with `RevealBehavior` on each card. The stagger comes from a batch clock rather
    than an item index: containers loading within 150 ms of each other are one batch and step 70 ms
    apart, capped at six steps. An index would have to be passed in, which a `DataTemplate` cannot
    do; the clock also handles a later page appended by paging, which starts a fresh batch and enters
    at once instead of inheriting a stale delay.
  - Also replaced the bare search `Entry` with an `InputField` frame carrying the `brand` focus ring,
    wired with the `DataTrigger` shape research.md documents — Requirement 5.2's pattern, proven here
    before Tasks 12 to 14 depend on it. Section labels are set in sentence case with
    `TextTransform="Uppercase"` rather than typed in caps, so the localization slice can translate the
    word without inheriting English casing.
  - **Addition — `OpenCatalogCommand` on `HomeViewModel`.** Requirement 4.1's hero needs a call to
    action, and a view cannot navigate on its own. It is a one-line static command in the exact shape
    of the `OpenProductAsync` and `OpenCategoryAsync` already there — no state, no existing behaviour
    touched — so Requirement 9.6 still holds and the suite is green at 136 tests.
  - **The hero's image source is assigned in code-behind, and that is the second bug this task hid.**
    `x:Static` cannot supply a nullable `ImageSource`: the XAML source generator dereferences the
    extension's result to stamp a namescope on it, so a nullable return fails CS8602 under
    warnings-as-errors — *inside generated code*, pointing at a path that does not exist on disk until
    `EmitCompilerGeneratedFiles` is switched on. The obvious workaround,
    `{Binding Source={x:Static services:Artwork.Banner}, Converter={StaticResource RemoteImage}}`,
    builds clean and then **yields nothing**: a Binding whose Source is a plain string and whose Path
    is left implicit produces no value under SourceGen. On device the hero silently kept the gradient
    panel that is supposed to be its *failure* state, and `banner-dark.jpg` was never requested at all
    — proven by its 156 496 bytes never appearing in the platform image cache while the two category
    tiles did. `HomePage` now assigns `HeroImage.Source` directly. The banner is cached and painting.
  - **A gradient fallback that looks deliberate hides its own failure.** The hero looked finished for
    three deploys while never fetching its artwork, because the fallback is a designed panel rather
    than an empty box. Anything binding decorative artwork should be checked against the platform
    image cache, not against the screenshot.
  - _Requirements: 4.1, 4.2, 4.3, 6.2_

- [x] 12. Restyle Catalog and Product
  - Catalog: filter/sort pills on `surface-2` with `grad-accent` active fill; two-column grid of
    `ProductCardView`; paging preserved. **Done** — a category rail and a sort rail, both horizontally
    scrolling so neither assumes how long a translated label runs, over a two-column
    `GridItemsLayout` with `RevealBehavior` on each card. Brand and price range stay behind the
    Filters toggle: they are a list of dozens and a pair of numbers, and neither becomes a rail of
    chips without making the screen worse. The search box gained the `InputField` frame and focus
    ring, as did both price fields.
  - Product: image area on `media-bg` with the media glow, variant selector and add-to-cart on
    `grad-accent`. **Done** — the image area now stacks the same three layers as the card (glow,
    artwork, vignette fade) at 260dp, and the variant `Picker` became a pill rail. A dropdown hides
    every option but one behind a tap, and on a product page the options are the decision.
  - **Deviation — sort is pills, not the picker design.md sketches.** design.md's Catalog diagram
    shows `Sort: [ Newest v ]` on `input-bg`, while Requirement 4.4 and this task both say filter
    *and sort* are pill controls. The requirement wins; the ASCII is the outlier.
  - **The four sort options are written out rather than bound to the enum.** Binding them needs a
    converter turning `ProductSort` into display text — exactly the "literal copy baked into styles or
    converters" that requirements.md's localization assumption rules out. In a view the strings sit
    where every other string on the screen does. Copy matches the storefront's own labels.
  - **Addition — three view-model commands**, because a pill needs something to invoke:
    `SelectCategory` and `SelectSort` on `CatalogViewModel`, `SelectVariant` on `ProductViewModel`.
    `SelectCategory` also clears the search, and that is not tidiness: a query runs the FTS endpoint
    and ignores every filter, so a category chosen while a search was live would light up and change
    nothing. Additive only, so Requirement 9.6 holds and the suite is green at 136.
  - **Addition — `SelectionConverter` (`IsSelected`), an `IMultiValueConverter`.** A pill inside a
    template has to compare *itself* against a property on the view model, which is two values, and a
    single-value converter can only see one. The alternative is a wrapper type per list carrying an
    `IsSelected` flag, which puts selection state into the view models this slice must not change.
  - **`DataTrigger` with `Value="{x:Null}"` does not fire.** Measured: with no category selected the
    "All" pill stayed on `surface-2` while every other pill behaved. "Nothing is selected" now goes
    through an `IsNullConverter` instead. Worth knowing before Tasks 13 and 14 write another one.
  - **The media glow shipped at three times its intended strength, and this task is what exposed it.**
    Task 10 ported `glow-media`'s gradient but not the `opacity: .34` and `scale(.84)` the web applies
    to it at rest — the swell to full opacity belongs to a hover state mobile does not have. On Home
    the card bodies were below the fold, so nothing looked wrong; the moment Catalog put whole cards
    on screen, every card body was washed red. Fixed in `ProductCardView` and on the product page.
    Measured after: card body `#21191C` to `#1C181B`, a warm lift off `surface` `#17171B`, where
    before it was a saturated red-brown.
  - **Verified on device**, dark theme:
    | Check | Result |
    |---|---|
    | Exactly one category pill lit, swaps on tap | ✔ `#C4291E` active, `#1D1D22` inactive |
    | Exactly one sort pill lit, swaps on tap | ✔ same values |
    | Category filter actually filters | ✔ Computers drops the accessories product |
    | Sort actually sorts | ✔ price ascending, 650 000 then 7 500 000 |
    | Variant pill swaps, dependent fields follow | ✔ Gray/25 in stock to Black/40 in stock |
    | Two-column grid, cards render | ✔ |
    | Zero `FATAL EXCEPTION` | ✔ |
  - **Paging is preserved in code but could not be exercised.** The seeded catalogue holds 3 products
    in 1 page, so `RemainingItemsThresholdReachedCommand` never fires against this data. The wiring is
    carried over unchanged from the previous `CollectionView`; it wants a seed with more than 20
    products, or Task 15 against a fuller catalogue.
  - Light theme for these two screens is not re-checked here — Task 15.
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
