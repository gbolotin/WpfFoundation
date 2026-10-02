using System.Windows;

[assembly: DoNotParallelize]

namespace WpfFoundation.Tests;

[TestClass]
public static class TestAssembly
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        // Application's type initializer registers the pack URI handlers that resource dictionaries load through.
        _ = Application.Current;
    }
}
