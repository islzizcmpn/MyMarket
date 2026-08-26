| Field | Value |
| --- | --- |
| **Issue Title** | Mobile app refresh spins forever and does not show new data until relaunch |
| **Description** | "When I do the refresh of the app it loads and does not stop and does not refresh immediately.In order for the refresh really happen it requires me to close the app and relaunch ,which is not good" |
| **Created** | 2026-08-26 |
| **Last Updated** | 2026-08-26 |
| **Current Phase** | Validated |
| **Status Summary** | **Resolved and verified on the device.** Root cause: `IsBusy` served as both the TwoWay-bound `RefreshView.IsRefreshing` target and the re-entrancy guard in `BaseViewModel.RunAsync`, so the pull gesture set the guard before the command it triggered ran - the load was skipped and the flag never cleared. Fixed with F3 (private `_running` guard + `Mode=OneWay` on all five bindings) plus the requested tab-reload change (2-minute freshness window replacing the one-shot `_loaded` flag). Verified: three consecutive pulls each fetch cleanly, spinner retracts, Catalog behaves the same, and tab returns refetch once the window expires. |

## Investigation Log

### 2026-08-26 — Investigation started
- Invoked as `fix-it-v1 "When I do the refresh of the app it loads and does not stop and does not refresh immediately..."`.
- Scope confirmed with the user: the **mobile version** = `src/PcMarket.Mobile` (.NET MAUI, Android, run on a physical device via `scripts/start-mobile-dev.ps1`).
- **Restatement:** triggering a refresh in the mobile app starts a loading indicator that never ends. The screen's data is not updated. The only way to see fresh data is to kill the app and start it again.
- Read-only survey of the refresh path completed (no code changed, no commands run against the device).

## Evidence

### E1 — Where refresh is wired (static read, 2026-08-26)
Five screens bind a `RefreshView` the same way:

| Page | Line |
| --- | --- |
| `src/PcMarket.Mobile/Views/HomePage.xaml` | 10 |
| `src/PcMarket.Mobile/Views/CatalogPage.xaml` | 45 |
| `src/PcMarket.Mobile/Views/CartPage.xaml` | 14 |
| `src/PcMarket.Mobile/Views/OrdersPage.xaml` | 14 |
| `src/PcMarket.Mobile/Views/AddressesPage.xaml` | 32 |

All use the identical pattern:
```xml
<RefreshView IsRefreshing="{Binding IsBusy}" Command="{Binding LoadCommand}">
```

### E2 — The busy flag is both the spinner state and the re-entrancy guard
`src/PcMarket.Mobile/ViewModels/BaseViewModel.cs` — `RunAsync` opens with:
```csharp
if (IsBusy) { return; }   // returns WITHOUT ever setting IsBusy back to false
IsBusy = true;
...
finally { IsBusy = false; }
```
`RefreshView.IsRefreshing` is a **TwoWay** bindable property in MAUI. So the same `IsBusy` field is (a) pushed *into* by the native pull gesture and (b) read as the guard that decides whether the load runs. If the gesture's write of `IsRefreshing=true` reaches `IsBusy` before `LoadCommand` executes, `RunAsync` early-returns, no data is fetched, and nothing ever clears `IsBusy` — a permanently spinning indicator with stale data. That is an exact match for the reported symptom.

### E3 — Tab screens only load once per app lifetime
`HomeViewModel` and `CatalogViewModel` gate their `OnAppearing` load behind a `_loaded` field:
```csharp
[RelayCommand]
private Task AppearingAsync() => _loaded ? Task.CompletedTask : LoadAsync();
```
Tab pages are resolved once when the shell is built and live as long as it does (`MauiProgram.AddPage` comment). So revisiting the Home or Catalog tab never re-fetches. Pull-to-refresh is the *only* in-app way to get fresh data on those screens — which explains why a relaunch (fresh view model, `_loaded == false`) is the only thing that works. `CartViewModel` / `OrdersViewModel` have no `_loaded` guard and do reload on appearing.

### E4 — The redesign did not touch the refresh wiring
`git diff` over the uncommitted mobile redesign changes shows `HomePage.xaml`, `CatalogPage.xaml` and `CartPage.xaml` were edited only inside their item `DataTemplate`s (image wrapped in a `MediaPanel` border + `RemoteImage` converter). The `RefreshView` elements and their bindings are untouched. So if this is the pull-to-refresh gesture, it is a pre-existing defect, not a regression from the redesign work.

### E6 — User answers, 2026-08-26
Asked two disambiguating questions in chat:
- *Which "refresh" is failing?* → **"Pull-to-refresh in the app"** — the swipe-down gesture on Home / Catalog / Cart inside the running app.
- *Does any error message appear?* → **"No error, just spins"** — the indicator spins indefinitely, nothing is shown.

Consequences:
- **H3 (Hot Reload) eliminated** — not the dev loop at all.
- **H2 (hung request / dead tunnel) eliminated** — a stalled HTTP call ends at the client timeout and lands in the `HttpRequestException` / `TaskCanceledException` arms of `RunAsync`, which set `Error` and would show a message. Nothing is shown, and `IsBusy` is never cleared, so control never reaches the `finally`. The work delegate is therefore never entered.
- Combined with "data does not update", both halves of the symptom point at the single early-return in `RunAsync`.

### E7 — The first pull after a fresh launch already hangs (user, 2026-08-26)
Asked whether the very first pull-to-refresh after launch sticks, or only later ones. Answer: **"Yes, even the first one."**

This is significant on its own. A freshly resolved view model starts with `IsBusy == false`, and the initial on-appearing load has already completed by the time the user swipes (the list is on screen). So nothing *before* the gesture can have left the flag set — the gesture itself must be what makes `IsBusy` true, which is only possible through the TwoWay write-back of `RefreshView.IsRefreshing`. Directly supports H1; leaves no room for the "stuck from a previous cycle" variant.

### E5 — Dev topology (context)
`scripts/start-mobile-dev.ps1` documents that debug builds on a physical device reach the backend through an `adb reverse` tunnel to `localhost:5055`, and that the tunnel is silently dropped whenever the adb transport resets. A dropped tunnel produces "Can't reach the store..." from the `HttpRequestException` arm of `RunAsync` — an *error*, not an endless spinner — so this is background context rather than a leading hypothesis, but it is worth ruling out.

### E8 - Device reproduction, 2026-08-26 (executes A2)
Run end-to-end by Claude on the attached device - Redmi Note 11 (`2201117SY`, serial `6PQOLVTWMBAQ4LEY`), 1080x2400.

Environment: the Docker stack was already up and healthy (postgres/redis/minio/api/web/admin/nginx). API started on the host with `dotnet run --project src/PcMarket.Api --launch-profile http`; `/health` returned `{"status":"Healthy"}` for all three dependencies. `adb reverse tcp:5055 tcp:5055` was already registered; the device-side probe `nc -w 3 127.0.0.1 5055` returned `RC=0`. Instrumented Debug build installed with `-t:Install`.

**Initial load** (app launched fresh, Home tab): loads correctly, real catalogue data on screen. The trace already shows the re-entrancy, harmlessly:
```
RunAsync entered (IsBusy=False)
IsBusy -> True                     <- set by RunAsync itself
RunAsync entered (IsBusy=True)     <- RefreshView fires LoadCommand again
GUARD HIT -> returning ...         <- harmless here: the outer call is still running
work delegate starting (IsBusy=True)
work delegate finished (IsBusy=True)
finally reached -> clearing IsBusy
IsBusy -> False
```

**Pull-to-refresh** (`adb shell input swipe 540 1200 540 2100 600`, log cleared immediately beforehand). Complete `[FIXIT]` output - four lines, nothing after:
```
[FIXIT] HomeViewModel: IsBusy -> True
[FIXIT] set by:
[FIXIT] HomeViewModel: RunAsync entered (IsBusy=True)
[FIXIT] HomeViewModel: GUARD HIT -> returning without running work, IsBusy stays true (IsBusy=True)
```

The flag is set **before** `RunAsync` is entered, and the stack trace names what set it:
```
at PcMarket.Mobile.ViewModels.BaseViewModel.set_IsBusy(Boolean value)
at PcMarket.Mobile.Views.HomePage.<>c.<InitializeComponent>b__3_16(HomeViewModel source, Boolean value)
at Microsoft.Maui.Controls.Internals.TypedBinding`2[...].ApplyCore(Object sourceObject, BindableObject target,
       BindableProperty property, Boolean fromTarget, SetterSpecificity specificity)
at Microsoft.Maui.Controls.Internals.TypedBinding`2[...].Apply(Boolean fromTarget)
```
`TypedBinding.Apply(fromTarget)` is the **target -> source** direction: the compiled XAML binding for `IsRefreshing` writing back into the view model. `b__3_16` is that binding's generated setter. There is no `RunAsync` frame - the view model did not set its own flag.

No `work delegate starting`, no `finally reached`, no `IsBusy -> False`. The work delegate is never entered and the flag is never cleared.

Screenshots (scratchpad): `01-after-launch.png` shows the loaded Home page; `02-after-pull.png`, taken 8 s after the gesture, shows the refresh spinner still on screen with the identical product list behind it.

**Ordering settled:** in this MAUI version `binding.Apply(fromTarget: true)` runs *before* `RefreshView` executes its `Command`. That is what makes the guard fatal rather than merely redundant.

## Root Cause

`src/PcMarket.Mobile/ViewModels/BaseViewModel.cs` uses the *same* field for two incompatible jobs:

1. `IsBusy` is bound to `RefreshView.IsRefreshing`, a **TwoWay** bindable property - so the UI writes to it.
2. `IsBusy` is the re-entrancy guard `RunAsync` reads to decide whether a load is already running.

The pull gesture sets `IsRefreshing = true`; the TwoWay binding immediately writes `IsBusy = true`; the RefreshView then executes `LoadCommand`; `RunAsync` sees `IsBusy == true`, concludes a load is already in flight, and takes its only exit that does not clear the flag:

```csharp
if (IsBusy) { return; }   // no fetch, and no finally to retract the spinner
```

Both halves of the reported symptom follow from that one line: no fetch (data never updates) and no reset (spinner never stops). A relaunch resolves a fresh transient view model with `IsBusy == false`, which is why only that works.

Affects all five screens that bind the pattern (E1). Compounding it, `HomeViewModel` and `CatalogViewModel` gate their on-appearing load behind a one-shot `_loaded` field and tab pages live as long as the shell (E3), so on those two tabs pull-to-refresh is the *only* in-app path to fresh data.

## Hypotheses

### H1 — `RefreshView.IsRefreshing` TwoWay binding trips `RunAsync`'s own busy guard — **CONFIRMED**
The gesture sets `IsRefreshing = true`; the TwoWay binding writes `IsBusy = true`; `RunAsync` sees `IsBusy` already true and returns immediately without running the work and without resetting the flag. Result: spinner never stops, data never reloads, only a relaunch recovers.
- Supports: E1, E2, E3 (restart-only recovery), E4 (long-standing, not new), E6 (no error ⇒ the work delegate never ran).
- **Confirmed 2026-08-26 by E8** — the device trace shows the write-back setting the flag before `RunAsync` is entered, then the guard firing, then silence.

### H2 — Request hangs (no timeout reached / dead tunnel) — **ELIMINATED** (was Medium)
The load *does* start but the HTTP call never returns promptly, so the spinner keeps spinning until the client timeout. Would still end in an error message rather than an infinite spinner, and a relaunch would not reliably fix it.
- Supports: E5.
- Contradicts: E6 — no error banner ever appears, so the `catch` arms are never reached; relaunch reliably loads fresh data.
- **Eliminated 2026-08-26** by E6.

### H3 — The user means .NET / XAML **Hot Reload**, not the in-app gesture — **ELIMINATED** (was Medium)
"Refresh of the app ... loads and does not stop ... requires me to close the app and relaunch" also reads as a dev-loop complaint: edits do not appear until the app is redeployed. Active redesign work (`docs/specs/pcmarket_clone/mobile_redesign`) makes this plausible. Root cause would be entirely different (Hot Reload not attaching, or the edits being in resource dictionaries/handlers that Hot Reload cannot apply).
- **Eliminated 2026-08-26** by E6: the user confirmed the in-app swipe-down gesture.

### H4 — Data is fetched but the UI does not rebind — **Eliminated** (was Low)
`ObservableCollection` is cleared and repopulated on each load; a cross-thread mutation would throw rather than silently no-op, and `RunAsync` would surface it. Eliminated by E8: the fetch never runs at all, so there is nothing to rebind.

### H5 — The new `RemoteImage` converter blocks the UI thread — **Eliminated**
Considered because the uncommitted redesign added `RemoteImageConverter` and `Services/Artwork.cs`, and a blocked UI thread would leave the native `SwipeRefreshLayout` animation running (it animates off the UI thread) with no error — a superficial match. Reading the code rules it out: `RemoteImageConverter.Convert` only calls `Artwork.Source`, which does string work and constructs a `UriImageSource`. No I/O, no blocking, no `.Result`/`.Wait()`. Eliminated by inspection.

## Approved Actions

### A1 — Disambiguate what "refresh" means
- **Asked:** 2026-08-26. **Answered:** 2026-08-26. Result recorded as E6.

### A3 — Confirm whether the first pull after launch already fails
- **Asked:** 2026-08-26. **Answered:** 2026-08-26. Result recorded as E7.

### A2 — Instrument `RunAsync` and capture one pull-to-refresh from logcat
- **Approved:** 2026-08-26. Instrumentation added (I1), then executed by Claude on the attached device at the user's request ("please proceed yourself"). Result: E8 — H1 confirmed.

## Pending Actions

_(none - A1, A2 and A3 are all complete; the investigation is waiting on a fix decision, tracked under Next Steps.)_

## Fix Options

The two changes below are independent; either alone stops the hang, and they can be taken together.

### F1 - Separate the re-entrancy guard from the bound flag (`BaseViewModel.cs`, 1 file)
Give `RunAsync` a private field no binding can touch, and let `IsBusy` go back to being purely the state the UI observes:
```csharp
private bool _running;

protected async Task RunAsync(...)
{
    if (_running) { return; }
    _running = true;
    IsBusy = true;
    Error = null;
    try { await work(cancellationToken); }
    catch (...) { ... }
    finally { _running = false; IsBusy = false; }
}
```
- **Impact:** fixes all five screens at once. Whatever the UI writes into `IsBusy`, the load still runs and `finally` still retracts the spinner.
- **Risk:** Low. Behaviour is unchanged for every caller that already worked; the guard keeps doing its real job - blocking genuine double-taps and the RefreshView's own re-entrant `LoadCommand`.
- **Verify:** pull-to-refresh on Home; the log must show `work delegate starting` -> `finished` -> `finally reached`, and the spinner must retract.

### F2 - Stop the view from writing view-model state (`Mode=OneWay`, 5 XAML files)
```xml
<RefreshView IsRefreshing="{Binding IsBusy, Mode=OneWay}" Command="{Binding LoadCommand}">
```
in `HomePage.xaml:10`, `CatalogPage.xaml:45`, `CartPage.xaml:14`, `OrdersPage.xaml:14`, `AddressesPage.xaml:32`.
- **Impact:** removes the write-back entirely, so the view model is the only writer of its own flag.
- **Risk:** Low. The view model already drives `IsRefreshing` in the VM -> UI direction, which is the direction that retracts the spinner.
- **Verify:** as F1, plus confirm the spinner still appears during a refresh (it is driven by `IsBusy`, not by the gesture).

### F3 - F1 + F2 together - **recommended**
F1 makes the logic correct at the root; F2 removes the ability of any view to reach into the flag again and reintroduce this. Neither depends on the other, and together they close both directions.

### Out of scope (noted, not proposed)
`HomeViewModel._loaded` and `CatalogViewModel._loaded` make those tabs load exactly once per app lifetime, so revisiting a tab never refetches. That is not the reported defect, but it is why a relaunch was the only workaround. Worth a separate decision about whether tab revisits should refetch.

## Implementation Notes

### I2 - 2026-08-26 - Fix applied (F3 + tab reload)
Instrumentation from I1 removed in the same pass; no `FIXIT` markers remain anywhere in the project.

**F1 - `src/PcMarket.Mobile/ViewModels/BaseViewModel.cs`**
- Added `private bool _running`, used as the re-entrancy guard in place of `IsBusy`. Set immediately before `IsBusy = true`, cleared in `finally` alongside it. Carries a comment naming this issue so the two are not merged again.
- `IsBusy` now has one job: the state the UI observes.

**F2 - five XAML files**, each `RefreshView` binding changed to:
```xml
IsRefreshing="{Binding IsBusy, Mode=OneWay}"
```
`HomePage.xaml:10`, `CatalogPage.xaml:45`, `CartPage.xaml:14`, `OrdersPage.xaml:14`, `AddressesPage.xaml:32`.

**Tab reload** - replaced the one-shot `_loaded` flag with a freshness window in `BaseViewModel`:
- `private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(2);`
- `private DateTimeOffset? _loadedAt;`
- `protected bool IsStale`, `protected void MarkLoaded()`, `protected void Invalidate()`.
- `HomeViewModel` and `CatalogViewModel`: `_loaded` field deleted; `AppearingAsync` is now `IsStale ? LoadAsync() : Task.CompletedTask`; `_loaded = true` became `MarkLoaded()`; `CatalogViewModel.ApplyQueryAttributes` calls `Invalidate()` instead of `_loaded = false`.

**Why a window rather than reloading on every appearance.** `OnAppearing` fires when a pushed page is popped, not only when a tab is switched to. An unconditional reload would therefore reset the catalogue to page 1 and discard the user's scroll position every time they viewed a product and pressed back - a worse regression than the staleness it fixes. Two minutes is long enough to cover a product look-and-return, short enough that a tab genuinely returned to later shows current prices and stock. `CartViewModel` and `OrdersViewModel` are untouched and still load on every appearance, which is correct for them.

Build: `dotnet build -f net10.0-android -c Debug -t:Install` - **succeeded, 0 warnings, 0 errors**.

### I1 — 2026-08-26 — Temporary diagnostic instrumentation (A2)
`src/PcMarket.Mobile/ViewModels/BaseViewModel.cs` only. Every addition is marked `FIXIT-DIAG mobile-refresh-never-completes` and is to be removed together with the fix.

- `Trace(...)` helper writing `[FIXIT] <ViewModelType>: <message> (IsBusy=...)` via `Debug.WriteLine`.
- `RunAsync`: traces on entry, on the guard's early return, before and after the work delegate, and in `finally`.
- `OnIsBusyChanged`: logs each transition plus a `StackTrace`, so the caller that set the flag is named — a `RefreshView` TwoWay binding write-back and `RunAsync` produce visibly different frames.

Verified with `dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-android -c Debug` — **build succeeded, 0 warnings, 0 errors** (57s).

Expected log shape if H1 is correct:
```
[FIXIT] HomeViewModel: IsBusy -> True          <- from the RefreshView binding, NOT RunAsync
[FIXIT] set by: ... Microsoft.Maui.Controls binding frames ...
[FIXIT] HomeViewModel: RunAsync entered
[FIXIT] HomeViewModel: GUARD HIT -> returning without running work, IsBusy stays true
```
and then silence — no "work delegate starting", no "finally reached".

## Verification Results

All checks run by Claude on serial `6PQOLVTWMBAQ4LEY` against the host API on `:5055`, using the API's own request log as the witness that a fetch actually left the device. Screenshots in the session scratchpad.

### V1 - Pull-to-refresh on Home now fetches and retracts - PASS
One `adb shell input swipe 540 1200 540 2100 600` produced, in the API log:
```
GET /api/v1/catalog/categories                          -> 200 in 3.6 ms
GET /api/v1/catalog/products?sort=Newest&page=1&pageSize=10 -> 200 in 7.2 ms
```
`04-fixed-after-pull.png`, taken 6 s later, shows no spinner and the list rendered. Compare `02-after-pull.png` from E8, where the spinner was still turning 8 s in and no request had been made at all.

### V2 - Repeated pulls leave no stuck state - PASS
Three consecutive pulls produced exactly 3 `categories` and 3 `products` requests, zero 4xx/5xx. Under the old code the first pull poisoned `IsBusy` permanently, so pulls 2 and 3 could never have fired.

### V3 - Catalog screen - PASS
Switched to the Catalog tab, pulled: `GET /api/v1/catalog/products?sort=Newest&page=1&pageSize=20 -> 200`. `07-catalog-after-pull.png` shows the list intact and no spinner.

### V4 - Tab return honours the freshness window - PASS
- Returning to Home **within** 2 minutes of its last load: **0** new catalog requests - paging and scroll position preserved, no needless round trip.
- After waiting out the window, hopping Catalog -> Home: **3** requests - `products?pageSize=20` (Catalog), then `categories` and `products?pageSize=10` (Home). Stale tab data is no longer stranded.

### V5 - Instrumentation removed - PASS
`grep -rn "_loaded\|FIXIT" src/PcMarket.Mobile/ViewModels src/PcMarket.Mobile/Views` returns only the new `_loadedAt` / `IsStale` / `MarkLoaded` / `Invalidate` members in `BaseViewModel.cs`.

### Not covered
The verification proves the refresh issues a live request and repopulates the bound collections; it does not mutate catalogue data to watch a specific value change on screen, which would have meant writing to the dev database. If a visible before/after is wanted, change a product name in the admin panel and pull to refresh.

## Next Steps

Issue resolved. Remaining, all optional:

1. **Commit the fix.** It sits alongside the uncommitted mobile redesign work; the two are unrelated and could be committed separately.
2. **Tune `Freshness`** if 2 minutes is not the right trade-off for how the store's stock and prices move. Single constant at the top of `BaseViewModel.cs`.
3. **Consider the same audit elsewhere.** The defect class is "a TwoWay-bound property doubling as control-flow state". Nothing else in the mobile project binds `IsBusy` two-way today, but the `_running` comment is what stops it coming back.

## Environment
- The host API started for this investigation (`dotnet run --project src/PcMarket.Api`) was **stopped** afterwards; port 5055 is free again, as it was at the start. The Docker stack was already running beforehand and was left untouched.
- `adb reverse tcp:5055 tcp:5055` is still registered on the device - harmless, and it is what the dev script sets up anyway.
- The device has the **fixed** Debug build installed.
