using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace WpfFoundation.Tests;

/// <summary>
/// Runs a WPF integration test on a dedicated STA thread with its own dispatcher,
/// so awaited continuations return to that dispatcher as they do in an application.
/// </summary>
internal static class StaDispatcher
{
    public static void Run(Func<Task> test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await test();
                }
                catch (Exception exception)
                {
                    failure = ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    public static void Run(Action test) => Run(() =>
    {
        test();
        return Task.CompletedTask;
    });
}
