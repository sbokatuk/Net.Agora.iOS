// Intentionally empty — see the .csproj, and src/Agora.Extension.md.
//
// Net.Agora.Extensions.VirtualBackground.iOS ships one xcframework: a framework the RTC engine loads at
// runtime when the corresponding switch on AgoraRtcEngineKit is turned on. There is no
// Objective-C class for a consumer to call, so there is nothing to bind — but the iOS SDK will
// not build a binding project without an API definition file, and a binding project is what packs
// the framework as <Assembly>.resources.zip.
namespace Net.Agora.Extensions.VirtualBackground.iOS {
}
