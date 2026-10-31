## What's changed

`Net.Agora.Whiteboard.iOS` moves to **2.16.147.1**: the native pin advances from
netless-io/Whiteboard-iOS 2.16.137 to **2.16.147**, and the binding revision starts over at 1 on the
new line.

### Upstream 2.16.138 to 2.16.147

Ten netless releases, taken here at once. The ones a consumer may notice:

- 2.16.138 adds `-[WhiteRoom setSyncMode:]`.
- 2.16.143 makes `addApp:completionHandler:` wait for the web app's setup and pass `nil` on failure.
  2.16.144 adds forced maximising, lazy setup and a local app cache.
- 2.16.145 loads the SDK's resources through the SwiftPM module bundle, and detaches Apple Pencil
  handlers when a room disconnects.
- 2.16.146 reads optional per-region configuration from netless's own storage when joining a room,
  with a five-second download timeout, and falls back to local values.
- 2.16.147 serialises replacing a room or player session, and isolates callbacks from a stale one.

The embedded web bundle moves with them: `white-web-sdk` 2.16.59, `window-manager` 1.0.25,
`appliance-plugin` 1.1.45, and `app-slide` 0.2.108 by 2.16.147.

### Binding

`WhiteRoom.SetSyncMode(bool)` is bound. `false`, the default, applies remote updates at a smoothed
pace; `true` applies each one as it arrives, trading smoothness for latency.

Nothing that was already bound changed. Upstream did change two signatures, `addApp` and
`dispatchDocsEvent`, but neither is in this binding. The rest of what upstream added isn't bound
either, including `leaveRoomWithCompletionHandler:`, `fitOriginSizeAndCamera`, `focusApp` and the
unified page state API.

### Build

`native/Whiteboard/Package.resolved` was re-locked for this version. It resolves the same
`DSBridge-IOS` 4.0.2 and `White_YYModel` 1.0.7 as before.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.iOS` | 2.16.147.1 | netless-io/Whiteboard-iOS 2.16.147, built from source (commit `8a8a90b7`) |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.

Reminder for consumers: the package ships `Whiteboard_Whiteboard.bundle` as content plus a
`buildTransitive` targets file that declares it as a `BundleResource` in your app. The library looks
the bundle up by that exact name, so referencing the package with `ExcludeAssets="build"` gives a
blank board with no error. Referencing `Net.Agora.Fastboard.iOS` alongside this package is not
supported: Fastboard's static library already contains Whiteboard's objects, at the version
fastboard-iOS resolves rather than this one, and both declare the same bundle.
