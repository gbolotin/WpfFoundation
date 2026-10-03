# WpfFoundation — reusable WPF library and Gallery

The work is delivered in three stages:

- **Stage 0** moves the repositories out of OneDrive, so GitHub carries work between the two PCs.
- **Stage 1** builds WpfFoundation (library, Gallery and tests) and refactors DoViFixer onto it.
- **Stage 2** adds document tabs to the library and refactors the other applications onto WpfFoundation, starting with removing Prism from UPSWarden.

## 1. Solution and boundaries

Create a separate repository at `C:\Users\gbolo\source\repos\WpfFoundation`, published publicly as `gbolotin/WpfFoundation`.

The solution will contain:

- **WpfFoundation** — reusable WPF infrastructure and controls.
- **WpfFoundation.Gallery** — interactive examples and usage reference.
- **WpfFoundation.Tests** — automated behavior and WPF integration tests.

Use **.NET 10**, **MIT**, **CommunityToolkit.Mvvm**, and native Windows Fluent styling. DoViFixer migrates in stage 1; UPSWarden and the other applications migrate in stage 2.

Keep the library independent of application projects and DI containers. Use constructor injection and explicit composition in Gallery startup. Use Toolkit commands, observable objects, and validation directly rather than maintaining equivalent helpers.

**How applications consume it**

- Applications reference the `WpfFoundation` NuGet package, pinned in their `Directory.Packages.props`, and upgrade by changing that version. Release versions follow SemVer from `v*` tags; a removed or renamed public type or resource key is a breaking change.
- CI packs each tagged release with SourceLink and a symbols package (`.snupkg`, `ContinuousIntegrationBuild=true`) and publishes both to NuGet.org. Applications restore from NuGet.org with no extra package source or token. CI publishes through NuGet.org trusted publishing: a policy for `gbolotin/WpfFoundation` and `ci.yml` exchanges the workflow's OIDC token for a short-lived key, so no API key is stored. The `WpfFoundation` id was free on NuGet.org on 2026-10-02; the first preview package claims it.
- NuGet.org packages cannot be deleted, only unlisted, so work in progress ships as prerelease versions (`1.0.0-preview.N`) and only validated releases get stable versions.
- **Local switch:** when the library's source is checked out next to the application (`..\WpfFoundation`, as under `C:\Users\gbolo\source\repos`), the application references `src/WpfFoundation/WpfFoundation.csproj` with a `ProjectReference` instead of the package, so library code can be debugged and edited in the same Visual Studio session. The switch is on whenever that project file exists and is turned off with `-p:UseLocalWpfFoundation=false`. The property goes in the application's `Directory.Build.props`, and the references go in the application project:

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

# Stage 0 — Repositories out of OneDrive

## 3. Repository move

The repositories currently live in `C:\Users\gbolo\OneDrive\source\repos` so that work can continue on the second PC. OneDrive syncs `.git`, `bin` and `obj` file by file while Visual Studio and git write to them, which locks files and can corrupt a repository. Move every repository to `C:\Users\gbolo\source\repos` on both PCs and let GitHub carry work between them.

- Push every repository from the OneDrive folder, including work-in-progress branches (`git push --all`), until the script below reports nothing. Give any repository without a GitHub remote a private GitHub repository first.
- Clone each repository into `C:\Users\gbolo\source\repos` on both PCs, with the same folder names, so `..\WpfFoundation` resolves on both for the local switch.
- Recreate per-PC state that git does not carry, such as local settings files and user secrets.
- Build each solution on both PCs, then delete the OneDrive copy once. Deleting it on one PC also removes it from the other.
- From then on, push before leaving a PC and pull at the other. Unfinished work goes on a branch as a work-in-progress commit.

This script lists uncommitted changes and unpushed commits in every repository under `$root`. Run it against the OneDrive folder before the move, and against `C:\Users\gbolo\source\repos` before switching PCs:

```powershell
$root = 'C:\Users\gbolo\OneDrive\source\repos'
Get-ChildItem $root -Directory | ForEach-Object {
  $dirty = git -C $_.FullName status --porcelain
  $unpushed = git -C $_.FullName log --branches --not --remotes --oneline
  if ($dirty -or $unpushed) { "== $($_.Name)"; $dirty; $unpushed }
}
```

Stage 0 is done when every repository builds from `C:\Users\gbolo\source\repos` on both PCs and no repository remains under OneDrive.

# Stage 1 — WpfFoundation and DoViFixer

Build stage 1 in slices. Each slice adds part of the library with its Gallery page and tests, ships as a preview package, and DoViFixer moves onto that part before the next slice starts. API mistakes then surface while only one slice depends on them.

| Slice | Library and Gallery | DoViFixer |
|---|---|---|
| **1. Setup** | Repository setup, CI, packaging and the Gallery shell; the first preview claims the NuGet.org id | References the package with the local switch, adds `DoViFixer.Local.sln` and its own CI |
| **2. Resources and behaviors** | Fluent resources and controls, behaviors and converters, with their Gallery pages | Replaces `Presentation/Common` and `Resources/Common.xaml` |
| **3. Theme and dialogs** | Theme service, `IDialogService`, `IFileDialogService` and the Dialogs page | Replaces `ThemeService` and `IUserDialogs` |
| **4. Navigation** | Navigation service, sidebar and the Navigation page | Replaces the `ShellViewModel` page coordination and `PageHost` |

All four slices shipped together as `v1.0.0-preview.1`, which DoViFixer pins. Stage 1 ends by tagging `v1.0.0` once the hands-on check below passes, and DoViFixer pins that version.

## 4. Library implementation

**Repository setup**

- Copy DoViFixer's `global.json` (SDK 10.0.400, `latestPatch`), `Directory.Build.props` (nullable, implicit usings, C# 14, deterministic builds, warnings as errors), `Directory.Packages.props` for central package versions, and `.editorconfig`.
- Suppress `WPF0001` once in `Directory.Build.props` with `<NoWarn>$(NoWarn);WPF0001</NoWarn>`. Fluent `ThemeMode` is still marked experimental, so with warnings as errors the build fails without it. Every project in this repository uses WPF, so one repository-wide line replaces DoViFixer's per-project entries. Applications that still set `ThemeMode` in their own code or XAML keep their own suppression.
- The agent rules are already in the repository: `AGENTS.md`, `docs/agents/common-rules.md` (copied from DoViFixer, with only the link to the repository-specific rules changed, so the shared rules stay identical across repositories) and `docs/agents/wpffoundation-rules.md`. Keep them current as the library grows.

**Fluent resources and controls**

- Provide one documented resource-dictionary entry point, with prefixed resource keys to avoid collisions.
- Build on native Fluent control styles and dynamic theme resources. Support System, Light, and Dark through a small theme service using WPF's existing mechanism. [Microsoft Fluent documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
- Include heading, label, card, primary-button, transparent-button, icon-content, settings-row, toggle-switch, and status-item styles. Build them on the Fluent theme's own brushes, text styles and corner radius resources and its 4 px spacing grid, rather than adding new colors, font sizes or radii.
- Use stock WPF controls where sufficient: settings rows use `HeaderedContentControl`; toggle switches retain `CheckBox` behavior and accessibility.
- Adapt `MarqueeTextBlock` with configurable speed, gap, and animation enablement. Stop animation when hidden or unloaded and respect Windows animation preferences.

**Navigation**

- Introduce `INavigationPage`, `INavigationService`, and a reusable sidebar/retained-content presentation.
- Expose page collection, current page, navigation availability, and awaitable navigation. Resolve views through explicit WPF DataTemplates.
- Support asynchronous initialization and application-supplied navigation guards. Initialize successfully once; allow retry after failure. Rejected, failed, or cancelled transitions preserve the current selection: the service raises the current-page change again on the dispatcher, so a bound sidebar `ListBox` returns to the current item.
- Retain each visited page's view and ViewModel until its navigation host is disposed.
- Keep status content optional and separate from the navigation-page contract.
- Document tabs follow in stage 2, when UPSWarden needs them.

**Dialogs**

- Provide `IDialogService` for messages, confirmations, long-text review, and modal custom forms; provide a separate `IFileDialogService` for native open/save/folder selection.
- Host every dialog in one library `DialogWindow`. Its content is the dialog ViewModel, and an implicit DataTemplate for that ViewModel type supplies the body. Messages, confirmations and review are built-in ViewModels with templates in the library resources; applications register templates for their own forms. Nothing uses the native `MessageBox`, which ignores the dark theme.
- The window owns the shared chrome: title, owner and centering, the theme, size-to-content with minimum and maximum sizes (resizable only when the ViewModel asks, as review does), and a footer of buttons the ViewModel describes, with default and cancel buttons marked.
- Dialog ViewModels never reference the window. They finish through a result contract that the window observes, and `IDialogService` awaits that result.
- Fail fast in debug builds when no DataTemplate exists for a dialog ViewModel, instead of showing its type name.
- Custom forms use typed ViewModels and DataTemplates, awaitable results, validation-aware acceptance, and cancellation without committing edits.
- Resolve the correct owner window, restore focus, and handle Escape/window-close as cancellation. Buttons name the action (for example "Delete" and "Keep") rather than Yes/No, and destructive confirmations default to the safe choice.
- Keep application wording, save operations, and business validation outside the dialog implementation.

**Behaviors and converters**

- Adapt `ColumnSort<T>`, `GridViewSort`, `GridViewSizing`, and `FileDrop`.
- Preserve source collection order during sorting, support custom comparers and sorting guards, and show header direction indicators.
- Give independent lists separate collection views; handle source replacement and detach subscriptions correctly.
- Keep drop acceptance in the bound command and execute only on drop.
- Include the used inverse-boolean and reference-equality visibility converters. Reuse WPF's built-in boolean-to-visibility converter.

## 5. Gallery experience

Use WpfFoundation's own sidebar navigation and retained pages throughout Gallery.

| Page | Included examples |
|---|---|
| **Overview** | Setup, resource merging, project structure, minimal usage |
| **Styled controls** | Only the stock controls WpfFoundation styles: headings, labels, primary and transparent buttons, and icon content. Microsoft's WPF Gallery app already shows the other stock Fluent controls, so this page points to it instead of repeating them |
| **Custom UI** | Cards, settings rows, toggle switches, icon buttons, status items and marquee |
| **Navigation** | Retained state, initialization and blocked navigation |
| **Dialogs** | Messages, confirmation, review, native pickers and a validated sample form |
| **Behaviors** | Sorting, column sizing, drag/drop and converter examples |

Each example includes working interaction, relevant property controls, and copyable usage XAML. Use fictional sample data.

Default to System theme, with immediate Light/Dark switching. No live XAML editor or general property inspector in v1. For colors, typography, spacing, corner radius and icons, the Gallery points to the Design Guidance section of Microsoft's WPF Gallery app instead of repeating it. Document tab examples follow in stage 2.

## 6. DoViFixer refactoring

Move DoViFixer onto WpfFoundation without changing what users see or do, one slice at a time, pinning each preview package as it ships.

- Reference the `WpfFoundation` package from `DoViFixer.App` with the local switch, and add `DoViFixer.Local.sln` with the library project for local work.
- Replace `Presentation/Common` with the library and CommunityToolkit.Mvvm: `ObservableObject`, `RelayCommand` and `AsyncCommand` become the Toolkit's `ObservableObject`, `RelayCommand` and `AsyncRelayCommand`; the converters, `ColumnSort`, `GridViewSort`, `GridViewSizing` and `FileDrop` come from the library.
- Replace `Resources/Common.xaml` with the library entry dictionary and update views to the prefixed resource keys. Keep only DoViFixer-specific resources in the application.
- Replace the `ShellViewModel` page coordination and the `PageHost` presenter with the library navigation service and sidebar. Busy operations and settings saving become an application-supplied navigation guard. Status items move out of the page contract into the shell.
- Replace `IUserDialogs`/`UserDialogs` with `IDialogService` and `IFileDialogService`. Opening the log folder stays in DoViFixer.
- Replace `ThemeService` with the library theme service, mapping DoViFixer's `AppTheme` setting to it.
- Add a Windows GitHub Actions workflow to DoViFixer, which has no CI today. It builds in Release and runs the tests except `NativeIntegration` with `-p:UseLocalWpfFoundation=false`, so every push proves DoViFixer works against the pinned package. Both PCs have `..\WpfFoundation` next to DoViFixer, so a release built on a PC would silently use local, possibly uncommitted library code; release builds come from this workflow or pass the same flag.
- Update DoViFixer's agent rules and `docs/architecture.md`, which still describe an optional `DoViFixer.Common.Wpf` project.

## 7. Stage 1 validation and delivery

- Test navigation success, rejection, cancellation, initialization retry, retained state, overlapping requests, and that a rejected navigation restores the sidebar selection.
- Test sorting direction, custom comparers, unchanged source order, independent views, source replacement, and subscription lifetimes.
- Test drop acceptance, converter edge cases, dialog results, validation, and marquee lifecycle.
- Run WPF integration checks on an STA dispatcher. Render all Gallery pages in Light and Dark.
- Before tagging `v1.0.0`, check DoViFixer and every Gallery page by hand on a real desktop:
  - **High contrast:** turn on a Windows contrast theme; all text, borders, focus rectangles, toggle switches, sort indicators and the sidebar selection stay visible.
  - **Keyboard only:** reach every control with Tab and the arrow keys in a sensible order, see where focus is, toggle switches with Space, navigate the sidebar with the arrow keys, and use dialogs with Enter, Escape and Tab, with focus returning afterwards.
  - **DPI scales:** at 100%, 150% and 200% scaling, and after moving a window between monitors with different scaling, nothing is clipped or blurry and the last list column still fills the width.
- Verify resource loading from the packed library: build and run DoViFixer with `UseLocalWpfFoundation=false` against the package, not only through project references.
- DoViFixer's existing test projects pass, and its pages, settings, dialogs and theme switching behave as before. The one intended difference is that page views are created on first visit instead of at startup.
- Add Windows CI for Release build, tests, NuGet packing with SourceLink and symbols, publishing tagged releases to NuGet.org, and a downloadable framework-dependent Gallery ZIP.
- Enforce the SemVer rule from `v1.0.0` on. Enable the SDK's package validation (`EnablePackageValidation`) with `PackageValidationBaselineVersion` set to the last stable release, so packing fails when a public type or member is removed or renamed. Add a test that compares the entry dictionary's resource keys with a list committed in the repository, so a removed or renamed key fails until the list and the major version change. Previews before `v1.0.0` may still break.
- Include README, examples, extraction notes, MIT license, and applicable third-party notices.

Stage 1 provides public source, a NuGet.org package, and a Gallery ZIP. Installers and additional themes are deferred.

# Stage 2 — Other applications

Every application that adopts WpfFoundation, starting with UPSWarden, follows the same UI design guidance as WpfFoundation and DoViFixer: Microsoft's Windows app design guidance (https://learn.microsoft.com/windows/apps/design/), with the Design Guidance section of Microsoft's WPF Gallery app and the WPF Fluent theme taking precedence for WPF-specific brush, text style and corner radius keys, spacing values and icon glyphs. As part of its migration, each application:

- Copies `docs/agents/common-rules.md` unchanged (it carries the UI design rules) and links it from its `AGENTS.md`, next to its own repository-specific rules.
- Adds the XAML design check to its CI with the shared composite action, as DoViFixer does, plus a `xaml-design-allowlist.txt` that lists only deliberate, explained exceptions.
- Replaces its own literal colors, font sizes, margins and corner radii with Fluent theme resources and the standard spacing values, rather than allowlisting them.

## 8. Document tabs

Build document tabs in WpfFoundation before UPSWarden migrates, designed against UPSWarden's device tabs as their first consumer.

- Add `DocumentItem`, `DocumentWorkspace`, and document-tab presentation. Opening an existing document key activates its existing tab.
- Host document content in a retained-items presenter (one view per open document, only the active one visible), with the tab headers as a separate selector. A stock `TabControl` rebuilds templated content on every switch, so it cannot retain views.
- Support asynchronous close approval. Closing an inactive tab preserves selection; closing the active tab selects its left neighbor, then its right neighbor, then an empty state.
- Release retained document views on close. Application callbacks own ViewModel cleanup; the library does not automatically dispose borrowed objects.
- Add document tab examples to the Gallery, covering duplicate-document activation and close guards with fictional data.
- Test document identity, close rejection, neighbor selection, view release, and cleanup callbacks.
- Release the tabs as a new minor package version, which UPSWarden then pins.

## 9. UPSWarden migration

Remove Prism and Unity from `UPSWarden.Presentation.Wpf`, `UPSWarden.Common.Wpf` and `UPSWarden.SnmpAgent.App`, and move both applications onto WpfFoundation.

| Prism or Unity usage today | Replacement |
|---|---|
| `PrismApplication` startup and the Unity container (`IContainerRegistry`, `IUnityContainer`) | Standard WPF `Application` startup with Microsoft.Extensions.DependencyInjection composition, as in DoViFixer |
| Regions and `RequestNavigate` (`IRegionManager`, `INavigationAware`, `NavigationContext`) | WpfFoundation navigation service and sidebar |
| Main `TabControl` region and `HeaderWithCloseButtonViewModel` | `DocumentWorkspace` and document tabs, one document per device |
| Status bar region | Shell-owned status presentation |
| Prism `IDialogService`/`IDialogAware` and `CustomDialogWindow` | WpfFoundation `IDialogService` custom forms in the shared `DialogWindow` (the same one-window, ViewModel-per-dialog model) |
| `ViewModelLocator` auto-wiring | Explicit DataTemplates and constructor injection |
| `BindableBase` and `DelegateCommand` | CommunityToolkit.Mvvm `ObservableObject`, `RelayCommand` and `AsyncRelayCommand` |
| `UnityDeviceViewModelFactory` | A small device ViewModel factory registered in DI |
| `ThemeManager` and `Colors.Light/Dark.xaml` | WpfFoundation theme service; keep only UPSWarden-specific colors |

- Reference WpfFoundation the same way as DoViFixer: the package plus the local switch, a local solution file with the library project, and a CI build with the switch off.
- Keep `UPSWarden.Common.Wpf` as UPSWarden's shared presentation project for its two applications, without Prism.
- Update UPSWarden's `AGENTS.md`, which currently prescribes Prism navigation and regions, and add the shared `common-rules.md` with its UI design rules.
- Add the XAML design check to UPSWarden's CI and fix its findings, keeping only explained exceptions in the allowlist.
- Version UPSWarden like DoViFixer: one SemVer `VersionPrefix` in `Directory.Build.props` for every assembly, with `VersionSuffix` defaulting to `dev`; CI builds pass `-p:VersionSuffix=ci.<run>`, and a `vX.Y.Z` tag builds `X.Y.Z`. Show the version in both applications (for example the settings or about view) and in their logs.
- A `v*` tag creates a GitHub Release with framework-dependent ZIPs of both applications, marked as a pre-release when the tag has a suffix. Tags are pushed only with Gabi's approval.

## 10. UPSWarden validation

- Both applications start, navigate, open and close device tabs (including duplicate-device activation and close approval), show dialogs, and switch themes.
- `UPSWarden.SnmpAgent.Tests` passes, and no project references a Prism or Unity package.
- The XAML design check passes, and both applications follow the design guidance in Light, Dark and high contrast.
- CI builds carry a `ci.<run>` version, and both applications show it.

## 11. Remaining applications

AudioAwake and AlbumFixer adopt WpfFoundation after UPSWarden; write each one's scope before it starts. Each follows the design guidance and adopts the XAML design check as described at the start of stage 2. AlbumFixer keeps its separate dark theme, so its theme colors are allowlisted, while layout, typography, spacing and wording still follow the guidance. PatternBuilder will require a .NET upgrade before adoption.
