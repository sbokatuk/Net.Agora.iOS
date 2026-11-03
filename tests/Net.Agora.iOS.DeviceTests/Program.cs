using System.Runtime.InteropServices;
using Foundation;
using UIKit;

namespace Net.Agora.iOS.DeviceTests;

/// <summary>
/// Host for the on-simulator checks. Runs every one on launch, reports the outcome to stdout —
/// which <c>simctl launch --console-pty</c> streams straight back to CI — and exits with a verdict
/// line the runner script greps for.
/// </summary>
public static class Program
{
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register(nameof(AppDelegate))]
public sealed class AppDelegate : UIApplicationDelegate
{
    public override UIWindow? Window { get; set; }

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // A window is not strictly needed for a headless run, but iOS terminates an app that never
        // presents one, which would look like a crash rather than a failing check.
        var root = new UIViewController();
        root.View!.BackgroundColor = UIColor.SystemBackground;

#pragma warning disable CA1422 // UIScreen.MainScreen / this UIWindow constructor are obsolete from
                               // iOS 26 in favour of UIWindowScene overloads that do not exist below
                               // iOS 13 - this app's own SupportedOSPlatformVersion.
        Window = new UIWindow(UIScreen.MainScreen.Bounds) { RootViewController = root };
#pragma warning restore CA1422
        Window.MakeKeyAndVisible();

        // On the main thread deliberately: the Agora kits dispatch their delegate callbacks and
        // completion blocks through the main queue, and their own samples create and drive them
        // from it — the Signaling flavour's awaitable check relies on this and pumps the run
        // loop rather than blocking. Nothing here blocks long enough to trip the watchdog: no
        // check joins a channel, and the one that starts a join, on the Fastboard flavour, uses
        // placeholders and gives up after 15 seconds.
        RunAndReport();

        return true;
    }

    private static void RunAndReport()
    {
        SmokeTests.Reporter = message => Console.WriteLine($"    {message}");

        var failures = 0;

        foreach (var test in SmokeTests.All)
        {
            try
            {
                test.Execute();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception exception)
            {
                failures++;

                // The type as well as the message: half the interesting failures here are a native
                // call throwing something the binding did not expect, and the message alone rarely
                // says which side of the bridge it came from.
                Console.WriteLine($"FAIL {test.Name}: {exception.GetType().Name}: {exception.Message}");

                if (exception.StackTrace is { } stack)
                {
                    Console.WriteLine(stack);
                }
            }
        }

        // The same marker the façade repository's device tests print, so this repository's
        // run-simulator-tests.sh is a straight adaptation of that one rather than a fork with a
        // different grep.
        Console.WriteLine(failures == 0
            ? "AGORA_E2E_DONE PASS"
            : $"AGORA_E2E_DONE FAIL ({failures} failed)");
        Console.Out.Flush();

        // Terminate so the runner's `simctl launch --console-pty` returns instead of hanging until
        // its timeout. Exiting from an iOS app is otherwise not something to imitate.
        //
        // _exit rather than Environment.Exit, which runs the process's exit handlers. From 4.7.0
        // the voice build's AgoraffmpegExtension registers one that never returns: it spins in
        // aosl_module_unregister, so the verdict prints and the process then sits there.
        _exit(failures == 0 ? 0 : 1);
    }

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern void _exit(int status);
}
