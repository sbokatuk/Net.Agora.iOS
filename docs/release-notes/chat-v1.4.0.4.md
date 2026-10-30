## What's changed

`Net.Agora.Chat.iOS` advances to **1.4.0.4**. The managed binding is unchanged, and `Agora_Chat_iOS`
is still 1.4.0. What moves is the `aosl` the package carries.

### AgoraInfra_iOS 1.3.9 → 1.3.16

Chat is one of the products whose `aosl` comes from `AgoraInfra_iOS`, pinned separately from the
Chat pod. Agora published 1.3.16 while the Chat pod stayed at 1.4.0, so only the infra pin moves,
and since the first three components of the package version track the Chat pod, the binding
revision is what advances.

1.3.16 exports 64 `aosl` symbols that 1.3.9 did not and removes none, and all four `AgoraChat`
imports are still there. It is also, byte for byte on both slices, the `aosl` inside the
`AgoraRtcEngine_iOS` and `AgoraAudio_iOS` 4.7.0 pods.

`build/fetch-chat.sh` still handles a fat device slice, but 1.3.16 is back to a thin `ios-arm64`, so
that step now has nothing to do.

### Using Chat with other products

This release is safe alongside either RTC line. An app that references more than one package
keeps a single `aosl.framework`, and `AoslCompatibilityTests` holds every `aosl` shipped here to
every symbol any framework here imports: 1.3.16 exports all of them for RTC 4.6.2, RTC 4.7.0,
Signaling and Chat.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.iOS` | 1.4.0.4 | `Agora_Chat_iOS` 1.4.0 + `AgoraInfra_iOS` 1.3.16 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
