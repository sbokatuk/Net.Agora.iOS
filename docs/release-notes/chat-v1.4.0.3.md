## What's changed

`Net.Agora.Chat.iOS` advances to **1.4.0.3**. The managed binding is unchanged; what moves is the
native payload underneath it.

### AgoraInfra_iOS 1.3.5 → 1.3.9

Chat is the one product here whose `aosl` does not come out of its own archive: `Agora_Chat_iOS`
declares `AgoraInfra_iOS` as a pod dependency, so `build/fetch-chat.sh` downloads two archives and
the two versions are pinned separately. Agora published `AgoraInfra_iOS` 1.3.9 while the Chat pod
itself stayed at 1.4.0, so only the infra pin moves — and since the first three components of the
package version track the Chat pod, the binding revision is what advances.

The payload is otherwise the same size and shape it was: `aosl` 1.3.9 adds API (`aosl_ref_create_flags`,
`aosl_ares_create_flags`, `aosl_ares_magic_complete`) and reworks the C++ `aosl_ares_class` helper,
none of which this binding calls — `AgoraChat` links `aosl` as its one `@rpath` dependency and the
bound surface is unchanged.

### The device slice is now thinned

1.3.9 builds its device slice as `ios-arm64_armv7` — a fat armv7 + arm64 binary — where 1.3.5
shipped a thin `ios-arm64`. .NET for iOS has never supported 32-bit armv7, so `build/fetch-chat.sh`
now lipos it out, renames the slice and rewrites the xcframework manifest to match, exactly as
`fetch-signaling.sh` has always done for RTM. Without it the package would carry a dead
architecture once per target framework and fail the existing
`Native_payload_carries_no_dead_architectures` package test.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.iOS` | 1.4.0.3 | `Agora_Chat_iOS` 1.4.0 + `AgoraInfra_iOS` 1.3.9 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
