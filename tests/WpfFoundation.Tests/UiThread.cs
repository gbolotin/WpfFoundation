using System.Windows;
using System.Windows.Threading;

namespace WpfFoundation.Tests;

/// <summary>
/// Runs WPF integration tests on one shared STA dispatcher thread. WPF allows one Application per process,
/// so the thread owns a Fluent-themed Application with the library's entry dictionary merged, as an
/// application using the library has.
/// </summary>
internal static class UiThread
{
    private static readonly Lazy<Dispatcher> Dispatcher = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static Task RunAsync(Func<Task> test) => Dispatcher.Value.InvokeAsync(test).Task.Unwrap();

    public static Task RunAsync(Action test) => Dispatcher.Value.InvokeAsync(test).Task;

    public static Task<T> RunAsync<T>(Func<T> test) => Dispatcher.Value.InvokeAsync(test).Task;

    public static Task<T> RunAsync<T>(Func<Task<T>> test) => Dispatcher.Value.InvokeAsync(test).Task.Unwrap();

    public static void Shutdown()
    {
        if (Dispatcher.IsValueCreated)
        {
            Dispatcher.Value.InvokeShutdown();
        }
    }

    /// <summary>Lets pending layout, bindings and loaded events run.</summary>
    public static async Task IdleAsync()
    {
        await System.Windows.Threading.Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    }

    /// <summary>Shows a window off screen, without a taskbar entry.</summary>
    public static Window ShowWindow(object content, double width = 600, double height = 400)
    {
        var window = new Window
        {
            Content = content,
            Width = width,
            Height = height,
            ShowInTaskbar = false,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000
        };
        window.Show();
        return window;
    }

    private static Dispatcher Start()
    {
        Dispatcher? started = null;
        Exception? failure = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown, ThemeMode = ThemeMode.Light };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = WpfFoundationResources.EntryDictionaryUri });
                started = dispatcher;
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                ready.Set();
            }

            if (failure is null)
            {
                System.Windows.Threading.Dispatcher.Run();
            }
        })
        {
            IsBackground = true,
            Name = "WPF test UI thread"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return started ?? throw new InvalidOperationException("The WPF test application could not start.", failure);
    }
}
