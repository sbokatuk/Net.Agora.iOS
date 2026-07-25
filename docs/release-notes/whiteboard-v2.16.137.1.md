## What's changed

**The Interactive Whiteboard debuts**: `Net.Agora.Whiteboard.iOS` **2.16.137.1** — the first
product here with no binary to fetch. Agora publishes every other SDK as a prebuilt `.xcframework`
behind a `download.agora.io` URL; netless ships this one as a CocoaPods *source* pod, so
`build/build-whiteboard.sh` builds it: clone a pinned commit, archive the repository's Swift
package for both platforms, link the archived objects with `libtool`, assemble the xcframework. No
CocoaPods and no Ruby — the `Package.swift` resolves the dependency graph.

That widens `build/checksums.txt`'s job past SHA-256-ing an archive, hence its second entry form:
a git source is pinned by **commit**, because a tag is a mutable pointer and netless has re-tagged
before. The clone finds the commit through the tag; the script verifies the commit.

The package carries the board's JavaScript resource bundle under **SwiftPM's own name**
(`Whiteboard_Whiteboard.bundle`): the library looks it up by that exact string through the
module-bundle finder SwiftPM generated, so a tidier name makes the board render blank, with no
error. A binding package cannot ship a `BundleResource` of its own, so it travels as plain content
with a `buildTransitive` `.targets` that re-declares it in the consuming app. The package tests
assert both the xcframework and the bundle.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Whiteboard.iOS` | 2.16.137.1 | built from `netless-io/Whiteboard-iOS` 2.16.137 |
