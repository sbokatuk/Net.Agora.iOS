# Net.Agora.iOS

[![NuGet](https://img.shields.io/nuget/v/Net.Agora.Video.iOS?label=nuget)](https://www.nuget.org/packages/Net.Agora.Video.iOS)
[![Targets: net8.0 | net9.0 | net10.0](https://img.shields.io/badge/targets-net8.0%20%7C%20net9.0%20%7C%20net10.0-512BD4)](#packages)
[![AgoraRtcEngine_iOS 4.6.2](https://img.shields.io/badge/AgoraRtcEngine__iOS-4.6.2-099DFD)](https://cocoapods.org/pods/AgoraRtcEngine_iOS)
[![Licence: MIT](https://img.shields.io/badge/licence-MIT-green)](LICENSE)

.NET for iOS and .NET MAUI bindings for Agora's native iOS SDKs.

Two products are bound, from `net8.0-ios` through `net10.0-ios`:

| Package | Native pod | Use it when |
| --- | --- | --- |
| `Net.Agora.Video.iOS` | [`AgoraRtcEngine_iOS`](https://cocoapods.org/pods/AgoraRtcEngine_iOS) | The app shows or sends video (also carries the full audio surface). |
| `Net.Agora.Voice.iOS` | [`AgoraAudio_iOS`](https://cocoapods.org/pods/AgoraAudio_iOS) | Audio only — the same engine built without the video pipeline, one fewer framework and a markedly smaller payload. |

```bash
dotnet add package Net.Agora.Video.iOS   # or Net.Agora.Voice.iOS
```

Pick **one**: both pods ship a framework named `AgoraRtcKit`, so referencing both packages
collides at bundle time — mirroring Agora's own pods. The bindings' namespaces follow the package
(`Net.Agora.Video.iOS` / `Net.Agora.Voice.iOS`).

These are raw platform bindings — hand-written surfaces over `AgoraRtcEngineKit`, scoped to what
the cross-platform clients need: engine lifecycle, join/leave, publish/mute, audio routing,
volume indication, token renewal and connection state, plus the camera surface in Video. Most apps want the cross-platform clients instead:
[`Net.Agora.Video` / `Net.Agora.Voice`](https://github.com/sbokatuk/Net.Agora), which wrap these
packages and their Android siblings behind one API. Reach for a binding directly only when you
need something the cross-platform client does not expose.

```csharp
using Net.Agora.Video.iOS;

var config = new AgoraRtcEngineConfig { AppId = "<APP_ID>" };
var engine = AgoraRtcEngineKit.SharedEngine(config, del: null);

engine.EnableVideo();
engine.JoinChannel(token: null, channelId: "my-channel", info: null, uid: 0, joinSuccess: null);
```

---

## How this repository works

This repository is the *only* thing that binds Agora's iOS SDKs:
[`Net.Agora`](https://github.com/sbokatuk/Net.Agora) (the cross-platform façade) and
[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) (the Android binding) are
separate repositories, each with their own release cadence. Each package's version is
`<pod version>.<binding revision>` — see `Directory.Build.props` for why the iOS and Android lines
don't share a version number. The package set lives in `build/packages.tsv`; adding a package
means adding a row there, a project under `src/`, and a fetch script under `build/`.

### What is bound, and why by hand

Objective Sharpie's bundled clang cannot parse any Xcode SDK from 15.3 onward —

```
module '_c_standard_library_obsolete' requires feature 'found_incompatible_headers__check_search_paths'
```

— confirmed by downloading the real `AgoraRtcEngine_iOS` xcframework and running `sharpie bind`
against it directly. `src/Net.Agora.Video.iOS/ApiDefinition.cs` and `StructsAndEnums.cs` are
hand-written instead, straight from the real headers (`AgoraRtcEngineKit.h`, `AgoraObjects.h`,
`AgoraEnumerates.h`), scoped to what the cross-platform client needs: create the engine, join/leave
a channel, publish/mute audio and video, render local/remote video, and the matching delegate
callbacks. Extend it by reading more of the same headers as more of the SDK is needed.

**No Mac Catalyst.** `AgoraRtcKit.xcframework`'s `Info.plist` lists only `ios-arm64` and
`ios-arm64_x86_64-simulator` slices — no `maccatalyst`. There is no `Net.Agora.Video.Mac`.

## Building locally

```sh
./build/fetch-video.sh            # downloads + SHA-256-verifies the xcframeworks into src/Net.Agora.Video.iOS/lib
./build/fetch-voice.sh            # same, for Net.Agora.Voice.iOS
./build/BuildNugets.sh            # packs into ./artifacts
dotnet test tests/Net.Agora.iOS.PackageTests
```

The fetch scripts refuse to proceed if the downloaded archive's hash doesn't match
`build/checksums.txt` — see that file for how to record a new version's hash.

No single .NET SDK builds net8, net9 *and* net10 for iOS, so `BuildNugets.sh` packs twice (the
installed SDK's band, then a `net10` pass from a scratch `global.json`) and merges the results —
see `build/merge-packages.py`.

## Tests

**Package tests** run anywhere and inspect both packed `.nupkg`s — a binding assembly and a native
payload for every target framework, every expected xcframework present (six for Video, five for
Voice — the voice build of AgoraRtcKit links no video codec, which is the point), the payload
logically identical across target frameworks (which is what would catch a mismatched net10 merge
graft), iOS device and simulator slices only, and manifests consistent with the slices actually
shipped:

```sh
./build/BuildNugets.sh                       # produce ./artifacts first
dotnet test tests/Net.Agora.iOS.PackageTests
```

**Simulator smoke tests** (`tests/Net.Agora.iOS.DeviceTests`) build an app against the packed
package and drive the raw binding on a booted simulator, which is the only way to prove the
native frameworks actually link and load — the historical example being `video_dec`, which
AgoraRtcKit's binary loads at launch and whose absence nothing at compile or link time can see.
The checks assert every framework is loaded into the process, read the native SDK version, and
round-trip the engine create → drive → destroy with an unregistered App ID; nothing touches the
network and no credentials are involved (the [façade repo][facade]'s own device tests cover the
join path):

```sh
./.github/scripts/run-simulator-tests.sh 4.6.2.1 net10.0-ios26.0
./.github/scripts/run-simulator-tests.sh 4.6.2.1 net10.0-ios26.0 Voice
```

One package per run — both ship a framework named `AgoraRtcKit`, so a single app holds one of
them. The Voice flavour swaps the video checks for the audio-route/volume-indication surface that
binding adds.

## CI

| Workflow | Trigger | What it does |
| --- | --- | --- |
| [`pr.yml`](.github/workflows/pr.yml) | pull request | Packs every package in `build/packages.tsv` as `<version>-beta.<pr>.<run>`, runs the package tests, builds the MAUI sample against the packed package, runs the simulator smoke tests (per package, net8 and net10 legs in parallel), then publishes the betas to nuget.org. Forked PRs build and test but skip publishing, since they cannot read secrets. |
| [`release.yml`](.github/workflows/release.yml) | tag `v*` | Same build and tests at the tag's version, publishes to nuget.org, then creates a GitHub release — from `docs/release-notes/<version>.md` when a curated file exists, otherwise from the commit log since the previous tag. |

Both call the reusable [`build.yml`](.github/workflows/build.yml), whose pack, sample and e2e jobs
run on macOS — iOS builds have no cross-platform path, unlike the Android bindings.

## Sample

`samples/Net.Agora.Sample.iOS` is a MAUI app (iOS only) that joins a channel and publishes/renders
video directly against `AgoraRtcEngineKit`: an App ID/channel/token entry, Join/Leave buttons, and
a local + remote video view backed by a hand-rolled MAUI handler (`AgoraVideoView.cs`) — which is
exactly what a consumer skipping the cross-platform façade has to wire up, and the part the
[façade repo][facade]'s `Net.Agora.Video.Maui` package otherwise does for you. It's the same flow
as that repo's cross-platform MAUI sample, but built straight against this package with no façade
in between — proof `Net.Agora.Video.iOS` is consumable end to end.

It consumes the packed `Net.Agora.Video.iOS` package from `./artifacts` (see `NuGet.config`), so
fetch and pack first:

```sh
./build/fetch-video.sh
./build/BuildNugets.sh
dotnet build samples/Net.Agora.Sample.iOS -f net9.0-ios18.0 -p:RuntimeIdentifier=iossimulator-arm64
```

Debug is the default configuration and is what you want here too: a Release build triggers full
AOT compilation and linking, which costs real build time for no added signal on whether the
package restores and links correctly — same reasoning as the façade repo's sample.

## Licence

MIT — see [LICENSE](LICENSE). Agora's own SDK is distributed under Agora's SDK licence terms.

[agora]: https://www.agora.io/en/
[facade]: https://github.com/sbokatuk/Net.Agora
