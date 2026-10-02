# WpfFoundation — reusable WPF library and Gallery

## 1. Solution and boundaries

Create a separate repository at `C:\Users\gbolo\OneDrive\source\repos\WpfFoundation`, published publicly as `gbolotin/WpfFoundation`.

The solution will contain:

- **WpfFoundation** — reusable WPF infrastructure and controls.
- **WpfFoundation.Gallery** — interactive examples and usage reference.
- **WpfFoundation.Tests** — automated behavior and WPF integration tests.

Use **.NET 10**, **MIT**, **CommunityToolkit.Mvvm**, and native Windows Fluent styling. Existing applications remain unchanged; their migrations are future work.

Keep the library independent of application projects and DI containers. Use constructor injection and explicit composition in Gallery startup. Use Toolkit commands, observable objects, and validation directly rather than maintaining equivalent helpers.

## 2. Findings and extraction decisions

| Project inspected | Worth reusing or adapting | Keep application-specific |
|---|---|---|
| **DoViFixer** | Retained navigation pattern, sorting, column sizing, file-drop behavior, converters, Fluent styles, settings rows, toggle switches, status presentation | Media workflows, settings persistence, dependency checks, operation guards |
| **UPSWarden**, including SnmpAgent.App | Closable document-tab behavior, custom-form dialog requirements, status and validation presentation | Prism regions, Unity factories, device ViewModels, polling, SNMP, persistence |
| **AudioAwake** | `MarqueeTextBlock` | Tray integration, screensaver lifecycle, media-session handling |
| **AlbumFixer** | File/folder selection, confirmation ownership and defaults, drag/drop patterns | Album-processing rules and its separate dark theme |
| **PatternBuilder** | Typed DataTemplate and editable-form examples | Regex processing and segment models |
| **WpfApp1** | Fluent setup reference | No additional reusable implementation |
| **UpsSnmpSimulator** | Form and validation examples | Simulator state and SNMP operations |

Other inspected repositories provide no additional WPF extraction candidates.

DoViFixer's **current** reference is a Fluent `ListBox` sidebar and retained page presentations. Its navigation coordination currently resides in `ShellViewModel`; extract the reusable behavior without transferring its application dependencies.

Document these decisions and source provenance in the new repository.

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
- Support asynchronous initialization and application-supplied navigation guards. Initialize successfully once; allow retry after failure. Rejected, failed, or cancelled transitions preserve the current selection.
- Retain each visited page's view and ViewModel until its navigation host is disposed.
- Add `DocumentItem`, `DocumentWorkspace`, and document-tab presentation. Opening an existing document key activates its existing tab.
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

## 5. Validation and delivery

- Test navigation success, rejection, cancellation, initialization retry, retained state, and overlapping requests.
- Test document identity, close rejection, neighbor selection, view release, and cleanup callbacks.
- Test sorting direction, custom comparers, unchanged source order, independent views, source replacement, and subscription lifetimes.
- Test drop acceptance, converter edge cases, dialog results, validation, and marquee lifecycle.
- Run WPF integration checks on an STA dispatcher. Visually check all Gallery pages in Light/Dark, keyboard navigation, focus, high contrast, and representative DPI scales.
- Verify resource loading from a separately packed library, not only through Gallery's project reference.
- Add Windows CI for Release build, tests, NuGet packing, and a downloadable framework-dependent Gallery ZIP.
- Include README, examples, extraction notes, MIT license, and applicable third-party notices. Verify copied code is eligible for MIT publication.
- Implement on `codex/initial-foundation`; publish the validated initial version with `main` as the public repository's default branch.

The first release provides public source and build/package artifacts. NuGet.org publishing, installers, additional themes, and existing-application migrations are deferred. PatternBuilder will require a .NET upgrade before adoption.
