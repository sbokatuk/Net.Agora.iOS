## What's changed

`Net.Agora.Fastboard.iOS` advances to **1.4.6.1**, built from netless-io/fastboard-iOS at the pinned
revision behind tag 1.4.6, through the `AgoraFastboardBridge` Swift shim. The Whiteboard library
embedded in it moves from 2.16.137 to 2.16.145.

The .NET API is unchanged: the shim's header matches 1.4.5.2's apart from comments, and the
presentation, app focus and document paging APIs that Fastboard 1.4.6 adds are not bound. What does
change is how `Join`, `Disconnect` and `SetWritable` behave, and the text of failure messages.

### `Join` is refused unless the board is idle

Fastboard 1.4.6 refuses a join while another is in flight, once the board has joined, and while a
disconnect is still finishing. It says so only through a completion block that can't cross into
Objective-C, so the shim now forwards the refusal to `FastboardDidFail`, with the message
`Wait for the current room operation to finish before starting another one.` The refusal leaves the
board as it was: a joined room stays joined.

A join overtaken by a `Disconnect` reports `The room operation was cancelled.` the same way.

A join the room turned down can be retried on the same board. `Error on setupSDK` is the exception:
Fastboard stays failed after it and refuses every later join, so that case needs a new
`AgoraFastboard`.

### `Join`, `Disconnect` and `SetWritable` now run on the main thread

Fastboard 1.4.6 opens all three with a main-thread `precondition`, and a failed Swift precondition
ends the process with nothing managed code could catch. The shim now runs them straight away on the
main thread and queues them there from any other, so off the main thread they may finish after the
call returns. The other calls on `AgoraFastboard` go straight to the room, as they did.

### Failure messages read as sentences

Fastboard looks its messages up in a bundle named `LocalizedStrings`, which its CocoaPods spec
produces and SwiftPM does not, so 1.4.5.2 handed an app the bare key: `joinRoomError` rather than
`Join room error`. `build/build-fastboard.sh` now builds that bundle inside
`Fastboard_Fastboard.bundle`, the second place Fastboard looks. Code that matches on `joinRoomError`,
`setupSDKError` or `disconnectedError` in `FastboardDidFail` needs changing, and not to the English
sentence: these three follow the device's language, English or Simplified Chinese.

### `SetWritable` before joining now completes

In 1.4.5 a `SetWritable` with no room never called back, so `SetWritableAsync` never finished. It now
completes with `The room is not ready.` A second update while one is outstanding completes with the
same `Wait for the current room operation...` message and doesn't start.

### Fastboard 1.4.6, and what a build pulls in

Fastboard's changelog for 1.4.6 also lists fixes to join cancellation, duplicate joins, late
callbacks, the Pencil lifecycle, toolbar release, shape tools shared between rooms, and icon loading
under SwiftPM.

Fastboard 1.4.5 declared Whiteboard-iOS as `from: 2.16.125`, and that range floated up to a
Whiteboard it can't compile against. 1.4.6 declares `exact: "2.16.145"`, which is why the embedded
copy is that version and doesn't follow `Net.Agora.Whiteboard.iOS`. The new
`native/AgoraFastboardBridge/Package.resolved` fixes it, with `DSBridge-IOS` 4.0.2 and
`White_YYModel` 1.0.7, and a build that disagrees with that lockfile now fails.

### Samples

`samples/Net.Agora.Sample.Fastboard.iOS` and `samples/Net.Agora.Sample.Whiteboard.iOS` gained a
region field, because a room can only be joined from the region it was created in. Their status line
now appears: as a truncating label that started empty, it kept a height of zero.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Fastboard.iOS` | 1.4.6.1 | netless-io/fastboard-iOS 1.4.6 with Whiteboard-iOS 2.16.145, built from source via AgoraFastboardBridge |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.

Reminder for consumers: the package ships `Fastboard_Fastboard.bundle` and
`Whiteboard_Whiteboard.bundle` as content plus a `buildTransitive` targets file that declares them as
`BundleResource` items in your app; `ExcludeAssets="build"` gives a blank toolbar with no error.
Referencing `Net.Agora.Whiteboard.iOS` alongside this package is not supported — this static library
already contains Whiteboard's objects and both declare the same bundle.
