# Codebase Research

Findings gathered while implementing this spec. Written for the next run of the implementation
command — read this before touching `Resources/Styles/`.

Last updated 2026-08-26, after Task 12 and its on-device verification.

---

## Delivered so far

### `Resources/Styles/Colors.xaml` (rewritten, Task 1)

27 token pairs — 54 `Color` resources. Naming convention: `TokenX` holds the dark value (the
default), `TokenXLight` the light one. Consumers pair them explicitly:

```xml
{AppThemeBinding Light={StaticResource TokenBgLight}, Dark={StaticResource TokenBg}}
```

Available token roots (append `Light` for the companion):

| Group | Keys |
|---|---|
| Surfaces | `TokenBg`, `TokenSurface`, `TokenSurface2`, `TokenMediaBg`, `TokenInputBg` |
| Ink | `TokenInk`, `TokenMuted`, `TokenLine` |
| Brand | `TokenBrand`, `TokenBrand600`, `TokenBrand700`, `TokenBrand200`, `TokenBrand050`, `TokenAccent`, `TokenAccentHot` |
| Status | `TokenOk`, `TokenOk700`, `TokenOk200`, `TokenOk050`, `TokenDanger`, `TokenDanger700`, `TokenDanger200`, `TokenDanger050` |
| Hero / error UI | `TokenHeroFrom`, `TokenHeroTo`, `TokenErrorUiBg`, `TokenErrorUiInk` |

A `*Light` companion exists for **every** token, including the theme-invariant `TokenAccent`, so
consumers never have to check which ones vary.

### `Resources/Styles/Brushes.xaml` (new, Task 2)

9 brushes, merged second in `App.xaml` (Task 3). `BrushHeroScrim` was added in Task 11.

| Key | Type | Source token |
|---|---|---|
| `BrushAccent` | Linear 135° | `grad-accent` — carries white text |
| `BrushAccentHot` | Linear 90° | `grad-accent-hot` — never has text on it |
| `BrushGlowMedia` | Radial | `glow-media` — behind product imagery |
| `BrushHeroScrim` | Linear ↑ | hero overlay layer 3; keeps white copy legible over artwork |
| `BrushHeroBloom` | Radial | `hero-bloom` |
| `BrushHeroVignette` | Radial | `hero-vignette` |
| `BrushVignetteMediaFade` | Linear ↓ | `vignette-media` layer 1 |
| `BrushVignetteMediaEdge` | Radial | `vignette-media` layer 2 |
| `BrushHeroPanel` | Linear 135° | `hero-from` → `hero-to`; flat fallback when remote art fails |

---

### `Resources/Fonts/` (Task 4)

`Play-Regular.ttf` / `Play-Bold.ttf`, registered in `MauiProgram` as `PlayRegular` / `PlayBold`.
`OFL.txt` ships alongside as the licence requires.

**They are subsetted.** The files from `github.com/google/fonts/ofl/play` are 199 KB and 211 KB —
410 KB together, which packed to **+499 KB** of APK and blew Requirement 2.5's 150 KB budget more
than threefold. Google's CDN serves subsetted WOFF2; the repo TTFs carry the full 827-codepoint set.

Regenerate with (fontTools 4.63):

```
python -m fontTools.subset Play-Regular.ttf --output-file=out.ttf \
  --unicodes="U+0000-024F,U+02B0-02FF,U+0400-052F,U+2000-206F,U+20A0-20CF,U+2116,U+2122" \
  --no-hinting --layout-features="kern,liga,clig,ccmp,locl" --drop-tables+=DSIG
```

Result: 52 KB and 53 KB on disk (**74–75% smaller**), **50 KB packed in the APK**. Coverage kept at
586 codepoints — Latin 331, Cyrillic 222 — with Greek and Vietnamese dropped. Cyrillic is retained
deliberately: the app is Russian-facing and the localization slice needs it.

`--no-hinting` is the big lever, not the glyph cuts. Dropping Greek + Vietnamese removes only ~21% of
codepoints; it is the TrueType hinting tables that dominate, and Android's rasterizer largely ignores
them at this device's ~400 dpi. Verified on-device: the subset renders identically to the full font.

**Type scale.** Play has only 400 and 700, so bold is selected by naming the `PlayBold` *file*, never
by `FontAttributes="Bold"` — that would synthesise a heavier weight on top of an already-bold face.
Hierarchy otherwise comes from size plus `CharacterSpacing` (H1 `-0.4`, H2 `-0.2`; prices stay at 0,
since tightening long digit runs costs more legibility than it buys).

The `MauiFont` glob in the csproj was narrowed from `Resources\Fonts\*` to `Resources\Fonts\*.ttf`
so `OFL.txt` is not bundled as if it were a typeface.

### `Resources/Styles/AppStyles.xaml` (rewritten, Task 5)

The storefront style layer. Contains **no literal colour at all** — every value resolves through a
token, including the text on the accent gradient, which uses the `White` key (the brand's warm
off-white, theme-invariant) because the gradient stays dark red in both themes.

Keyed styles, all reusable by later tasks:

| Key | Target | Notes |
|---|---|---|
| `Card` | Border | `surface` in a `line` hairline, radius 16. No shadow — see Task 9 |
| `MediaPanel` | Border | `media-bg`, no stroke; backdrop for product imagery |
| `InputField` / `InputFieldFocused` | Border | the input frame and its `brand` focus ring |
| `Segmented` | Border | track a pair of pills sits in |
| `StatusBadge` / `StatusBadgeText` | Border / Label | filled pill, brand-tinted by default |
| `H1` `H2` `H3` `MutedText` `Price` `ErrorText` `SuccessText` | Label | type scale |
| `PrimaryButton` `GhostButton` `Quantity` `Pill` `PillActive` | Button | |

Implicit styles it now owns outright: `Page`, `Label`, `Button`, `Border`, `Entry`, `Editor`,
`Picker`, `SearchBar`, `DatePicker`, `TimePicker`, `RadioButton`.

### `Services/ThemeService.cs`, `Services/SystemBars.cs` (new, Task 6–7)

`ThemeService` is a singleton: `Restore()` (start-up), `Apply(AppTheme)` (user choice, persists),
`ApplySystemBars()` (called on `Window.Activated`). Preference key `pcmarket.theme`, values
`dark`/`light`, both read and write in `try/catch`.

`SystemBars` is a partial class with a `static partial void ApplyPlatform` implemented only in
`Platforms/Android/SystemBars.Android.cs`. On the other heads the method has no body and the call
compiles away — no `#if ANDROID` anywhere.

### `Services/AppConfig.cs`, `Services/Artwork.cs` (Task 8)

`AppConfig.MediaRootUrl` sits beside `ApiRootUrl` and has the identical shape — same emulator branch,
same Debug/Release split — so the two never drift: `http://10.0.2.2:5193` on a virtual Android device,
`http://localhost:5193` otherwise, `https://pcmarket.uz` in Release. The existing cleartext exemption in
`network_security_config.xml` is scoped by *host*, not port, so it already covered `:5193` — confirmed,
no change was needed.

`Artwork` is the resolver. Everything decorative goes through it:

| Member | Returns |
|---|---|
| `Url(path)` | `{media root}/images/{path}`; an already-absolute URL passes through untouched |
| `Banner` | `home/banner-dark.jpg` |
| `Category(slug)` | `home/cat-{slug}.jpg`, or the stand-in for slugs that have none |
| `Source(url)` | cached `UriImageSource`, or `null` when the string is not an absolute URI |

`RemoteImageConverter` (registered in `AppStyles.xaml` as `RemoteImage`) is a three-line wrapper over
`Source` so a `DataTemplate` can use it. Bind through it rather than binding a bare string: MAUI's own
string conversion only handles paths that are already absolute, and pins the cache at one day.

**`Category` must mirror the storefront's stand-in map, not just the slug rule.** `Home.razor` resolves
a category ring by trying `cat-{slug}.jpg` on disk and falling back to a named photograph. The mobile
app cannot stat a remote folder, so the map is copied. It currently holds one entry — `computers` →
`feat-case-1.jpg` — and without it the Computers tile 404s while the web shows a photo. Measured
against the running storefront:

```
images/home/banner-dark.jpg       200
images/home/cat-accessories.jpg   200
images/home/feat-case-1.jpg       200
images/home/cat-computers.jpg     404   <- the naive rule
```

Keep it in step with `CategoryFallbackArt` in `Home.razor` whenever art is added.

**Cache validity is split by configuration** — 5 minutes in Debug, 30 days in Release. Design.md asks
for a long validity because the art only changes at deploy cadence, which is right for Release and
actively obstructive in Debug, where replacing a file on the storefront would then not show for a month.

**The fallback is the absence of a source, not a placeholder.** `Source` returns `null` for anything it
cannot parse, and an `Image` with no source draws nothing, so what stays visible is the panel behind it.
That makes "no URL", "malformed URL" and "fetch failed" all land on the same flat token surface, which
is what Requirement 7.4 asks for. It only holds because every remote image is wrapped — see below.

Measured, not assumed. With the device offline and the cache emptied, every product image failed and
each panel read a uniform `#1B1B20` where image content had been — thumbnails and the 220dp product
panel alike — with the layout unmoved and no broken-image glyph. Glide reports the failure as a
warning that goes nowhere:

```
W/Glide: Load failed for [https://placehold.co/...] with dimensions [176x176]
W/Glide: com.bumptech.glide.load.HttpException(Failed to connect or obtain data, status code: -1)
```

Zero `FATAL EXCEPTION`. Restoring the network and relaunching brought all three images back and
repopulated the cache, which is what rules out a code fault masquerading as the fallback.

### Remote images are wrapped, and the wrapper is the fallback

All four (`HomePage`, `CatalogPage`, `CartPage`, `ProductPage`) now sit inside a `MediaPanel` border.
Thumbnails take `StrokeShape="RoundRectangle 12"` and `Padding="6"` over the style's 16; the product
hero keeps 16 and takes `Padding="16"`. Size moved from the `Image` to the `Border`, with the image left
on `Aspect="AspectFit"` to fill what is left.

Task 10's `ProductCardView` should carry this shape rather than reinventing it, and Task 11's hero and
category tiles need the same treatment — a remote image with no token panel behind it has no fallback
at all.

Verified by pixel on the Redmi Note 11, both themes, with the panel's padding ring sampled *next to* a
loaded image so the panel and the artwork are distinguishable:

| Sample | Dark | Light | Token |
|---|---|---|---|
| Media panel | `#1B1B20` | `#EFEDEA` | `TokenMediaBg` |
| Card behind it | `#17171B` | `#FFFFFF` | `TokenSurface` |
| Image content | `#EEF0FF` | `#EEF0FF` | remote artwork painting through |

**Caching is real and observable.** MAUI's Android `UriImageSource` is backed by Glide, whose disk cache
is at `/data/user/0/uz.pcmarket.mobile/cache/image_manager_disk_cache/`. Three entries were written on
the Task 6 deploy and were still serving three days later, across a reinstall — which is the evidence
for Requirement 7.5, and is much easier to read than trying to catch a refetch:

```
adb shell "run-as uz.pcmarket.mobile ls -la /data/user/0/uz.pcmarket.mobile/cache/image_manager_disk_cache/"
```

### How to test the image fallback without bricking anything

Two traps, both hit while proving Requirement 7.4.

**Cutting the network alone proves nothing** — the cache above serves the art regardless, so the screen
looks perfectly healthy. The cache has to go first. Delete *only* the Glide directory:

```
adb shell "run-as uz.pcmarket.mobile rm -rf /data/user/0/uz.pcmarket.mobile/cache/image_manager_disk_cache"
```

This is safe in a way `pm clear` is not. `cache/` is not `files/`, so the FastDev assemblies survive and
the app still launches — see the SIGABRT section above for what `pm clear` does instead.

**Check what the development PC's internet actually rides on before touching the phone's radio.** Cutting
it took the host offline the first time, because the PC was tethered from the same handset. Confirm the
host has its own route first:

```powershell
Get-NetRoute -DestinationPrefix "0.0.0.0/0" | Sort-Object RouteMetric | Select-Object -First 1 InterfaceAlias
netsh wlan show interfaces | Select-String SSID     # and that it is not the phone's hotspot
```

With those two out of the way the test is clean, because **`adb reverse` rides the USB transport and
survives airplane mode**. The device shows zero connected networks while `tcp:5055` still answers, so the
catalogue loads over USB and only the images fail — exactly the case Requirement 7.4 describes, with
nothing else broken to confuse the reading:

```
adb shell dumpsys connectivity | grep -c "state: CONNECTED/CONNECTED"   # 0
adb exec-out 'nc -w 3 127.0.0.1 5055 < /dev/null; echo RC=$?'           # RC=0
```

Once Task 11 puts storefront artwork on screen there is a gentler option that needs no radio at all:
stop the storefront on the host, which fails the decorative set and leaves both the API and the product
photography untouched.

### Product photography does not go through the media root

The seeded catalogue carries absolute `https://placehold.co/...` URLs, so `PrimaryImageUrl` is already
absolute and `Url` passes it straight through. Product imagery gains caching and the panel fallback from
this task, nothing more; only the decorative set resolves against `MediaRootUrl`.

### `Services/Motion/` and `Resources/Styles/Motion.xaml` (Task 9)

Four files under `Services/Motion/`, namespace `PcMarket.Mobile.Services.Motion`, plus a fifth merged
dictionary. XAML reaches the behaviours through
`xmlns:motion="clr-namespace:PcMarket.Mobile.Services.Motion"`.

| Member | Shape | What it is |
|---|---|---|
| `BrandEasing.LiftIn` | `Easing` | `cubic-bezier(.16, 1, .3, 1)` — the rise |
| `BrandEasing.LiftOut` | `Easing` | `cubic-bezier(.65, 0, .35, 1)` — the settle |
| `BrandEasing.Reveal` | `Easing` | alias of `LiftIn`, as the web's reveal transition is |
| `BrandEasing.CubicBezier(x1,y1,x2,y2)` | factory | for any further curve ported from `app.css` |
| `MotionGate.Enabled` | `bool` | false when the OS animation scale is 0 |
| `CardLift` | `Behavior<View>` | `Raise()` / `Release()`; shadow only while raised |
| `RevealBehavior` | `Behavior<View>` | rise 24 and fade in on first load, staggered per batch |

`Motion.xaml` holds `ShadowLift` / `ShadowLiftLight` (colours) and `ShadowLiftOpacity` /
`ShadowLiftOpacityLight` (`x:Double`), plus the implicit `Shadow` style. **It must stay merged last**
in `App.xaml` — see the template-shadow note below.

**A cubic-bezier is not a function of time.** The animation's progress is the curve's `x`; the eased
value is the `y` at that `x`. Each sample therefore solves `x(t) = progress` — Newton-Raphson, eight
passes, falling back to bisection when the derivative goes flat, which it does on the near-vertical
opening of `(.16, 1, .3, 1)`. Swept at 1000 points both curves are monotonic and in range; `LiftIn`
reads 0.494 at 10% and 0.972 at half, `LiftOut` is symmetric about 0.5 to seven places. Substituting
`Easing.CubicOut` would not be close: it is at roughly half distance where the web's curve is past
80%.

**`MotionGate` caches.** The Android read goes through `Settings.Global.AnimatorDurationScale`, which
crosses into the settings provider, and a card list asks on every attach. The cost of the cache is
that flipping the OS setting mid-session takes effect next launch.

**`CardLift` listens for nothing.** It exposes `Raise()` and `Release()` and the view calls them.
See the two device findings below for why.

### TWO DEVICE FINDINGS THAT DECIDE HOW MOTION IS WIRED — read before touching it

Both were found by measuring the lift on the Redmi Note 11 after it shipped looking correct.

**1. `PointerGestureRecognizer` never fires for touch on Android.** The platform wiring is all
present — `GesturePlatformManager.InitializePointerHandler` exists in the Android assembly and
`PlatformPointerEventArgs` exposes a `MotionEvent` — so the metadata says it is supported and the
device says otherwise: a finger raises neither `PointerPressed` nor `PointerReleased`. A `Button`'s
`Pressed` / `Released` pair does. **Do not drive touch feedback from pointer events on this project.**

**2. Assigning `Shadow` raises a spurious `Unloaded`, and design.md's cancel-on-`Unloaded` rule then
eats the animation.** Setting `Shadow` re-attaches the platform view when the element is inside a
`CollectionView`. That raises `Unloaded` for the card *under the finger*, with `Handler` and `Parent`
both still live. Resetting on it cancelled the rise one frame in, so the animation completed against
a translation that had already been zeroed and the lift did nothing at all — while every log line
said it had run. Proven by removing only the shadow assignment: `TranslationY` went 0 → -16.

`Unloaded` is therefore **not** a teardown signal here. `CardLift` resets only when
`view.Handler is null`, which a destroyed container has and a re-attached one does not. Anything else
that cancels on `Unloaded` inside an item template needs the same discriminator.

**The input surface is a `Button` at `Opacity="0"`.** Transparent is not enough — a Button still
paints its platform pressed state, and over a whole card that is a square black scrim: measured, it
took the artwork from `#EEF0FF` to `#B5B7C2` and squared off the card's rounded corners while the
finger was down. Android hit-tests on bounds and ignores alpha, so an `Opacity="0"` Button receives
every event and paints nothing.

**Both behaviours restore the static state on detach** and wrap their animations besides, because in
a recycling list the teardown can beat the handler. `RevealBehavior` arms off `HandlerChanged` as
well as `Loaded`, because it *hides* the item first — an entrance that never armed leaves the item
invisible permanently, which is much worse than arming twice (it is idempotent).

**`TranslateTo` and `FadeTo` are obsolete in MAUI 10** and fail this solution's warnings-as-errors.
Use `TranslateToAsync` / `FadeToAsync`. design.md still names the old ones.

**`VisualElement.Shadow` is typed non-nullable in MAUI 10** although its default is no shadow. Drop a
shadow with `view.ClearValue(VisualElement.ShadowProperty)`; `= null` does not compile.

### `Views/ProductCardView.xaml` (Task 10)

The card, defined once, for Home, Catalog and anything else listing products.

```xml
<views:ProductCardView Product="{Binding .}"
                       Command="{Binding Source={RelativeSource AncestorType={x:Type vm:CatalogViewModel}},
                                         Path=OpenProductCommand}" />
```

Two bindable properties: `Product` (`ProductListItemDto`) and `Command`, which runs on tap with
`Product` as its parameter. Layout, outside in: `Card` border at `Padding="0"` → glow rectangle
spanning the card → `MediaPanel` at `HeightRequest="150"` with the image and the vignette fade over
it → body with name, brand, price, an out-of-stock line, and the accent bar.

**`Command` is a deviation from design.md, and the reason matters for Task 12.** One invisible
`Button` spanning the card reports press, release and click, so all three agree by construction and
nothing depends on how Android dispatches touch between nested views. **Do not go back to putting a
`TapGestureRecognizer` on the outside of this view, and do not give that Button a visible
background.** Verified on device: tapping a card opens the product page.

**The CTA reads "View" and is not a `Button`.** A Button there would swallow the tap and split one
action into two. The web's equivalent is a ghost button that only fills with the accent on hover,
which mobile has no counterpart for; here the whole card is the target and the bar is the affordance.

**The media panel is a fixed 150, not 1:1.** MAUI has no aspect-ratio. Binding `HeightRequest` to the
element's own `Width` is the usual trick and it sets a request during arrange, forcing a second
measure pass per card inside a virtualizing list — exactly the per-card cost design.md warns about.

**The glow spans the card, not the media panel.** `BrushGlowMedia` is centred at 46% of *card* height
for that reason: product photography is opaque, so a light behind the image is simply hidden by it.
Only the `Fade` half of `vignette-media` is applied; `Edge` is the reduction `Brushes.xaml` already
sanctions.

### `Views/HomePage.xaml`, `CategoryArtConverter`, `HomeViewModel.OpenCatalogCommand` (Task 11)

Hero → search → categories rail → two-column new-arrivals grid.

The hero stacks `BrushHeroPanel`, the remote banner, `BrushHeroScrim`, `BrushHeroBloom`,
`BrushHeroVignette`, then the copy. **The panel is the Requirement 7.4 fallback, not decoration** —
with the storefront down the image draws nothing and the gradient is what stays, copy legible and
layout unmoved. Hero copy is on `White`, not `ink`: the artwork is a dark photograph in both themes.

**The scrim is the layer doing a job rather than an effect, and it is rotated from the web's.** The
storefront runs it 90deg, left to right, because its hero is a 21:8 band with the copy in the left
third. This hero is a full-width 228dp block with the copy along the bottom, so a left-to-right ramp
would darken an empty left edge and leave the headline over the bright half. Same stops, same colour,
same falloff, turned to run bottom to top. It goes under the bloom so the bloom reads as light
falling on the darkened area rather than a tint mixed underneath.

Verified on device by recovering the alpha from the pixels — sample the same point with and without
the layer, then `alpha = (before - after) / (before - 8)`:

| Position from bottom | 0.15 | 0.30 | 0.46 | 0.62 | 0.80 | 1.00 |
|---|---|---|---|---|---|---|
| Recovered alpha | 0.85 | 0.81 | 0.67 | 0.43 | 0.29 | 0.15 |
| CSS stops | .93 → .80 → .45 → .14 | | | | | |

One row of the artwork bleeds through at the very bottom edge (a single bluish pixel row at the
rounded-corner clip). Cosmetic, not a coverage gap.

`CategoryArtConverter` (registered in `AppStyles.xaml` as `CategoryArt`) maps a slug through
`Artwork.Category` to a cached source. It is separate from `RemoteImage` because the catalogue
contract carries no image for a category — the file is derived from the slug rather than read off the
DTO.

`OpenCatalogCommand` is the only view-model addition in the whole slice: one static one-liner in the
shape of the navigation commands already on `HomeViewModel`, because Requirement 4.1's hero needs a
call to action and a view cannot navigate on its own.

**Two separate traps sit on the hero's image source; the second one ships silently.**

`x:Static` cannot supply a nullable value to a bindable property: the source generator dereferences
the extension's result to stamp a namescope on it, so `ImageSource?` fails CS8602 under
warnings-as-errors — inside generated code, pointing at a path that does not exist on disk until you
build with `-p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=obj/gen`. That
flag pair is how to read any XAML SourceGen error in this project.

The obvious workaround is worse, because it builds:
`{Binding Source={x:Static services:Artwork.Banner}, Converter={StaticResource RemoteImage}}`
compiles clean and **yields no value**. A Binding whose `Source` is a plain string and whose `Path`
is left implicit produces nothing under SourceGen. `HomePage` assigns `HeroImage.Source` in
code-behind instead.

**Check decorative artwork against the platform image cache, never against the screenshot.** The hero
looked finished for three deploys while never fetching anything, because its fallback is a designed
gradient panel rather than an empty box — the failure state is indistinguishable from success at a
glance. The cache is unambiguous: `banner-dark.jpg` is 156 496 bytes and simply was not there.

```
adb shell "run-as uz.pcmarket.mobile ls -la /data/user/0/uz.pcmarket.mobile/cache/image_manager_disk_cache/"
curl -s -o /dev/null -w "%{size_download}" http://localhost:5193/images/home/banner-dark.jpg
```

**Section labels use `TextTransform="Uppercase"` over sentence-case text** rather than caps typed
into the string, so the localization slice translates the word without inheriting English casing.

### `Views/CatalogPage.xaml`, `Views/ProductPage.xaml` (Task 12)

Catalog is a category rail over a sort rail over a two-column `GridItemsLayout` of `ProductCardView`,
with brand and price range behind the Filters toggle. Product swaps its variant `Picker` for a pill
rail and stacks the card's three media layers at 260dp.

**The pill pattern, which Tasks 13 and 14 should reuse rather than reinvent:**

```xml
<Button Style="{StaticResource Pill}" Command="..." CommandParameter="{Binding .}">
    <Button.Triggers>
        <DataTrigger TargetType="Button" Value="True">
            <DataTrigger.Binding>
                <MultiBinding Converter="{StaticResource IsSelected}">
                    <Binding Path="." />
                    <Binding Path="SelectedCategory"
                             Source="{RelativeSource AncestorType={x:Type vm:CatalogViewModel}}" />
                </MultiBinding>
            </DataTrigger.Binding>
            <Setter Property="Background" Value="{StaticResource BrushAccent}" />
            <Setter Property="TextColor" Value="{StaticResource White}" />
            <Setter Property="BorderWidth" Value="0" />
        </DataTrigger>
    </Button.Triggers>
</Button>
```

A pill has to compare *itself* against a view-model property, which is two values; `IValueConverter`
only sees one. `SelectionConverter` (`IsSelected`) is the `IMultiValueConverter` that does it, and a
`MultiBinding` is legal as a `DataTrigger.Binding` because both derive from `BindingBase`. For a fixed
set — the four sort options — the simpler form works and needs no converter:
`Binding="{Binding Sort}" Value="{x:Static catalog:ProductSort.PriceAsc}"`.

**`DataTrigger` with `Value="{x:Null}"` silently never fires.** Measured on device: the "All" pill
stayed on `surface-2` with no category selected while every other pill behaved correctly. Use
`Converter={StaticResource IsNull}` against `Value="True"` instead.

**Rails are `BindableLayout` inside a horizontal `ScrollView`, not `CollectionView`.** A handful of
items that all fit in memory gains nothing from virtualization, and it sidesteps the
CollectionView-inside-a-ScrollView measuring problem entirely.

**Sort labels are written out in the view, not bound to the enum.** A converter mapping `ProductSort`
to display text is precisely the "literal copy baked into a converter" the localization slice must not
inherit. Copy matches the storefront: `Newest`, `Price: low to high`, `Price: high to low`, `Name A–Z`.

**Three additive view-model commands** — `SelectCategory`, `SelectSort`, `SelectVariant` — because a
pill needs something to invoke. `SelectCategory` clears `SearchText`: a query runs the FTS endpoint and
ignores every filter, so a category chosen during a search would light up and change nothing.

### The media glow needs its rest opacity, and Task 10 shipped without it

`glow-media` is not just a gradient. The web applies it at **`opacity: .34` and `scale(.84)`** at rest
and only swells it to full on hover — a state mobile does not have. Ported at full strength it washes
the entire card red.

It survived Task 10 and Task 11 because Home's card bodies sat below the fold; Catalog put whole cards
on screen and it was obvious immediately. Both `ProductCardView` and `ProductPage` now carry
`Opacity="0.34" Scale="0.84"` on the glow rectangle.

Measured after the fix — card body against `surface` `#17171B`:

| Sample | Before | After |
|---|---|---|
| Card body, upper | saturated red-brown | `#21191C` |
| Card body, lower | | `#1C181B` |

**The general lesson: a ported token is not just its colour.** Check whether the CSS rule applying it
also sets opacity, scale or blend mode before assuming the brush alone is the whole thing.

---

---

## Constraints discovered — read before editing XAML here

### 0. AN IMPLICIT STYLE REPLACES THE TEMPLATE'S — IT DOES NOT LAYER OVER IT

The single most important thing on this page, found during Task 5.

Resource lookup finds **exactly one** implicit style per control type, and `MergedDictionaries` are
searched last-merged-first. So an implicit `Style TargetType="Label"` in `AppStyles.xaml` does not
add to the template's — it **shadows it entirely**, and every setter the template had and yours
does not is simply gone.

Tasks 1–4 shipped this one-setter style:

```xml
<Style TargetType="Label">
    <Setter Property="FontFamily" Value="PlayRegular" />
</Style>
```

which was discarding the template's `TextColor` and leaving every unkeyed label on the Android
platform default. It looked correct only because that default is near-white under night mode — it
would have gone black-on-charcoal the moment the light theme was properly exercised.

**Every implicit style in `AppStyles.xaml` is therefore complete**, carrying colour, size, family
and visual states. When adding a new one, port the template's setters for that type first, then
retune. The template still owns the types not named there — ActivityIndicator, CheckBox, Switch,
Slider, ImageButton, ProgressBar, RefreshView, SearchHandler, Shadow — which inherit the brand
through the retuned keys in `Colors.xaml`, exactly as the "retune, don't rewrite" strategy intends.

The same rule bit the shell: the template's `Shell` style paints the tab bar black in dark theme,
and `Styles.xaml` is merged *before* `AppStyles.xaml`, so a style could have overridden it — but
setting the values directly on the `Shell` element in `AppShell.xaml` outranks any style and is
what Task 7 does.

### 1. XML comments cannot contain a doubled hyphen

Every CSS custom property starts with one, so **the CSS name can never be written verbatim** in a
XAML comment. Both new files name them stripped (`bg`, not the CSS spelling) and say so at the top.

This is not a style preference — it is a hard parse error, `MAUIG1001`, and it fails the build. It
cost one build cycle during Task 2 because the comment *explaining the rule* violated it.

### 2. The stock template pairs keys per theme; it has no per-key light variant

`Styles.xaml` (434 lines, deliberately untouched) writes bindings like
`Light={StaticResource White}, Dark={StaticResource OffBlack}`. A key's role therefore depends on
which side of the pair it sits on, and **two keys serve both sides**:

- `White` — light-theme background **and** dark-theme foreground → must stay near-white
- `Black` — light-theme foreground **and** dark-theme background → must stay near-black

The port works because the brand's warm off-white (`#F2F1EF`) and near-black (`#101013`) are close
enough to serve both duties. **Do not retune `White` or `Black` toward a mid-tone** — it breaks text
contrast in one theme and page background in the other, simultaneously.

Roles of the rest, read off the template's usage:

```
Gray100  dark-theme selected indicator (bright)
Gray200  light placeholder / dark body text  (light grey, appears on both sides)
Gray300  light-theme disabled text  (18 bindings — the most-used pair)
Gray400  light-theme thumb and foreground
Gray500  dark-theme placeholder
Gray600  dark-theme disabled text
Gray900  light-theme body text and titles
Gray950  light-theme emphasis / dark-theme bar background
OffBlack dark-theme bar background
Primary  accent, LIGHT THEME ONLY (template always pairs it Dark=White)
```

Verified after the rewrite: all 18 keys the template references still resolve.

### 3. MAUI `RadialGradientBrush` is always circular

`Radius` is a single scalar. The CSS ellipses (`hero-bloom` at 62%×130%, vignettes at 80%/90%) are
approximated with a circle at the larger extent. **The bloom loses its vertical stretch** — it reads
as a rounder, softer light rather than a column. This is the one unavoidable fidelity loss in the
port; no XAML mechanism avoids it.

### 4. CSS radial stop percentages need rescaling

CSS stops are percentages of the gradient's ending shape; MAUI offsets are 0..1 along `Radius`. Each
brush sets `Radius` to the CSS ending percentage and divides its stops through it — a CSS stop at
30% inside a 66% ending shape becomes offset `0.455`.

### 5. XAML writes alpha first

`rgba(255,122,61,.30)` → `#4DFF7A3D`, not `#FF7A3D4D`. Every precomputed `color-mix()` value in
`Brushes.xaml` follows this.

### 6. A MAUI `Brush` paints one gradient

`vignette-media` is two layered CSS gradients, so it ships as two brushes (`…Fade`, `…Edge`) meant
for stacked elements. If the stack proves costly on the target device, `…Fade` alone carries most of
the effect and dropping `…Edge` is a valid Requirement 6.3 reduction.

---

## CRITICAL — `adb install` alone does NOT deploy your code changes

Discovered during Task 3, after a verification pass silently reported success against **stale
assemblies from the Phase 8 deploy**.

MAUI Debug builds default `EmbedAssembliesIntoApk=false`. The APK is a shell; the managed assemblies
(including everything XAML SourceGen compiles into `PcMarket.Mobile.dll` — which is where
`Colors.xaml` and `Brushes.xaml` end up) are pushed separately by MSBuild's deploy target. So:

```
adb install -r ...-Signed.apk      # installs the shell, keeps whatever assemblies are on device
```

replaces the package while the app keeps loading the *old* assemblies. Everything appears to work.
The app launches, hits the API, renders — with the previous build's resources.

**Deploy with the MSBuild target instead:**

```
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android -t:Install
```

**The tell** is in logcat, and it is easy to dismiss as noise:

```
W monodroid-assembly: open_from_bundles: failed to load bundled assembly Microsoft.Maui.dll
W monodroid-assembly: the assembly might have been uploaded to the device with FastDev instead
```

That second line is literally saying the assemblies come from the fast-deploy directory, not the APK.

**Verify by pixel, not by eye.** A screenshot that "looks dark" proves nothing. Sample a colour that
changed and compare it to the value you shipped:

```powershell
Add-Type -AssemblyName System.Drawing
$b=[System.Drawing.Bitmap]::FromFile($png); $c=$b.GetPixel($x,$y)
"#{0:X2}{1:X2}{2:X2}" -f $c.R,$c.G,$c.B
```

Task 3 used the tab label, which binds the `Magenta` key: `#D600AA` meant stale, `#B5241A` meant the
retune was live. Same glyph, same 992-pixel count, different colour — unambiguous.

### The same staleness bites APK assets — `dotnet clean` before trusting a size measurement

Task 4 hit the sibling problem. Two temporary `Play-*.full.ttf` backups were created, used, and
deleted from disk — but an incremental build had already packaged them, and **deleting the source
did not remove them from the APK**. The archive carried both the full and subset fonts, inflating
the measured delta from 50 KB to 220 KB and making the subsetting look like it had barely worked.

Inspect the archive rather than trusting the total, and `dotnet clean -f net10.0-android` before any
size claim:

```powershell
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z=[System.IO.Compression.ZipFile]::OpenRead($apk)
$z.Entries | Where-Object { $_.FullName -match '\.ttf$' } |
  ForEach-Object { "{0} packed {1:N0}" -f $_.FullName,$_.CompressedLength }
```

Also note: whole-APK deltas across incremental builds are not comparable. Measure a component's cost
from its own zip entries.

### `adb shell pm clear` bricks a Debug install, and `-t:Install` will NOT repair it

Same root cause, third guise. Found in Task 6 while trying to prove the first-run default by wiping
the stored preference. `pm clear` deletes `/data/user/0/uz.pcmarket.mobile/files/`, which is where
FastDev puts the managed assemblies. The app then aborts on launch:

```
F monodroid: No assemblies found in '/data/user/0/uz.pcmarket.mobile/files/.__override__/arm64-v8a'
F monodroid: Assuming this is part of Fast Deployment. Exiting...
F libc    : Fatal signal 6 (SIGABRT)
```

The trap is the recovery: re-running `-t:Install` **does not fix it**. MSBuild tracks what it last
pushed, sees the package still installed at the same version, and skips the assembly push — so the
app keeps aborting and it looks like the code change crashed it. **Uninstall first:**

```
adb uninstall uz.pcmarket.mobile
dotnet build ... -f net10.0-android -t:Install
```

To clear a preference without this, uninstall rather than `pm clear`. The abort message names Fast
Deployment explicitly, which is the tell that separates it from a real crash — a XAML fault shows
up as `XamlParseException` in a managed stack trace, never as a SIGABRT before the runtime starts.

---

## Open findings for later tasks

### How to wire an input's focus ring — affects Tasks 12, 13, 14

`InputField` and `InputFieldFocused` are two separate `Border` styles because a shared style cannot
observe its child's focus: `x:Reference` resolves against the *view's* name scope, and a
`ResourceDictionary` has none. Nor does a `Border` receive focus state from a child — MAUI's
`ChangeVisualState` never propagates. The view wires it, in three lines:

```xml
<Border Style="{StaticResource InputField}">
    <Border.Triggers>
        <DataTrigger TargetType="Border" Binding="{Binding Source={x:Reference Region}, Path=IsFocused}" Value="True">
            <Setter Property="Stroke" Value="{AppThemeBinding Light={StaticResource TokenBrandLight}, Dark={StaticResource TokenBrand}}" />
        </DataTrigger>
    </Border.Triggers>
    <Entry x:Name="Region" Placeholder="Region" Text="{Binding Region}" />
</Border>
```

An unwrapped input is not unstyled — the implicit input styles already sit it on `input-bg` and
give it a `Focused` state tinting the fill with `brand-050`. The wrapper is what adds the `line`
border Requirement 5.2 asks for.

The same "the view owns state that depends on the view model" rule produced the theme toggle:
`Pill` plus a `DataTrigger` filling the selected one with `BrushAccent`. Reuse that shape for the
catalog filter and sort pills in Task 12 rather than inventing a second mechanism.

### RESOLVED (Task 9) — the template's default shadow is WHITE

`Styles.xaml:298` declares an **implicit** `<Style TargetType="Shadow">` (no `x:Key`, so it applies
globally) with `Brush` bound `Light=White, Dark=White`. Against the charcoal ramp that renders as a
pale halo, not a shadow — and being implicit it reaches every `Shadow` in the app, including the lift.

`Motion.xaml` now carries a complete implicit `Shadow` style that outranks it, which is **the reason
`Motion.xaml` is merged fifth** rather than anywhere earlier: resource lookup takes the one implicit
style per type from the last dictionary merged. The template is still untouched. If a later task adds
another dictionary after `Motion.xaml`, that dictionary inherits the obligation.

### Play has no `U+02BB` — affects the RU/UZ/EN localization slice

Uzbek Latin writes *oʻzbek*, *gʻalaba* with `U+02BB` MODIFIER LETTER TURNED COMMA. **Play does not
contain it** — this is the upstream font, not a subsetting loss: the `U+02B0-02FF` range was
explicitly requested and yielded 10 other codepoints but not that one.

`U+2019` RIGHT SINGLE QUOTATION MARK *is* present, and is what most Uzbek text uses in practice.
When the localization slice lands, either normalise Uzbek copy to `U+2019` or accept a system-font
fallback for that single glyph, which will not match Play's geometry.

Present and verified for Russian: `U+0401`/`U+0451` (Ё ё), `U+2116` (№), `U+00A0`. Absent:
`U+2009` THIN SPACE — harmless today, because `Format.Money` groups digits with a plain ASCII space
(`Replace(',', ' ')`), not a thin space. Do not switch it to `U+2009` without re-adding the glyph.

### RESOLVED (Task 5) — page background came from `OffBlack`, which maps to `surface`

The template binds `ContentPage` background `Light=White, Dark=OffBlack`, so the page rendered
`#17171B` dark — the **surface** step, not `TokenBg`, because `OffBlack` also serves as the bar
background. `AppStyles.xaml` now carries its own implicit `Page` style on `TokenBg`, leaving
`OffBlack` doing bar duty. Retuning `OffBlack` down to `#101013` instead would have flattened the
bars into the page. Verified by pixel: page `#101013`, bars `#17171B`.

### Views are already clean — Requirement 1.6 is nearly free

Every `.xaml` under `Views/` contains **zero literal hex colours** today, and none reference a
`Colors.xaml` key directly. They consume only `AppStyles.xaml` styles (`Card`, `H1`, `H2`,
`MutedText`, `Price`, `ErrorText`, `SuccessText`, `PrimaryButton`, `GhostButton`, `Quantity`) and the
converters (`Money`, `Date`, `OrderStatus`, `Not`, `HasText`, `Attributes`).

**Consequence for Task 5:** rewriting those ten style keys in `AppStyles.xaml` re-skins all 18 views
without editing them. Keep the key names — renaming any of them means touching every view.

### RESOLVED (Task 7) — `colors.xml` and the splash held template purple

`colorPrimary` / `colorPrimaryDark` / `colorAccent` are now `#17171B` / `#101013` / `#E0452E`, and
`MauiSplashScreen Color` went `#512BD4` to `#101013`. Neither is reached by `Colors.xaml` — they
drive the launch theme before MAUI paints anything, which is why the cold start flashed violet.

**Still open: `MauiIcon Color="#512BD4"` in the csproj.** That is the adaptive-icon background, not
chrome, so it was left out of Task 7's scope rather than changed silently — but a purple icon on a
charcoal/red app is wrong and wants a decision. Changing it regenerates the launcher icon.

### `*Brush` `SolidColorBrush` resources are unreferenced

The eight `Gray*Brush` plus `PrimaryBrush`/`SecondaryBrush`/`TertiaryBrush`/`WhiteBrush`/`BlackBrush`
entries in `Colors.xaml` are used by nothing — not the template, not the views. Kept and retuned for
now because they are template surface area, but they are removable if the file needs slimming.

---

## Verification status

| Check | Result |
|---|---|
| `dotnet build -f net10.0-android` | ✔ 0 warnings, 0 errors |
| `dotnet build -f net10.0-ios` | ✔ 0 warnings, 0 errors |
| Template keys still resolving | ✔ all 18 present |
| On-device launch, dark theme | ✔ no `StaticResource` / `XamlParse` / `FATAL EXCEPTION` |
| On-device launch, light theme | ✔ same |
| Cross-dictionary resolution | ✔ `BrushHeroPanel` resolves `TokenHeroFrom` from `Colors.xaml` |
| Token values live on device | ✔ verified by pixel sampling, both themes |
| `dotnet test` after Tasks 9–11 | ✔ 136 passed, 0 failed |
| Requirement 1.6, after Tasks 10–11 | ✔ zero literal hex under `Views/` |
| Easing curves | ✔ monotonic and in range over a 1000-point sweep, both curves |
| Card lift on device | ✔ 42 px rise on the pressed card, 0 px on its neighbour |
| Card settle on device | ✔ exact resting position after press, navigate and back |
| Card tap opens the product | ✔ |
| Hero / category artwork fetched | ✔ all three present in the Glide cache |
| Catalog pills, filter and sort on device | ✔ one lit per rail, filtering and sorting both real |
| Product variant pills on device | ✔ swap, and the dependent fields follow |
| Catalog paging | ✘ **unexercisable — seed holds 3 products in 1 page** |
| Light theme after Tasks 9–12 | ✘ **not yet re-checked — Task 15** |
| Frame rate under scroll (Req 6.3) | ✘ **not yet measured — Task 15** |

Measured after a correct deploy (Redmi Note 11, Android 11). Every sample below is an **exact**
match for the shipped token — no approximations:

| Sample | Dark | Light | Token |
|---|---|---|---|
| Page background | `#101013` | `#F5F4F2` | `TokenBg` |
| Card surface | `#17171B` | `#FFFFFF` | `TokenSurface` |
| Shell nav bar | `#17171B` | `#FFFFFF` | `TokenSurface` |
| Tab bar | `#17171B` | `#FFFFFF` | `TokenSurface` |
| Android status bar | `#17171B` | `#FFFFFF` | `TokenSurface` |
| Tab label (active) | `#E0452E` | `#B5241A` | `TokenBrand` |
| Tab label (inactive) | `#8E8D97` | `#6C6B74` | `TokenMuted` |
| H1 heading | `#F2F1EF` | `#16161A` | `TokenInk` |
| Muted body text | `#8E8D97` | `#6C6B74` | `TokenMuted` |
| Error text | `#F36868` | — | `TokenDanger` |
| Primary CTA | interpolates `#B5111D` to `#D4431F` | | `BrushAccent` |

Theme behaviour, all measured rather than assumed:

| Check | Result |
|---|---|
| Toggle repaints every open screen, no restart | ✔ |
| Choice survives `am force-stop` and relaunch | ✔ light restored while OS was in **night** mode |
| First run, no stored value, OS in **light** mode | ✔ renders dark (Req 8.1 and 8.4 together) |
| System bars follow the toggle, icon contrast flips | ✔ both directions |
| Cold start no longer flashes violet | ✔ |

Sampling the tab label is still the sharpest canary: same glyph, same 992-pixel count as Task 3
measured, different colour. `#D600AA` means stale, `#F2F1EF` means pre-Task-7, `#E0452E` means live.

Both theme paths matter: `AppThemeBinding` selects a different key per theme, so a missing or
mistyped `*Light` companion only fails in one of them. Switch with
`adb shell "cmd uimode night yes|no"` and relaunch.

`BrushHeroPanel` is the useful canary for dictionary ordering — it is the only brush that references
`Colors.xaml` keys, so if `Brushes.xaml` were ever merged ahead of `Colors.xaml`, it would throw at
launch. It resolves, which proves the order in `App.xaml` is right.
