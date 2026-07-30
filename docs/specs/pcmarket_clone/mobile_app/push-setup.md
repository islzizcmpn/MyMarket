# Enabling push notifications

Order-status push is fully plumbed end to end **except** for the two pieces that need a Firebase account:
the device's registration token, and the server's outbound send. Both are single, isolated
implementations behind interfaces. Everything between them — the device registry, the API, the
notification channel, and the Hangfire fan-out — is real and covered by tests.

## What already works

| Piece | Where | Status |
| --- | --- | --- |
| Device registry | `DeviceToken` entity, `AddDeviceTokens` migration, unique index on `Token` | Done |
| Register / unregister API | `POST`/`DELETE /api/v1/users/me/device-tokens` (`UserEndpoints.cs`) | Done, idempotent |
| App-side registration | `Services/PushRegistrar.cs` — registers after sign-in, drops on sign-out | Done |
| Notification channel | `PushNotificationChannel` resolves the recipient's tokens and delegates | Done |
| Outbound sender seam | `IPushSender` (`Application/Abstractions/Messaging`) | Done |
| **Device token source** | `Services/PushTokenSource.cs` — returns null | **Stub** |
| **Outbound sender** | `Infrastructure/Messaging/LoggingPushSender.cs` — logs, reports success | **Stub** |

Both stubs are deliberate no-ops that report success, so an environment with no Firebase project never
fails a notification or burns its retries, and the app runs normally without push.

Verified by `Phase8MobileTests`: registering the same token twice keeps one row, a token that moves to
another account is reassigned rather than duplicated, deletion is safe to repeat, and the push channel
fans out to every registered device while reporting success for a user who has none.

## Why the client side is still a stub

`Xamarin.Firebase.Messaging` (125.1.1, newest as of 2026-07-26) constrains AndroidX to
`Lifecycle 2.9.x` / `Activity 1.10.x` / `Fragment 1.8.8`, while .NET 10 MAUI resolves `Lifecycle 2.11.x` /
`Activity 1.13.x` / `Fragment 1.8.9`. Restoring it fails with a wall of `NU1608` version-constraint errors.
The only way to take the dependency today is to suppress that check and ship an unverifiable AndroidX
version skew — for an SDK that cannot be tested here anyway. Revisit once the Firebase bindings catch up
with .NET 10's AndroidX set.

## Turning it on — client (Android)

1. Create a Firebase project and register an Android app with the package name from
   `<ApplicationId>` in `PcMarket.Mobile.csproj` (currently `uz.pcmarket.mobile` — settle this first; see
   the open question in [plan.md](plan.md)).
2. Download `google-services.json` into `src/PcMarket.Mobile/Platforms/Android/` and mark it as a
   `GoogleServicesJson` build item:
   ```xml
   <ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
     <GoogleServicesJson Include="Platforms\Android\google-services.json" />
     <PackageReference Include="Xamarin.Firebase.Messaging" Version="<version that resolves>" />
   </ItemGroup>
   ```
3. Implement `PushTokenSource.GetTokenAsync` against `FirebaseMessaging.Instance.GetToken()` under
   `#if ANDROID`. Keep returning null when Firebase is unavailable — the "app still runs without push"
   behaviour is a requirement, not a convenience.
4. Request `POST_NOTIFICATIONS` at runtime on API 33+. The permission is already declared in
   `Platforms/Android/AndroidManifest.xml`.
5. Handle incoming messages in a `FirebaseMessagingService` and deep-link the tap to
   `order?id={orderId}` — `PushNotificationChannel` forwards the notification's `Data` dictionary, so the
   order id can be carried there.

Nothing else in the app changes: `PushRegistrar` already calls the token source on every sign-in and
start-up, and unregisters on sign-out.

## Turning it on — server

1. Create a Firebase service account and download its JSON key.
2. Add a `FirebasePushSender : IPushSender` in `PcMarket.Infrastructure/Messaging` using the
   `FirebaseAdmin` package — already pinned in `Directory.Packages.props` (3.1.0), inert until referenced.
3. Swap the registration in `Infrastructure/DependencyInjection.cs`:
   ```csharp
   services.AddSingleton<IPushSender, FirebasePushSender>();  // was LoggingPushSender
   ```
4. Supply the credentials by environment variable (`GOOGLE_APPLICATION_CREDENTIALS`, or a path setting
   read alongside the other `Notifications` options) and keep the key out of the repo.

`PushNotificationChannel` needs no change — it already resolves recipients and calls `IPushSender`. The
`Notifications:Push` feature flag continues to switch the whole channel off.

## Pruning stale tokens

FCM rejects tokens for uninstalled apps. When the real sender lands, treat an `UNREGISTERED` /
`INVALID_ARGUMENT` response as a signal to delete that `DeviceToken` row — `LastSeenAt` is already
maintained on every re-registration, so a periodic Hangfire sweep of long-unseen tokens is the other
half of that story.
