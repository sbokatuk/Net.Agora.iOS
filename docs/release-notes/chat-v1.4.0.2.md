## What's changed

`Net.Agora.Chat.iOS` advances to **1.4.0.2**. The native SDK is unchanged at Agora_Chat_iOS 1.4.0.

### Awaitable operations

The completion-block methods now also have awaitable forms: `LoginAsync` (returning the username or
the error), `LogoutAsync` and `RenewTokenAsync`, and `SendMessageAsync` on the chat manager:

```csharp
var result = await client.ChatManager.SendMessageAsync(message);
if (result.Error is { } error)
    return Fail(error);
```

As with Signaling, these tasks complete rather than fault: `AgoraChatError` is not an `NSError`
subclass and the SDK reports outcome through it rather than through failure. The callback-taking
methods are untouched.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Chat.iOS` | 1.4.0.2 | Agora_Chat_iOS 1.4.0 + AgoraInfra_iOS 1.3.5 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
