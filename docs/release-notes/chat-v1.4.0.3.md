## What's changed

`Net.Agora.Chat.iOS` advances to **1.4.0.3**. The managed binding is unchanged; what moves is the
native payload underneath it.

### AgoraInfra_iOS 1.3.5 → 1.3.9

Chat is the one product here whose `aosl` does not come out of its own archive: `Agora_Chat_iOS`
declares `AgoraInfra_iOS` as a pod dependency, so `build/fetch-chat.sh` downloads two archives and
the two versions are pinned separately. Agora published `AgoraInfra_iOS` 1.3.9 while the Chat pod
itself stayed at 1.4.0, so only the infra pin moves — and since the first three components of the
package version track the Chat pod, the binding revision is what advances.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.iOS` | 1.4.0.3 | `Agora_Chat_iOS` 1.4.0 + `AgoraInfra_iOS` 1.3.9 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
