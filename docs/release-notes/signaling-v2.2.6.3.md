## What's changed

`Net.Agora.Signaling.iOS` advances to **2.2.6.3**. The managed binding is unchanged and
`AgoraRtm_iOS` is still 2.2.6; what moves is which `aosl` the package carries.

### Signaling no longer risks breaking Video and Voice in the same app

Four packages here ship an `aosl.xcframework` — Agora's own infrastructure library, which
`AgoraRtcKit`, `AgoraRtmKit` and `AgoraChat` all link as an `@rpath` dependency. An app that
references more than one of them does **not** get one each: every copy unpacks to
`Frameworks/aosl.framework`, the bundle carries exactly one, and which one is whichever the build
happened to keep. Every product in that app then runs against a library it may not have been
built against.

Up to 2.2.6.2 that was a real hazard. The `AgoraRtm_iOS` zip bundles `aosl` **1.2.13**, where the
RTC pods bundle **1.3.5**, and 1.2.13 does not export `aosl_ref_magic` — one of 144 `aosl` symbols
`AgoraRtcKit` resolves at load. An app referencing Signaling and Video or Voice could therefore
fail to bring up the RTC engine at all, decided by nothing the developer controls and reported by
nothing in the build.

`build/fetch-signaling.sh` now takes `aosl` from `AgoraInfra_iOS` 1.3.5 — the pod Chat already
takes it from, and at that version byte-for-byte the copy the RTC pods bundle — and never stages
the one inside the RTM zip. A Video + Signaling app now carries one library rather than two that
have to get along.

**If you use Signaling together with Video or Voice, upgrade.** Signaling on its own was never
affected, and nothing regressed for it here: all 89 `aosl` symbols `AgoraRtmKit` imports are
exported by 1.3.5 as well.

The sibling repositories carry the same class of bug and are fixed in the same round —
[`Net.Agora.Android`](https://github.com/sbokatuk/Net.Agora.Android) `signaling-v2.2.6.3`, where
`agora-rtm` vendors its own stale `libaosl.so` and `RtcEngine.Create()` returned `null` on a real
device, and [`Net.Agora.Mac`](https://github.com/sbokatuk/Net.Agora.Mac) `signaling-v2.2.8.3`.

`Net.Agora.Chat.iOS` stays on `AgoraInfra_iOS` 1.3.9 deliberately. 1.3.9 is not identical to
1.3.5 — it adds three symbols and drops two — but it satisfies all three SDK frameworks, so the
mixture is safe, and forcing unrelated products into lockstep would buy nothing.

### The conflict cannot come back quietly

Being invisible in a build is what made this dangerous, so both guards fail rather than warn:

- `build/fetch-signaling.sh` refuses to finish if the staged `aosl.framework` does not export
  `aosl_ref_magic` — the symptom, not a version string.
- `tests/Net.Agora.iOS.PackageTests/AoslCompatibilityTests.cs` reads real Mach-O symbol tables out
  of the packed `.nupkg`s and asserts that **every** `aosl` any package ships exports **every**
  `aosl` symbol any SDK framework here imports. A version string can agree while the exports do
  not, so the check is on exports.

### Samples

`samples/Net.Agora.Sample.Chat.iOS`, `samples/Net.Agora.Sample.Fastboard.iOS` and
`samples/Net.Agora.Sample.Whiteboard.iOS` are new, so every product bound here now has a sample.
`samples/Net.Agora.Sample.Signaling.iOS` gained a real RTM token field, so it can be pointed at a
project with App Certificate enabled rather than only at the App-ID-as-token fallback. Every iOS
sample that was missing an app icon and a launch screen now has one — without the latter, a sample
runs letterboxed on a real device.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.iOS` | 2.2.6.3 | `AgoraRtm_iOS` 2.2.6 + `AgoraInfra_iOS` 1.3.5 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
