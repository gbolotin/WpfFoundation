using WpfFoundation.Navigation;

namespace WpfFoundation.Tests;

[TestClass]
public sealed class NavigationServiceTests
{
    [TestMethod]
    public async Task NavigatingMakesThePageCurrentAndActivatesItEveryTime()
    {
        var trails = new TestPage("Trails");
        var notes = new ActivatedPage("Notes");
        var navigation = new NavigationService([trails, notes]);
        var changes = new List<string?>();
        navigation.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        Assert.IsNull(navigation.CurrentPage);
        Assert.IsTrue(await navigation.NavigateAsync(trails));
        Assert.IsTrue(await navigation.NavigateAsync(notes));
        Assert.IsTrue(await navigation.NavigateAsync(trails));
        Assert.IsTrue(await navigation.NavigateAsync(notes));

        Assert.AreSame(notes, navigation.CurrentPage);
        Assert.AreEqual(2, notes.Activations);
        Assert.AreEqual(4, changes.Count(name => name == nameof(INavigationService.CurrentPage)));
        Assert.IsTrue(await navigation.NavigateAsync(notes), "The current page is already there.");
        Assert.AreEqual(2, notes.Activations, "Navigating to the current page does nothing.");
    }

    [TestMethod]
    public async Task ARejectedNavigationKeepsTheCurrentPageAndRaisesItAgain()
    {
        var trails = new TestPage("Trails");
        var notes = new TestPage("Notes");
        var guard = new Guard { Allow = false };
        var navigation = new NavigationService([trails, notes], guard);
        await navigation.NavigateAsync(trails);
        int raised = 0;
        navigation.PropertyChanged += (_, e) => raised += e.PropertyName == nameof(INavigationService.CurrentPage) ? 1 : 0;

        Assert.IsFalse(await navigation.NavigateAsync(notes));

        Assert.AreSame(trails, navigation.CurrentPage);
        Assert.AreEqual(1, raised, "A bound sidebar is told to return to the current page.");
        Assert.AreEqual((trails, notes), guard.LastRequest);
    }

    [TestMethod]
    public async Task TheGuardCanMakeNavigationUnavailable()
    {
        var trails = new TestPage("Trails");
        var notes = new TestPage("Notes");
        var guard = new Guard();
        var navigation = new NavigationService([trails, notes], guard);
        await navigation.NavigateAsync(trails);
        var changes = new List<string?>();
        navigation.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        guard.SetCanNavigate(false);
        Assert.IsFalse(navigation.CanNavigate);
        CollectionAssert.Contains(changes, nameof(INavigationService.CanNavigate));
        Assert.IsFalse(await navigation.NavigateAsync(notes));
        Assert.IsNull(guard.LastRequest, "The guard is not asked while navigation is unavailable.");

        guard.SetCanNavigate(true);
        Assert.IsTrue(await navigation.NavigateAsync(notes));
        navigation.Dispose();
        changes.Clear();
        guard.SetCanNavigate(false);
        Assert.IsEmpty(changes, "A disposed service stops listening to the guard.");
    }

    [TestMethod]
    public async Task AnOverlappingNavigationIsRejectedWhileTheFirstCompletes()
    {
        var trails = new TestPage("Trails");
        var notes = new TestPage("Notes");
        var maps = new TestPage("Maps");
        var guard = new Guard { Pending = new TaskCompletionSource<bool>() };
        var navigation = new NavigationService([trails, notes, maps], guard);
        await navigation.NavigateAsync(trails);

        var first = navigation.NavigateAsync(notes);
        Assert.IsFalse(navigation.CanNavigate);
        Assert.IsFalse(await navigation.NavigateAsync(maps));
        guard.Pending.SetResult(true);

        Assert.IsTrue(await first);
        Assert.AreSame(notes, navigation.CurrentPage);
        Assert.IsTrue(navigation.CanNavigate);
    }

    [TestMethod]
    public async Task ACancelledNavigationKeepsTheCurrentPage()
    {
        var trails = new TestPage("Trails");
        var notes = new InitializedPage("Notes") { Delay = Timeout.InfiniteTimeSpan };
        var navigation = new NavigationService([trails, notes]);
        await navigation.NavigateAsync(trails);
        using var cancellation = new CancellationTokenSource();

        var navigating = navigation.NavigateAsync(notes, cancellation.Token);
        cancellation.Cancel();

        Assert.IsFalse(await navigating);
        Assert.AreSame(trails, navigation.CurrentPage);
        Assert.IsTrue(navigation.CanNavigate);
    }

    [TestMethod]
    public async Task APageInitializesOnceAndRetriesAfterAFailure()
    {
        var trails = new TestPage("Trails");
        var notes = new InitializedPage("Notes") { Failures = 1 };
        var navigation = new NavigationService([trails, notes]);
        var failures = new List<NavigationFailedEventArgs>();
        navigation.NavigationFailed += (_, e) => failures.Add(e);
        await navigation.NavigateAsync(trails);

        Assert.IsFalse(await navigation.NavigateAsync(notes));
        Assert.AreSame(trails, navigation.CurrentPage);
        Assert.HasCount(1, failures);
        Assert.AreSame(notes, failures[0].Page);
        Assert.IsInstanceOfType<InvalidOperationException>(failures[0].Exception);

        Assert.IsTrue(await navigation.NavigateAsync(notes));
        await navigation.NavigateAsync(trails);
        Assert.IsTrue(await navigation.NavigateAsync(notes));
        Assert.AreEqual(2, notes.Attempts, "One failed attempt, then one successful initialization.");
    }

    [TestMethod]
    public async Task AnInitialPageIsCurrentWithoutStartingWork()
    {
        var trails = new InitializedPage("Trails");
        var notes = new ActivatedPage("Notes");
        var navigation = new NavigationService([notes, trails], initialPage: trails);

        Assert.AreSame(trails, navigation.CurrentPage);
        Assert.AreEqual(0, trails.Attempts, "Constructing the service starts no work.");

        Assert.IsTrue(await navigation.NavigateAsync(notes));
        Assert.IsTrue(await navigation.NavigateAsync(trails));
        Assert.AreEqual(0, trails.Attempts, "The initial page counts as initialized; the application loads it at startup.");
        Assert.ThrowsExactly<ArgumentException>(() => new NavigationService([notes], initialPage: trails));
    }

    [TestMethod]
    public async Task PagesMustBeKnownAndDistinct()
    {
        var trails = new TestPage("Trails");

        Assert.ThrowsExactly<ArgumentException>(() => new NavigationService([]));
        Assert.ThrowsExactly<ArgumentException>(() => new NavigationService([trails, trails]));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => new NavigationService([trails]).NavigateAsync(new TestPage("Elsewhere")));
    }

    internal class TestPage(string name) : INavigationPage
    {
        public string NavigationName { get; } = name;

        public string? NavigationIcon => null;
    }

    internal sealed class ActivatedPage(string name) : TestPage(name), IPageActivation
    {
        public int Activations { get; private set; }

        public Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            Activations++;
            return Task.CompletedTask;
        }
    }

    internal sealed class InitializedPage(string name) : TestPage(name), IInitializeAsync
    {
        public int Failures { get; set; }

        public TimeSpan Delay { get; set; } = TimeSpan.Zero;

        public int Attempts { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Attempts++;
            await Task.Delay(Delay, cancellationToken);
            if (Failures-- > 0)
            {
                throw new InvalidOperationException("The trail index is unavailable.");
            }
        }
    }

    internal sealed class Guard : INavigationGuard
    {
        public bool Allow { get; set; } = true;

        public TaskCompletionSource<bool>? Pending { get; set; }

        public (INavigationPage From, INavigationPage To)? LastRequest { get; private set; }

        public bool CanNavigate { get; private set; } = true;

        public event EventHandler? CanNavigateChanged;

        public void SetCanNavigate(bool value)
        {
            CanNavigate = value;
            CanNavigateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> ConfirmNavigationAsync(INavigationPage from, INavigationPage to, CancellationToken cancellationToken)
        {
            LastRequest = (from, to);
            return Pending?.Task ?? Task.FromResult(Allow);
        }
    }
}
