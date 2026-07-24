# The extension packages

`Net.Agora.Extensions.*.iOS` are not bindings. They carry native payload and nothing else.

## Why they exist at all

Agora's RTC SDK ships its optional features — AI noise suppression, virtual background, spatial
audio, the software encoders and so on — as separate frameworks. On iOS they travel *inside* the
`AgoraRtcEngine_iOS` archive rather than as separate downloads, as CocoaPods subspecs
(`AgoraRtcEngine_iOS/VirtualBackground` and friends), so a pod consumer opts into each one and
gets only what it asked for.

The switch that turns each one on is already on `AgoraRtcEngineKit`, and therefore already in
`Net.Agora.Video.iOS` / `Net.Agora.Voice.iOS`. What is missing without these packages is the
framework the engine loads when the switch is flipped, and the failure mode is a runtime error
code from a call that compiled and linked perfectly.

So each extension is one package, mirroring the subspecs, and an app pays only for the ones it
turns on. That is the whole point of Agora splitting them: `AgoraSpatialAudioExtension` alone is
tens of MB across both slices.

## What the packages look like

Every one of them is one (or, for `VideoEncoder`, two) `.xcframework` with no Objective-C class a
consumer would call — the engine loads them itself. So each project's `ApiDefinition.cs` is
deliberately empty and the packed assembly is a ~6 KB stub next to the real payload in
`<Assembly>.resources.zip`, which is why `tests/Net.Agora.iOS.PackageTests` holds them to a
different set of expectations than the four real bindings.

They are still *binding* projects, because that is what packs a `NativeReference` as
`<Assembly>.resources.zip` — and the iOS SDK refuses to build one without an API definition file
("No API definition file specified"), which is the only reason those empty files exist.

`build/fetch-extensions.sh` stages them out of the same `AgoraRtcEngine_iOS` archive
`build/fetch-video.sh` already downloads and checksums, so there is nothing new to pin in
`build/checksums.txt`.

They deliberately depend on **neither** RTC package. The Video and Voice bindings are mutually
exclusive in one app, so depending on either would force a flavour on the consumer; and the audio
extensions work with both. The README tells consumers to add one alongside whichever RTC package
they already have.

## What is not here

**Screen capture** has no extension package. `AgoraReplayKitExtension.xcframework` is in the
archive, but it is not a plugin the engine loads — it is the code that runs inside a *Broadcast
Upload Extension*, a second app target with its own bundle identifier, entitlement and lifecycle
that only the consuming app can declare. A NuGet package cannot supply that. Ordinary screen
sharing does not need it either: `AgoraRtcEngineKit`'s screen-capture API is in the core SDK and
works with `Net.Agora.Video.iOS` alone.

**The low-latency variants** (`AgoraAiNoiseSuppressionLLExtension`,
`AgoraAiEchoCancellationLLExtension`) are present in the archive but have no Android artifact at
4.6.3 — the newest on Maven is 4.5.2 — so there is no pair to publish without opening a second
native version line.

**LipSync** (`AgoraLipSyncExtension`) is in the same position: an iOS framework with no Android
artifact under any spelling.
