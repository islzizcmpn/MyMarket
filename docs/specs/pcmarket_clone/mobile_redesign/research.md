# Codebase Research

Findings gathered while implementing this spec. Written for the next run of the implementation
command — read this before touching `Resources/Styles/`.

Last updated 2026-08-15, after Tasks 1–2.

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

8 brushes. **Not yet registered in `App.xaml`** — that is Task 3.

| Key | Type | Source token |
|---|---|---|
| `BrushAccent` | Linear 135° | `grad-accent` — carries white text |
| `BrushAccentHot` | Linear 90° | `grad-accent-hot` — never has text on it |
| `BrushGlowMedia` | Radial | `glow-media` — behind product imagery |
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

---

## Constraints discovered — read before editing XAML here

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

---

## Open findings for later tasks

### The template's default shadow is WHITE — affects Task 9

`Styles.xaml:298` declares an **implicit** `<Style TargetType="Shadow">` (no `x:Key`, so it applies
globally) with `Brush` bound `Light=White, Dark=White`. Against the charcoal ramp that renders as a
pale halo, not a shadow.

Since `Styles.xaml` stays untouched, Task 9 must override it with its own implicit `Shadow` style in
a dictionary merged later. Do not attempt to fix this by editing the template.

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

### Page background comes from `OffBlack`, which now maps to `surface` — affects Task 7

Measured on device after Task 3: the template binds `ContentPage` background
`Light=White, Dark=OffBlack`, so the page renders `#F2F1EF` light / `#17171B` dark.

The dark value is the **surface** step, not `TokenBg` (`#101013`), because `OffBlack` also serves as
the bar background. Requirement 3.1 asks for the page to sit on `bg`, so Task 5 or Task 7 should set
the page background explicitly to `TokenBg` and leave `OffBlack` doing bar duty. Retuning `OffBlack`
down to `#101013` instead would flatten the bars into the page.

### Views are already clean — Requirement 1.6 is nearly free

Every `.xaml` under `Views/` contains **zero literal hex colours** today, and none reference a
`Colors.xaml` key directly. They consume only `AppStyles.xaml` styles (`Card`, `H1`, `H2`,
`MutedText`, `Price`, `ErrorText`, `SuccessText`, `PrimaryButton`, `GhostButton`, `Quantity`) and the
converters (`Money`, `Date`, `OrderStatus`, `Not`, `HasText`, `Attributes`).

**Consequence for Task 5:** rewriting those ten style keys in `AppStyles.xaml` re-skins all 18 views
without editing them. Keep the key names — renaming any of them means touching every view.

### `Platforms/Android/Resources/values/colors.xml` still holds template purple

```xml
colorPrimary #512BD4 / colorPrimaryDark #2B0B98 / colorAccent #2B0B98
```

This drives the Android splash and native chrome, and is **not** reached by `Colors.xaml`. Task 7
(shell and system bars) needs to retune it, or the launch screen will flash purple before the themed
UI paints.

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

Measured after a correct deploy (Redmi Note 11, Android 11):

| Sample | Dark | Light | Source |
|---|---|---|---|
| Page background | `#17171B` | `#F2F1EF` | retuned `OffBlack` / `White` |
| Card surface | `#1B1B1F` | `#FFFFFF` | `AppStyles.xaml` `CardDark`/`CardLight` — unchanged, Task 5 |
| Tab label (active) | — | `#B5241A` | retuned `Magenta` |
| Tab bar | `#000000` | `#FFFFFF` | Shell default — Task 7 |

Both theme paths matter: `AppThemeBinding` selects a different key per theme, so a missing or
mistyped `*Light` companion only fails in one of them. Switch with
`adb shell "cmd uimode night yes|no"` and relaunch.

`BrushHeroPanel` is the useful canary for dictionary ordering — it is the only brush that references
`Colors.xaml` keys, so if `Brushes.xaml` were ever merged ahead of `Colors.xaml`, it would throw at
launch. It resolves, which proves the order in `App.xaml` is right.
