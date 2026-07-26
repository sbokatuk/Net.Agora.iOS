## What's changed

`Net.Agora.Fastboard.iOS` advances to **1.4.5.2**, still built from netless-io/fastboard-iOS at the
pinned revision behind tag 1.4.5, through the `AgoraFastboardBridge` Swift shim.

### Awaitable operations

`SetWritableAsync` is added — the one completion-block method on the shim's surface. `Join` has no
completion block, so there is nothing to await there.

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
