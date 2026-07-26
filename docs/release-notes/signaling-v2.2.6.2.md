## What's changed

`Net.Agora.Signaling.iOS` advances to **2.2.6.2**. The native SDK is unchanged at AgoraRtm_iOS 2.2.6.

### Awaitable operations

Every RTM operation took an Objective-C completion block, which meant a nested callback per step.
All six now also have an awaitable form:

```csharp
var result = await client.LoginAsync(token);
if (result.ErrorInfo is { ErrorCode: not 0 } error)
    return Fail(error.Reason);

await client.SubscribeAsync(channel, null);
await client.PublishAsync(channel, "hello", null);
```

`LoginAsync`, `LogoutAsync`, `RenewTokenAsync`, `SubscribeAsync`, `UnsubscribeAsync`, `PublishAsync`,
each returning `Task<AgoraRtmOperationResult>` — the response/error pair the block delivered.

The task **completes rather than faults** on an RTM error, deliberately: the SDK answers some
successes with a non-nil `errorInfo` whose `errorCode` is 0, so "error object present" is not
"failed". Check `result.ErrorInfo?.ErrorCode`. Verified end to end on the simulator, where
`LogoutAsync` without a login delivers code −10002 as data rather than as an exception.

The callback-taking methods are untouched — these are additions. `addDelegate:`/`removeDelegate:` are
now bound too, so a listener can be attached without owning client construction.

### The device slice is 32-bit-free

Agora still builds the RTM device slice as `ios-arm64_armv7`. .NET for iOS has never supported
32-bit — it has been arm64-only since .NET 6 — so that architecture was unreachable dead weight,
repeated once per packed target framework. The fetch script now thins it with `lipo`, renames the
slice to `ios-arm64` and rewrites the manifest to match. **The package drops from 32 MB to 25 MB.**

Verified by building a device (`ios-arm64`) app against it: it links, and the embedded
`AgoraRtmKit.framework` is arm64-only.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.iOS` | 2.2.6.2 | AgoraRtm_iOS 2.2.6 (AgoraRtmKit + aosl) |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
