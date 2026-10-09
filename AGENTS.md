# WpfFoundation-specific rules

Apply these rules together with the common development rules in the parent `AGENTS.md` shared across the owner's repositories. The staged implementation plan is documented in [docs/implementation-plan.md](docs/implementation-plan.md).

## Project architecture

WpfFoundation is a reusable WPF library for .NET 10, published as the `WpfFoundation` NuGet package under the MIT license, with an interactive Gallery app.

Main projects:

- WpfFoundation: reusable WPF infrastructure, styles, controls, behaviors, converters, navigation, dialogs, and the theme service.
- WpfFoundation.Gallery: interactive examples and usage reference for the library.
- WpfFoundation.Tests: behavior tests and WPF integration tests.

The domain, application, and infrastructure layering rules in the common rules apply to the applications that consume this library, not to the library itself.

## Library boundaries

- Keep the library independent of application projects. Never reference an application or copy application workflows, settings persistence, or business rules into it.
- Do not depend on a DI container. Use constructor injection; applications and the Gallery compose the services explicitly at startup.
- Use CommunityToolkit.Mvvm types (`ObservableObject`, `RelayCommand`, `AsyncRelayCommand`, `ObservableValidator`) directly. Do not add equivalent helpers.
- Keep application wording, save operations, and business validation outside library components.
- Add reusable code only when an application already needs it; the Gallery alone is not a reason to add a feature.
- Ask before adding new production dependencies.

## Public API and resource keys

- Public types, public members, and resource keys are the package's SemVer contract. Removing or renaming any of them is a breaking change and requires a major version.
- Prefix every resource key the library defines with `Wf` (for example `WfHeadingTextStyle`), so keys cannot collide with application resources.
- Expose resources through the single documented entry dictionary, `pack://application:,,,/WpfFoundation;component/Themes/WpfFoundation.xaml` (`WpfFoundationResources.EntryDictionaryUri`).
- When adding, removing, or renaming a resource key, update the committed key list that the resource key test checks.

## Fluent styling

- Build on WPF's native Fluent theme (`ThemeMode`: System, Light, Dark). Use its brushes, text styles, and corner radius resources and its 4 px spacing grid; do not add new colors, font sizes, or radii.
- Follow the Design Guidance section of Microsoft's WPF Gallery app for colors, typography, spacing, corner radius, and icons, and Microsoft's Windows app design guidance for dialog wording.
- Use stock WPF controls where sufficient, and keep their keyboard behavior and accessibility.
- Check every visual change in Light, Dark, and high contrast.

## Icons

- Segoe Fluent Icons glyphs (`SymbolThemeFontFamily`) stay fine where the font has the icon.
- For icons the font lacks, or combined icons such as "add files", add a `WfIcon*` geometry with `tools/IconImport` and show it with `PathIcon` or `WfIconContentTemplate`. Never give an icon its own color; it takes the foreground around it or a Fluent brush.

## Dialogs

- Host every dialog in the shared `DialogWindow`; never use the native `MessageBox`, which ignores the dark theme.
- Dialog ViewModels never reference the window. They finish through the result contract that `IDialogService` awaits.
- Button labels name the action (for example "Delete" and "Keep") rather than Yes/No. Destructive confirmations default to the safe choice.
- Keep native open, save, and folder pickers behind `IFileDialogService`.

## Gallery

- Use fictional sample data only.
- Show what WpfFoundation adds, with working interaction and copyable usage XAML. Point to Microsoft's WPF Gallery app for stock Fluent controls and design guidance instead of repeating them.

## Provenance and licensing

- Copy or adapt only code written by the repository owner. Never copy code from DoViFixer's media workflows, which derive from GPL-3.0 dovi_convert.
- The exception is vector icons: take them only from Fluent UI System Icons (MIT) through `tools/IconImport`, never by tracing Segoe Fluent Icons, whose outlines are Microsoft's font and can't be redistributed.
- Record each copied or adapted file with its source repository and path in the provenance notes.

## Packaging and releases

- Release from CI only. Work in progress ships as prerelease versions (`1.0.0-preview.N`), because NuGet.org packages cannot be deleted, only unlisted.
- Every change bumps the version: raise the prerelease number of `<Version>` in `Directory.Build.props` (for example `1.0.0-preview.7` to `1.0.0-preview.8`) in the same pull request.
- CI tags and publishes every push to main: it tags the pushed commit `v<Version>`, pushes the package to NuGet.org and creates the GitHub release. It fails when that tag already exists, so a change merged without a version bump is caught. Never tag a pull request branch by hand, because a tag publishes to NuGet.org.
- Keep SourceLink and the symbols package enabled.
- `WPF0001` is suppressed once in `Directory.Build.props`; do not add per-project suppressions.

## Testing and refactoring checks

- Run WPF integration tests on an STA dispatcher.
- Test navigation and dialog outcomes, including rejection and cancellation, not only success.
- Check for application-specific code, wording, or dependencies leaking into the library.
- Check for subscriptions and retained views that outlive their host.
