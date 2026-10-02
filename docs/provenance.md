# Provenance notes

WpfFoundation copies or adapts only code written by the repository owner. Each copied or adapted file is listed here with its source repository and path.

DoViFixer is GPL-3.0 because its media workflows adapt [dovi_convert](https://github.com/cryptochrome/dovi_convert). The DoViFixer files listed here are WPF presentation code with no counterpart in dovi_convert, so they are published here under MIT. Nothing from DoViFixer's media workflows is copied.

| WpfFoundation file | Source repository | Source path | Notes |
|---|---|---|---|
| `global.json` | gbolotin/DoViFixer | `global.json` | Copied unchanged |
| `.editorconfig` | gbolotin/DoViFixer | `.editorconfig` | Copied unchanged |
| `Directory.Build.props` | gbolotin/DoViFixer | `Directory.Build.props` | Adds the repository-wide `WPF0001` suppression and package metadata |
| `Directory.Packages.props` | gbolotin/DoViFixer | `Directory.Packages.props` | Keeps only the test packages |
| `docs/agents/common-rules.md` | gbolotin/DoViFixer | `docs/agents/common-rules.md` | Only the link to the repository-specific rules changed |
| `src/WpfFoundation/Behaviors/ColumnSort.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Presentation/Common/ColumnSort.cs` | Uses CommunityToolkit.Mvvm's `ObservableObject` |
| `src/WpfFoundation/Behaviors/GridViewSort.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Presentation/Common/GridViewSort.cs` | Each list sorts its own view, and a replaced source keeps the sort |
| `src/WpfFoundation/Behaviors/GridViewSizing.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Presentation/Common/GridViewSizing.cs` | Copied with comments reworded |
| `src/WpfFoundation/Behaviors/FileDrop.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Presentation/Common/FileDrop.cs` | Copied, with a type summary added |
| `src/WpfFoundation/Converters/*.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Presentation/Common/*Converter.cs` | The inverse-boolean and reference-equality visibility converters, copied with type summaries added |
| `src/WpfFoundation/Controls/StatusItem.cs` | gbolotin/DoViFixer | `src/DoViFixer.App/Navigation/StatusItem.cs` | Copied, with a type summary added |
| `src/WpfFoundation/Themes/WpfFoundation.xaml` | gbolotin/DoViFixer | `src/DoViFixer.App/Resources/Common.xaml`, and the status bar in `src/DoViFixer.App/Views/Shell.xaml` | Keys prefixed with `Wf`, spacing on the 4 px grid, card corner radius from the Fluent theme |
| `src/WpfFoundation/Controls/MarqueeTextBlock.cs` | gbolotin/AudioAwake | `AudioAwake.app/MarqueeTextBlock.cs` | Adds gap, speed and animation settings, trims when still, and stops when hidden or when Windows animations are off |
