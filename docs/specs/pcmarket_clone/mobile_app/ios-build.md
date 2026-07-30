# Building `PcMarket.Mobile` for iOS

The iOS head is kept compiling as part of every build, but it has never been run: no Mac, no Apple
Developer account, and therefore no signing identity in this environment. This is what a future session
needs to know to take it further.

## What works today

`dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-ios` succeeds on Windows with
0 warnings and 0 errors. That is a **compile only** — it proves the shared code, XAML, and bindings are
valid for the iOS target framework. It does not produce a runnable `.app`, because linking and packaging
an iOS application requires Apple's toolchain, which only exists on macOS.

Keep running this build whenever the app changes: MAUI platform code diverges quietly, and a compile
break here is much cheaper to fix than a discovery later.

## Prerequisites to actually run it

1. **A Mac** — either as the development machine, or paired from Windows via *Pair to Mac* in Visual
   Studio. The Mac needs Xcode installed (matching the `net10.0-ios` SDK's supported Xcode version) and
   must have been opened once so its licence is accepted.
2. **An Apple Developer account** (individual or organisation). The free tier can sign for a personal
   device with 7-day provisioning; TestFlight and the App Store need the paid programme.
3. **A bundle identifier** registered in the Apple Developer portal. The app currently declares
   `uz.pcmarket.mobile` (`<ApplicationId>` in `PcMarket.Mobile.csproj`) — see the open question below
   before registering it, since the identifier is effectively permanent once published.
4. **A signing certificate and provisioning profile** for that identifier, installed in the Mac's keychain.

## Build and run

```bash
# On macOS, simulator (no signing needed):
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-ios \
  -t:Run -p:_DeviceName=:v2:udid=<simulator-udid>

# List available simulators:
xcrun simctl list devices available

# On a physical device (signing required):
dotnet build src/PcMarket.Mobile/PcMarket.Mobile.csproj -f net10.0-ios \
  -p:RuntimeIdentifier=ios-arm64 \
  -p:CodesignKey="Apple Development: <name> (<team-id>)" \
  -p:CodesignProvision="<profile name>" \
  -t:Run
```

## Pointing at the API

`Services/AppConfig.cs` resolves the API root per platform. The Android emulator needs `10.0.2.2`
(its own `localhost` is the emulated device); **the iOS simulator does not** — it shares the host's
network stack, so `http://localhost:5055` is correct there, which is what the non-Android debug branch
already returns.

The Android build permits cleartext HTTP to the development host through
`Platforms/Android/Resources/xml/network_security_config.xml`. iOS has an equivalent gate, App Transport
Security, and blocks plain HTTP by default. Talking to a local HTTP API from the simulator therefore
needs an ATS exception added to `Platforms/iOS/Info.plist`:

```xml
<key>NSAppTransportSecurity</key>
<dict>
  <key>NSAllowsLocalNetworking</key>
  <true/>
</dict>
```

This has **not** been added, because it should not ship in a release build and there is no way to verify
it here. Add it when you first run the simulator, and keep it out of Release.

## Push notifications

iOS push is not implemented. `Services/PushTokenSource.cs` returns null on every platform; on iOS it would
need APNs (an APNs key in the developer portal, the Push Notifications capability, and either a direct
APNs integration or FCM's iOS SDK). See [push-setup.md](push-setup.md) for how the registration pipeline
is wired and the single seam to fill in.

## Deliberately deferred

- Signing, provisioning, and TestFlight/App Store submission — needs an Apple account.
- Any runtime verification on iOS — needs a Mac.
- APNs push.

These were out of scope for the phase (see [plan.md](plan.md)); the parent plan's "Future Considerations"
already lists store submission as deferred until signing accounts exist.
