## What's changed

`Net.Agora.Fastboard.iOS` advances to **1.4.5.2**, still built from netless-io/fastboard-iOS at the
pinned revision behind tag 1.4.5, through the `AgoraFastboardBridge` Swift shim.

### Awaitable operations

`SetWritableAsync` is added — the one completion-block method on the shim's surface. `Join` has no
completion block, so there is nothing to await there.

### Fixed: the package could not be linked on .NET 8

An app targeting `net8.0-ios18.0` failed to link against 1.4.5.1 with undefined
`__swift_FORCE_LOAD_$_swiftCompatibility56` and `__swift_FORCE_LOAD_$_swiftCompatibilityConcurrency`.
This is the only package here whose native payload contains Swift — Fastboard is a Swift pod, and so
is the shim over it — and its `NativeReference` did not ask for the Swift runtime's search paths.

It went unnoticed because the .NET 9 and 10 iOS SDKs link the Swift system libraries unconditionally
(their own runtime now depends on Swift), so those two bands linked for a reason unrelated to
Fastboard. The .NET 8 SDK does it only when a native reference asks, and now this one does. No API
or native payload change; `net9.0-ios18.0` and `net10.0-ios26.0` are unaffected.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Fastboard.iOS` | 1.4.5.2 | netless-io/fastboard-iOS 1.4.5, built from source via AgoraFastboardBridge |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.

Reminder for consumers: the package ships `Fastboard_Fastboard.bundle` and
`Whiteboard_Whiteboard.bundle` as content plus a `buildTransitive` targets file that declares them as
`BundleResource` items in your app; `ExcludeAssets="build"` gives a blank toolbar with no error.
Referencing `Net.Agora.Whiteboard.iOS` alongside this package is not supported — this static library
already contains Whiteboard's objects and both declare the same bundle.
