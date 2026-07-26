## What's changed

`Net.Agora.Whiteboard.iOS` advances to **2.16.137.2**, still built from netless-io/Whiteboard-iOS at
the pinned tag 2.16.137.

### Awaitable operations

Four completion-block methods gained awaitable forms: `JoinRoomAsync`, `SetWritableAsync`,
`SetScenePathAsync` and `DisconnectAsync`.

`JoinRoomAsync` is the one that **faults** on failure — its completion carries three values
(success, room, error), which is the shape the binding generator turns into an `NSError`-throwing
task. The two-value ones complete with a `(bool, NSError?)` tuple instead. The difference is
recorded in the ApiDefinition next to each method, since it is not obvious from the call site.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.iOS` | 2.16.137.2 | netless-io/Whiteboard-iOS 2.16.137, built from source |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.

Reminder for consumers: the package ships `Whiteboard_Whiteboard.bundle` as content plus a
`buildTransitive` targets file that declares it as a `BundleResource` in your app. The library looks
the bundle up by that exact name, so referencing the package with `ExcludeAssets="build"` gives a
blank board with no error. Referencing `Net.Agora.Fastboard.iOS` alongside this package is not
supported — Fastboard's static library already contains Whiteboard's objects and both declare the
same bundle.
