## What's changed

**Fastboard debuts**: `Net.Agora.Fastboard.iOS` **1.4.5.1** — netless's ready-made UI over the
Interactive Whiteboard, and the odd one out even among the from-source products: what it binds is
not the SDK. Fastboard is Swift, and although its public API is `@objc`-annotated, a Swift `@objc`
class registers under a mangled, module-qualified runtime name (`_TtC9Fastboard8FastRoom`) that a
binding would have to name literally and that changes with the module name. So
`native/AgoraFastboardBridge` — in this repository — wraps it in types with stable `@objc(...)`
names, and that shim is what gets bound. It also flattens the one call that cannot cross into
Objective-C at all: `joinRoom`'s `Result<WhiteRoom, FastRoomError>`, which becomes delegate
callbacks.

`build/build-fastboard.sh` builds the shim's Swift package (which pulls Fastboard and, transitively,
the whiteboard) into `AgoraFastboardBridge.xcframework`. It is self-contained — no dependency on
`Net.Agora.Whiteboard.iOS` — and carries both `Fastboard_Fastboard.bundle` and
`Whiteboard_Whiteboard.bundle` under SwiftPM's names, for the same reason as the whiteboard package.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Fastboard.iOS` | 1.4.5.1 | built from `netless-io/fastboard-iOS` 1.4.5, through this repository's own shim |
