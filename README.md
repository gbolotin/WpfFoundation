# WpfFoundation

Reusable WPF infrastructure and an interactive Gallery for .NET 10, using the native Windows Fluent theme and CommunityToolkit.Mvvm, without Prism.

The library is being built in slices; see the [implementation plan](https://github.com/gbolotin/WpfFoundation/blob/main/docs/implementation-plan.md). Preview packages (`1.0.0-preview.N`) may change their API before `1.0.0`.

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

## Repository

| Project | Purpose |
|---|---|
| `src/WpfFoundation` | The library and NuGet package |
| `src/WpfFoundation.Gallery` | Interactive examples and copyable usage XAML |
| `tests/WpfFoundation.Tests` | Behavior and WPF integration tests |

Build and test with the .NET 10 SDK pinned in `global.json`:

```bash
dotnet build WpfFoundation.sln
dotnet test WpfFoundation.sln
```

CI builds, tests and packs every push. A `v*` tag publishes the package and symbols to NuGet.org and attaches a framework-dependent Gallery ZIP to the GitHub release.

The Gallery shows what WpfFoundation adds. For stock Fluent controls and for colors, typography, spacing, corner radius and icons, use the Design Guidance section of Microsoft's [WPF Gallery](https://apps.microsoft.com/detail/9ndwt4zcf7l3) app.

Code adapted from the owner's other repositories is recorded in [provenance notes](https://github.com/gbolotin/WpfFoundation/blob/main/docs/provenance.md).

Licensed under the [MIT License](https://github.com/gbolotin/WpfFoundation/blob/main/LICENSE).
