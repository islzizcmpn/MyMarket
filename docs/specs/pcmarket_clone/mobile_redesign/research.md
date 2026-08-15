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

## Open findings for later tasks

### The template's default shadow is WHITE — affects Task 9

`Styles.xaml:298` declares an **implicit** `<Style TargetType="Shadow">` (no `x:Key`, so it applies
globally) with `Brush` bound `Light=White, Dark=White`. Against the charcoal ramp that renders as a
pale halo, not a shadow.

Since `Styles.xaml` stays untouched, Task 9 must override it with its own implicit `Shadow` style in
a dictionary merged later. Do not attempt to fix this by editing the template.

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
| On-device render | ✖ **not yet** — this is Task 3 |

`Brushes.xaml` is compiled and parse-validated by the MAUI SDK glob even though `App.xaml` does not
merge it yet, so its syntax is already proven. What is *not* proven is `StaticResource` resolution at
runtime, which only a device launch shows. That is precisely why Task 3 exists as its own checkpoint.
