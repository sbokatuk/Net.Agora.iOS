## What's changed

**Chat debuts**: `Net.Agora.Chat.iOS` **1.4.0.1** — a hand-written binding over
`Agora_Chat_iOS 1.4.0`, scoped to what a messaging client needs: `AgoraChatClient` (initialise,
sign in and out, renew the token), `IAgoraChatManager` (send, conversations), the message and
conversation model types, and the two all-optional delegates.

Note the pod: **`Agora_Chat_iOS`**, with underscores. The `AgoraChat` pod on CocoaPods trunk is the
abandoned 3.8.x Easemob line and is not this SDK. It is also the first product here whose `aosl`
does not come from its own archive — it declares `AgoraInfra_iOS` as a dependency, so
`build/fetch-chat.sh` downloads and checksums two archives rather than one.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.iOS` | 1.4.0.1 | `Agora_Chat_iOS` 1.4.0 + `AgoraInfra_iOS` 1.3.5 |
