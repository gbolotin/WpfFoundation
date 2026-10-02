# WpfFoundation — reusable WPF library and Gallery

The work is delivered in two stages:

- **Stage 1** builds WpfFoundation (library, Gallery and tests) and refactors DoViFixer onto it.
- **Stage 2** removes Prism from UPSWarden and moves it onto WpfFoundation.

AudioAwake, AlbumFixer and PatternBuilder adoption is not planned in either stage.

## 1. Solution and boundaries

Create a separate repository at `C:\Users\gbolo\OneDrive\source\repos\WpfFoundation`, published publicly as `gbolotin/WpfFoundation`.

The solution will contain:

- **WpfFoundation** — reusable WPF infrastructure and controls.
- **WpfFoundation.Gallery** — interactive examples and usage reference.
- **WpfFoundation.Tests** — automated behavior and WPF integration tests.

Use **.NET 10**, **MIT**, **CommunityToolkit.Mvvm**, and native Windows Fluent styling. DoViFixer migrates in stage 1 and UPSWarden in stage 2; other applications remain unchanged.

Keep the library independent of application projects and DI containers. Use constructor injection and explicit composition in Gallery startup. Use Toolkit commands, observable objects, and validation directly rather than maintaining equivalent helpers.

**How applications consume it**

- Applications reference the `WpfFoundation` NuGet package, pinned in their `Directory.Packages.props`, and upgrade by changing that version. Release versions follow SemVer from `v*` tags; a removed or renamed public type or resource key is a breaking change.
- CI packs each tagged release with SourceLink and a symbols package (`ContinuousIntegrationBuild=true`) and publishes it to GitHub Packages. Restoring from GitHub Packages needs a token with `read:packages`, so each application's `nuget.config` adds that source and each machine or CI that restores supplies the token.
- **Local switch:** when the library's source is checked out next to the application (`..\WpfFoundation`, as under `C:\Users\gbolo\OneDrive\source\repos`), the application references `src/WpfFoundation/WpfFoundation.csproj` with a `ProjectReference` instead of the package, so library code can be debugged and edited in the same Visual Studio session. The switch is on whenever that project file exists and is turned off with `-p:UseLocalWpfFoundation=false`. The property goes in the application's `Directory.Build.props`, and the references go in the application project:

  ```xml
  <PropertyGroup>
    <WpfFoundationProject>$(MSBuildThisFileDirectory)..\WpfFoundation\src\WpfFoundation\WpfFoundation.csproj</WpfFoundationProject>
    <UseLocalWpfFoundation Condition="'$(UseLocalWpfFoundation)' == '' and Exists('$(WpfFoundationProject)')">true</UseLocalWpfFoundation>
  </PropertyGroup>
  ```

  ```xml
  <ItemGroup Condition="'$(UseLocalWpfFoundation)' == 'true'">
    <ProjectReference Include="$(WpfFoundationProject)" />
  </ItemGroup>
  <ItemGroup Condition="'$(UseLocalWpfFoundation)' != 'true'">
    <PackageReference Include="WpfFoundation" />
  </ItemGroup>
  ```

- The committed solution lists only the application's own projects, so it builds anywhere. A second solution file (for example `DoViFixer.Local.sln`) also lists the library project for local work.
- Application CI and release builds set `UseLocalWpfFoundation=false`, so a shipped build always uses a published package.

**Licensing and provenance**

- Copy only code written by the repository owner. DoViFixer is GPL-3.0 because it adapts the workflows of [dovi_convert](https://github.com/cryptochrome/dovi_convert) (GPL-3.0, a Python command-line tool). The DoViFixer files reused here are WPF presentation code with no counterpart in dovi_convert, so they can be published under MIT. Nothing from DoViFixer's media workflows is copied.
- Record each copied or adapted file with its source repository and path in the provenance notes.

## 2. Findings and extraction decisions

| Project inspected | Worth reusing or adapting | Keep application-specific |
|---|---|---|
| **DoViFixer** | Retained navigation pattern, sorting, column sizing, file-drop behavior, converters, Fluent styles, settings rows, toggle switches, status presentation | Media workflows, settings persistence, dependency checks, operation guards |
| **UPSWarden**, including SnmpAgent.App | Closable document-tab behavior, custom-form dialog requirements, status and validation presentation | Device ViewModels, polling, SNMP, persistence. Prism regions and Unity factories are removed in stage 2 rather than kept |
| **AudioAwake** | `MarqueeTextBlock` | Tray integration, screensaver lifecycle, media-session handling |
| **AlbumFixer** | File/folder selection, confirmation ownership and defaults, drag/drop patterns | Album-processing rules and its separate dark theme |
| **PatternBuilder** | Typed DataTemplate and editable-form examples | Regex processing and segment models |
| **WpfApp1** | Fluent setup reference | No additional reusable implementation |
| **UpsSnmpSimulator** | Form and validation examples | Simulator state and SNMP operations |

Other inspected repositories provide no additional WPF extraction candidates.

DoViFixer's **current** reference is a Fluent `ListBox` sidebar and retained page presentations. Its navigation coordination currently resides in `ShellViewModel`; extract the reusable behavior without transferring its application dependencies.

Document these decisions and source provenance in the new repository.

# Stage 1 — WpfFoundation and DoViFixer

## 3. Library implementation

**Fluent resources and controls**

- Provide one documented resource-dictionary entry point, with prefixed resource keys to avoid collisions.
- Build on native Fluent control styles and dynamic theme resources. Support System, Light, and Dark through a small theme service using WPF's existing mechanism. [Microsoft Fluent documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
- Include heading, label, card, primary-button, transparent-button, icon-content, settings-row, toggle-switch, and status-item styles.
- Use stock WPF controls where sufficient: settings rows use `HeaderedContentControl`; toggle switches retain `CheckBox` behavior and accessibility.
- Adapt `MarqueeTextBlock` with configurable speed, gap, and animation enablement. Stop animation when hidden or unloaded and respect Windows animation preferences.

**Navigation and document tabs**

- Introduce `INavigationPage`, `INavigationService`, and a reusable sidebar/retained-content presentation.
- Expose page collection, current page, navigation availability, and awaitable navigation. Resolve views through explicit WPF DataTemplates.
- Support asynchronous initialization and application-supplied navigation guards. Initialize successfully once; allow retry after failure. Rejected, failed, or cancelled transitions preserve the current selection: the service raises the current-page change again on the dispatcher, so a bound sidebar `ListBox` returns to the current item.
- Retain each visited page's view and ViewModel until its navigation host is disposed.
- Add `DocumentItem`, `DocumentWorkspace`, and document-tab presentation, designed against UPSWarden's device tabs, its first consumer in stage 2. Opening an existing document key activates its existing tab.
- Host document content in a retained-items presenter (one view per open document, only the active one visible), with the tab headers as a separate selector. A stock `TabControl` rebuilds templated content on every switch, so it cannot retain views.
- Support asynchronous close approval. Closing an inactive tab preserves selection; closing the active tab selects its left neighbor, then its right neighbor, then an empty state.
- Release retained document views on close. Application callbacks own ViewModel cleanup; the library does not automatically dispose borrowed objects.
- Keep status content optional and separate from the navigation-page contract.

**Dialogs**

- Provide `IDialogService` for messages, confirmations, long-text review, and modal custom forms; provide a separate `IFileDialogService` for native open/save/folder selection.
- Custom forms use typed ViewModels and DataTemplates, awaitable results, validation-aware acceptance, and cancellation without committing edits.
- Resolve the correct owner window, restore focus, and handle Escape/window-close as cancellation. Destructive confirmations default to No.
- Keep application wording, save operations, and business validation outside the dialog implementation.

**Behaviors and converters**

- Adapt `ColumnSort<T>`, `GridViewSort`, `GridViewSizing`, and `FileDrop`.
- Preserve source collection order during sorting, support custom comparers and sorting guards, and show header direction indicators.
- Give independent lists separate collection views; handle source replacement and detach subscriptions correctly.
- Keep drop acceptance in the bound command and execute only on drop.
- Include the used inverse-boolean and reference-equality visibility converters. Reuse WPF's built-in boolean-to-visibility converter.

## 4. Gallery experience

Use WpfFoundation's own sidebar navigation and retained pages throughout Gallery.

| Page | Included examples |
|---|---|
| **Overview** | Setup, resource merging, project structure, minimal usage |
| **Colors** | Fluent and library resources, live swatches, resource names, resolved values, copy actions |
| **Icons** | Searchable names/codepoints, glyph previews, sizes, copyable XAML |
| **Typography** | Fluent text styles, searchable installed fonts, editable sample text |
| **Base controls** | Buttons, text inputs, selectors, lists, trees, tabs, menus, progress, dates, expanders and layout |
| **Custom UI** | Cards, settings rows, toggle switches, icon buttons, status items and marquee |
| **Navigation and tabs** | Retained state, initialization, blocked navigation, duplicate-document activation and close guards |
| **Dialogs** | Messages, confirmation, review, native pickers and a validated sample form |
| **Behaviors** | Sorting, column sizing, drag/drop and converter examples |

Each example includes working interaction, relevant property controls, and copyable usage XAML. Use fictional sample data.

Default to System theme, with immediate Light/Dark switching. Use installed Windows icon fonts with supported-glyph fallback; do not redistribute Windows font files. No live XAML editor or general property inspector in v1.

## 5. DoViFixer refactoring

Move DoViFixer onto WpfFoundation without changing what users see or do.

- Reference the `WpfFoundation` package from `DoViFixer.App` with the local switch, add the GitHub Packages source to `nuget.config`, and add `DoViFixer.Local.sln` with the library project for local work.
- Replace `Presentation/Common` with the library and CommunityToolkit.Mvvm: `ObservableObject`, `RelayCommand` and `AsyncCommand` become the Toolkit's `ObservableObject`, `RelayCommand` and `AsyncRelayCommand`; the converters, `ColumnSort`, `GridViewSort`, `GridViewSizing` and `FileDrop` come from the library.
- Replace `Resources/Common.xaml` with the library entry dictionary and update views to the prefixed resource keys. Keep only DoViFixer-specific resources in the application.
- Replace the `ShellViewModel` page coordination and the `PageHost` presenter with the library navigation service and sidebar. Busy operations and settings saving become an application-supplied navigation guard. Status items move out of the page contract into the shell.
- Replace `IUserDialogs`/`UserDialogs` with `IDialogService` and `IFileDialogService`. Opening the log folder stays in DoViFixer.
- Replace `ThemeService` with the library theme service, mapping DoViFixer's `AppTheme` setting to it.
- Update DoViFixer's agent rules and `docs/architecture.md`, which still describe an optional `DoViFixer.Common.Wpf` project.

## 6. Stage 1 validation and delivery

- Test navigation success, rejection, cancellation, initialization retry, retained state, overlapping requests, and that a rejected navigation restores the sidebar selection.
- Test document identity, close rejection, neighbor selection, view release, and cleanup callbacks.
- Test sorting direction, custom comparers, unchanged source order, independent views, source replacement, and subscription lifetimes.
- Test drop acceptance, converter edge cases, dialog results, validation, and marquee lifecycle.
- Run WPF integration checks on an STA dispatcher. Visually check all Gallery pages in Light/Dark, keyboard navigation, focus, high contrast, and representative DPI scales.
- Verify resource loading from the packed library: build and run DoViFixer with `UseLocalWpfFoundation=false` against the package, not only through project references.
- DoViFixer's existing test projects pass, and its pages, settings, dialogs and theme switching behave as before. The one intended difference is that page views are created on first visit instead of at startup.
- Add Windows CI for Release build, tests, NuGet packing with SourceLink and symbols, publishing tagged releases to GitHub Packages, and a downloadable framework-dependent Gallery ZIP.
- Include README, examples, extraction notes, MIT license, and applicable third-party notices.
- Implement on `codex/initial-foundation`; publish the validated initial version with `main` as the public repository's default branch, and publish its tag as the first package version DoViFixer pins.

Stage 1 provides public source, a NuGet package on GitHub Packages, and a Gallery ZIP. NuGet.org publishing, installers, and additional themes are deferred.

# Stage 2 — UPSWarden without Prism

## 7. UPSWarden migration

Remove Prism and Unity from `UPSWarden.Presentation.Wpf`, `UPSWarden.Common.Wpf` and `UPSWarden.SnmpAgent.App`, and move both applications onto WpfFoundation.

| Prism or Unity usage today | Replacement |
|---|---|
| `PrismApplication` startup and the Unity container (`IContainerRegistry`, `IUnityContainer`) | Standard WPF `Application` startup with Microsoft.Extensions.DependencyInjection composition, as in DoViFixer |
| Regions and `RequestNavigate` (`IRegionManager`, `INavigationAware`, `NavigationContext`) | WpfFoundation navigation service and sidebar |
| Main `TabControl` region and `HeaderWithCloseButtonViewModel` | `DocumentWorkspace` and document tabs, one document per device |
| Status bar region | Shell-owned status presentation |
| Prism `IDialogService`/`IDialogAware` and `CustomDialogWindow` | WpfFoundation `IDialogService` custom forms |
| `ViewModelLocator` auto-wiring | Explicit DataTemplates and constructor injection |
| `BindableBase` and `DelegateCommand` | CommunityToolkit.Mvvm `ObservableObject`, `RelayCommand` and `AsyncRelayCommand` |
| `UnityDeviceViewModelFactory` | A small device ViewModel factory registered in DI |
| `ThemeManager` and `Colors.Light/Dark.xaml` | WpfFoundation theme service; keep only UPSWarden-specific colors |

- Reference WpfFoundation the same way as DoViFixer: the package plus the local switch, and a local solution file with the library project.
- Keep `UPSWarden.Common.Wpf` as UPSWarden's shared presentation project for its two applications, without Prism.
- Update UPSWarden's `AGENTS.md`, which currently prescribes Prism navigation and regions.

## 8. Stage 2 validation

- Both applications start, navigate, open and close device tabs (including duplicate-device activation and close approval), show dialogs, and switch themes.
- `UPSWarden.SnmpAgent.Tests` passes, and no project references a Prism or Unity package.

PatternBuilder will require a .NET upgrade before adoption.
