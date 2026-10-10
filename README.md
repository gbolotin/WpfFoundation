# WpfFoundation

Reusable WPF infrastructure and an interactive Gallery for .NET 10, using the native Windows Fluent theme and CommunityToolkit.Mvvm, without Prism.

See the [implementation plan](https://github.com/gbolotin/WpfFoundation/blob/main/docs/implementation-plan.md) for what is built and what comes next. Stable versions follow SemVer from `1.0.0`: a removed or renamed public type, member or resource key needs a new major version. Preview packages (`X.Y.Z-preview.N`) may still change the API they add.

## Getting started

Reference the package and pin its version in `Directory.Packages.props`:

```xml
<PackageReference Include="WpfFoundation" />
```

Merge the single entry dictionary once in `App.xaml` and keep `ThemeMode` on the application so the Fluent theme applies:

```xml
<Application ThemeMode="System">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="pack://application:,,,/WpfFoundation;component/Themes/WpfFoundation.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

Every resource key the library defines starts with `Wf`, so it cannot collide with application resources. Setting `ThemeMode` is still reported as experimental (`WPF0001`), so applications that set it suppress that warning.

### Taskbar progress and notifications

`IOperationFeedback` shows a long operation's progress and outcome on the taskbar button. For Windows notifications when the window isn't active, also reference `WpfFoundation.Notifications`, which targets `net10.0-windows10.0.19041.0` and adds about 25 MB of Windows SDK projection to the app. Register at every start, before the main window is shown:

```csharp
var notifications = new WindowsNotificationService(Application.Current);
notifications.Register("Contoso.PhotoImporter", "Photo Importer", Path.Combine(AppContext.BaseDirectory, "app.ico"));
var feedback = new OperationFeedback(new TaskbarService(Application.Current), notifications);
```

`Register` writes the app's name and icon under `HKCU\Software\Classes`, and creates a per-user Start menu shortcut and a COM activator. Windows needs these to show the notification and to bring the app to the front when it is clicked. `Unregister` removes them.

## Repository

| Project | Purpose |
|---|---|
| `src/WpfFoundation` | The library and NuGet package |
| `src/WpfFoundation.Notifications` | The optional Windows notifications package, published with the same version |
| `src/WpfFoundation.Gallery` | Interactive examples and copyable usage XAML |
| `tests/WpfFoundation.Tests` | Behavior and WPF integration tests |
| `tools/XamlDesignCheck` | The XAML design check that CI runs here and in the apps |
| `tools/IconImport` | Regenerates the vector icon resources from Fluent UI System Icons |

Build and test with the .NET 10 SDK pinned in `global.json`:

```bash
dotnet build WpfFoundation.sln
dotnet test WpfFoundation.sln
```

CI runs the XAML design check, then builds, tests and packs every push. A `v*` tag publishes the package and symbols to NuGet.org and attaches a framework-dependent Gallery ZIP to the GitHub release.

The Gallery shows what WpfFoundation adds. For stock Fluent controls and for colors, typography, spacing, corner radius and icons, use the Design Guidance section of Microsoft's [WPF Gallery](https://apps.microsoft.com/detail/9ndwt4zcf7l3) app.

### Vector icons

`WfIcon*` resources are icon outlines from [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) (MIT), the open source set in the same style as Segoe Fluent Icons, on a 16 px grid. Show one with `PathIcon`, which takes the foreground around it, or as the `Tag` of a button using `WfIconContentTemplate`:

```xml
<wf:PathIcon Data="{StaticResource WfIconAddFiles}" Size="24" />
<Button Content="Add folder" Tag="{StaticResource WfIconAddFolder}" ContentTemplate="{StaticResource WfIconContentTemplate}" />
```

To add an icon, add a line to `tools/IconImport/icons.txt`, run `dotnet run tools/IconImport/ImportIcons.cs`, and add the new key to `tests/WpfFoundation.Tests/ResourceKeys.txt`. Each key is part of the SemVer contract, so add icons only when an application uses them.

### XAML design check

`tools/XamlDesignCheck` fails CI when XAML bypasses the Fluent theme: literal colors or `SystemColors`, margins and padding off the spacing steps (0, 4, 8, 12, 16, 24, 32, 48), literal font sizes, weights or families, and literal corner radii. Run it before opening a UI pull request:

```bash
dotnet run --project tools/XamlDesignCheck
```

A justified exception goes in `xaml-design-allowlist.txt` with its reason. Applications run the same check from their CI:

```yaml
- uses: gbolotin/WpfFoundation/.github/actions/xaml-design-check@<commit>
```

Code adapted from the owner's other repositories is recorded in [provenance notes](https://github.com/gbolotin/WpfFoundation/blob/main/docs/provenance.md).

Licensed under the [MIT License](https://github.com/gbolotin/WpfFoundation/blob/main/LICENSE).
