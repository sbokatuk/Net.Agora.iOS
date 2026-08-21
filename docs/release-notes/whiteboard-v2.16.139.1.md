## What's changed

`Net.Agora.Whiteboard.iOS` moves to **2.16.139.1**: the native pin advances from netless-io/Whiteboard-iOS
2.16.137 to **2.16.139**, and the binding revision starts over at 1 on the new line. This is the first
time this package's native version has moved, so it is also the first run of the bump path
`build/build-whiteboard.sh` was written for.

### Upstream 2.16.138 and 2.16.139

Two netless releases, two days apart (2026-08-14 and 2026-08-15), both taken here at once:

- 2.16.138 syncs `Whiteboard-bridge` to `aa1dfd7` (`@netless/appliance-plugin` 1.1.39,
  `@netless/window-manager` 1.0.18) and adds one method to `WhiteRoom`: `setSyncMode:`, which
  switches remote board updates from the default smoothed pacing to applying each one as it arrives.
- 2.16.139 syncs `Whiteboard-bridge` to `42c7f0d` (`@netless/appliance-plugin` 1.1.40) and moves the
  appliance plugin's default worker rendering mode and canvas-context compatibility policy into the
  plugin; the bridge keeps passing the host's `appliancePluginOptions` through.

Across both, the public headers gained exactly that one declaration — `-[WhiteRoom setSyncMode:]` —
and nothing was removed or changed, so every existing binding compiles against 2.16.139 unmodified.

### Binding

`WhiteRoom.SetSyncMode(bool)` is bound. Nothing else in the ApiDefinition moved.

### Watcher

The upstream-drift check reported these releases as "published, but no download URL could be
resolved — check by hand" (#16), which is what its CocoaPods path says about any pod whose podspec
points at a git tag instead of an archive. It now recognises a source pod and reports the tag as the
confirmation, the way it already does for Fastboard.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.iOS` | 2.16.139.1 | netless-io/Whiteboard-iOS 2.16.139, built from source (commit `6202ce85`) |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.

Reminder for consumers: the package ships `Whiteboard_Whiteboard.bundle` as content plus a
`buildTransitive` targets file that declares it as a `BundleResource` in your app. The library looks
the bundle up by that exact name, so referencing the package with `ExcludeAssets="build"` gives a
blank board with no error. Referencing `Net.Agora.Fastboard.iOS` alongside this package is not
supported — Fastboard's static library already contains Whiteboard's objects (at the version
fastboard-iOS 1.4.5 resolves, not necessarily this one) and both declare the same bundle.
