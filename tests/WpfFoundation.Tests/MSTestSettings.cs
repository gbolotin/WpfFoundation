[assembly: DoNotParallelize]

namespace WpfFoundation.Tests;

[TestClass]
public static class TestAssembly
{
    [AssemblyCleanup]
    public static void Cleanup() => UiThread.Shutdown();
}
