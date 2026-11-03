## What's changed

`Net.Agora.Signaling.iOS` advances to **2.2.6.4**. The managed binding is unchanged and `AgoraRtm_iOS`
is still 2.2.6. What moves is the `aosl` the package carries, from `AgoraInfra_iOS` 1.3.5 to
**1.3.16**.

### Why: RTC 4.7.0

An app that references more than one Agora package here keeps a single `aosl.framework`, so every
product in that app has to run against whichever copy survived. Since 2.2.6.3 this package has
carried the `aosl` the RTC pods bundle, byte for byte, so that an app carries one library rather than
two that have to get along.

RTC 4.7.0 moves that library to 1.3.16, and `AgoraRtcKit` 4.7.0 imports five symbols 1.3.5 never
exported: `aosl_ares_create_flags`, `aosl_ares_magic_complete`, `aosl_input_create_flags`,
`aosl_ref_create_flags` and `aosl_task_create_flags`. Left at 1.3.5, this package could stop the RTC
engine loading, which is the failure 2.2.6.3 fixed.

**If you use Signaling together with Video or Voice 4.7.0, you need this version.** Signaling on its
own was never affected, and all 89 `aosl` symbols `AgoraRtmKit` imports are exported by 1.3.16 too.
1.3.16 is also safe with RTC 4.6.2, which imports nothing it lacks.

`tests/Net.Agora.iOS.PackageTests/AoslCompatibilityTests.cs` is what holds this: it reads the real
symbol tables out of the packed `.nupkg`s, and fails if any `aosl` shipped here lacks a symbol any
framework here imports.

## Packages

| Package | Version | Native |
| --- | --- | --- |
| `Net.Agora.Signaling.iOS` | 2.2.6.4 | `AgoraRtm_iOS` 2.2.6 + `AgoraInfra_iOS` 1.3.16 |

Target frameworks: `net8.0-ios18.0`, `net9.0-ios18.0`, `net10.0-ios26.0`.
