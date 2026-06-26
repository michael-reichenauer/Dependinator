using System.Diagnostics;

namespace Dependinator.Core.Utils;

// Handles unhandled exceptions top ensure they are logged and program is restarted or shut down
public static class ExceptionHandling
{
    private static readonly TimeSpan MinTimeBeforeAutoRestart = TimeSpan.FromSeconds(10);

    private static bool hasDisplayedErrorMessageBox;
    private static bool hasFailed;
    private static bool hasShutdown;
    private static DateTime StartTime = DateTime.UtcNow;
    private static Action shutdown = () => { };

    public static void HandleUnhandledExceptions(Action shutdownCallback)
    {
        shutdown = shutdownCallback;
        // Add the event handler for handling non-UI thread exceptions to the event.
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            HandleException("app domain exception", e.ExceptionObject as Exception ?? new Exception());

        // Unobserved task exceptions are failures of fire-and-forget tasks that nobody
        // awaited. Since .NET 4.5 these do NOT terminate the process by default, and for a
        // long-running server host they must not: when a Blazor Server circuit is torn down
        // (the browser navigates away or closes) while the server is completing a JS-interop
        // call, SignalR's RemoteJSRuntime.EndInvokeDotNet can throw a NullReferenceException
        // sending the result to the now-gone client. That is a benign per-circuit race;
        // treating it as fatal here used to Environment.Exit the whole app, taking the server
        // down for every other client (and making firefox/webkit e2e runs flaky, as they
        // tear circuits down between tests). Log it, mark it observed, and keep running.
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Exception(e.Exception, "Unobserved task exception (non-fatal)");
            e.SetObserved();
        };

        // Add event handler for fatal exceptions using catch condition "when (e.IsNotFatal())"
        FatalExceptionsExtensions.FatalException += (s, e) => HandleException(e.Message, e.Exception);

        // Add handler for asserts
        Asserter.AssertOccurred += (s, e) => HandleException("Assert failed", e.Exception);
    }

    public static void OnBackgroundTaskException(Exception exception)
    {
        HandleException("RunInBackground error", exception);
    }

    // public static void HandleDispatcherUnhandledException()
    // {
    // 	// Add the event handler for handling UI thread exceptions to the event
    // 	Application.Current.DispatcherUnhandledException += (s, e) =>
    // 	{
    // 		HandleException("dispatcher exception", e.Exception);
    // 		e.Handled = true;
    // 	};

    // 	WpfBindingTraceListener.Register();

    // 	isDispatcherInitialized = true;
    // }

    static void HandleException(string errorType, Exception exception)
    {
        if (hasFailed)
            return;
        hasFailed = true;

        string errorMessage = $"Unhandled {errorType}";
        Log.Exception(exception, errorMessage);

        Shutdown(errorMessage, exception);
    }

    static void Shutdown(string message, Exception e)
    {
        if (hasShutdown)
            return;
        hasShutdown = true;

        // if (isDispatcherInitialized)
        // {
        // 	var dispatcher = GetApplicationDispatcher();
        // 	if (dispatcher.CheckAccess())
        // 	{
        // 		ShowExceptionDialog(e);
        // 	}
        // 	else
        // 	{
        // 		dispatcher.Invoke(() => ShowExceptionDialog(e));
        // 	}
        // }

        if (Debugger.IsAttached)
        {
            // NOTE: If you end up here a task resulted in an unhandled exception
            Debugger.Break();
        }

        ConfigLogger.CloseAsync().ContinueWith(t => shutdown());

        // shutdown();

        // if (DateTime.Now - StartTime >= MinTimeBeforeAutoRestart)
        // {
        // 	StartInstanceService.StartInstance(Environment.CurrentDirectory);
        // }

        // if (isDispatcherInitialized)
        // {
        // 	Application.Current.Shutdown(0);
        // }
        // else
        // {
        // 	throw new Exception($"Unhandled exception {message}", e);
        // }
    }

    private static void ShowExceptionDialog(Exception e)
    {
        if (hasDisplayedErrorMessageBox)
        {
            return;
        }

        if (DateTime.UtcNow - StartTime < MinTimeBeforeAutoRestart)
        {
            Console.WriteLine("Sorry, but an unexpected error just occurred");
            StartTime = DateTime.UtcNow;
        }

        hasDisplayedErrorMessageBox = true;
    }

    // private static Dispatcher GetApplicationDispatcher() =>
    // 	Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
}
