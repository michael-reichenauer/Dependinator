using System.Diagnostics;

namespace Dependinator.Utils;

// Handles unhandled exceptions top ensure they are logged and program is restarted or shut down
static class ExceptionHandling
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

        // Log exceptions that hasn't been handled when a Task is finalized.
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            HandleException("unobserved task exception", e.Exception);
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
