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

### Settings frame over the window

`ContentOverlay` shows a card, such as settings, over a window's content on a smoke layer. Its close button, a click outside the card and Esc run `CloseCommand`, which can keep it open while it cannot execute. `HeaderBackground` paints the title row, for example to match a navigation pane at the start of the content, and `HeaderTemplate` styles the title. `WfNavigationButtonStyle` styles a sidebar command, such as Settings at the bottom, like the sidebar's items. Both build on `WfSubtleButtonStyle`, a button with no fill until it is hovered or pressed. The Gallery's Navigation page has copyable XAML for both.

### Menu in the title bar

`TitleBar` puts the window's icon, the window's title and its content (such as a `Menu`) in the title bar. `RightHeader` adds content at the right end of the row, before the caption buttons, such as a settings button; a `WfSubtleButtonStyle` icon button with `Padding="10,4"` keeps the caption height. Place it at the top of the window's content and set the window's `Icon`. While the window shows the Fluent backdrop (`ThemeMode` on Windows 11, without high contrast), it replaces the system title bar through `WindowChrome`. Windows still draws the minimize, maximize and close buttons, with Snap Layouts. Dragging the row moves the window, and a double-click maximizes it. A click on the icon opens the system menu. Otherwise, or when `ExtendsIntoTitleBar` is `false`, the window keeps its system title bar, and the row shows only the content, below it. `IsExtended` says which. The Gallery's window uses it with `ExtendsIntoTitleBar="False"`, and its Navigation page has copyable XAML.

### Validation warning adorners

Apply `WfValidationWarningTemplate` to a `FrameworkElement` with WPF validation errors:

```xml
<TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged, ValidatesOnNotifyDataErrors=True}"
         Validation.ErrorTemplate="{StaticResource WfValidationWarningTemplate}"
         Margin="0,0,24,0" />
```

The native validation adorner displays one warning glyph immediately outside the control's right edge.
Its tooltip and accessible name contain every validation message, and update as errors change.
Reserve 24 pixels on the right; adorners do not participate in layout.
The host must provide an `AdornerLayer`, normally supplied by a WPF window or an `AdornerDecorator`.
The template preserves the input's Fluent template, focus behavior, and clear button.
WPF manages creation and removal when validation, visibility, or the host changes.

The template supports binding conversion errors, validation rules, and `INotifyDataErrorInfo`.
It does not validate values or prevent saving. Those remain the application's responsibility.
Use `WfValidationErrorsToTextConverter` with a `MultiBinding` to the errors collection and its `Count`
when displaying the same messages elsewhere. The count binding refreshes the text when errors change.
The Gallery's **Dialogs → Edit note** example includes this template and copyable XAML.

### Native Fluent validation outlines

Use `WfValidationOutlineTextBoxStyle` or `WfValidationOutlineComboBoxStyle` for a rounded validation outline:

```xml
<TextBox Text="{Binding Value, UpdateSourceTrigger=PropertyChanged, ValidatesOnNotifyDataErrors=True}"
         Style="{StaticResource WfValidationOutlineTextBoxStyle}" />
```

These styles reuse the native Fluent theme's `DefaultTextBoxInvalidationStyle` resource.
Microsoft's template draws the outline with the theme's critical brush and control corner radius.
The control template, focus underline, and clear button remain native. No extra right margin is needed.
WpfFoundation adds all validation messages to the invalid input's tooltip and UI Automation item status.
Correction removes the outline and restores the style's normal tooltip.
A locally assigned tooltip takes precedence over a style trigger, following WPF's standard property precedence.
The warning-marker template remains available independently. The Gallery's Edit note dialog demonstrates both options.

This uses the installed .NET 10 Fluent resource; no Microsoft control-template source is copied into the library.
See [Microsoft's Fluent TextBox source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Styles/TextBox.xaml).

### TextBox placeholders, suffixes, and search boxes

`TextBoxAssist` uses adorners and the native Fluent template to add hints, units, and trailing icons to stock TextBox controls:

```xml
<!-- xmlns:behaviors="clr-namespace:WpfFoundation.Behaviors;assembly=WpfFoundation" -->
<TextBox Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}"
         behaviors:TextBoxAssist.Placeholder="Filter notes"
         AutomationProperties.Name="Filter notes"
         Style="{StaticResource WfSearchTextBoxStyle}" />

<TextBox Text="{Binding Distance, UpdateSourceTrigger=PropertyChanged}"
         behaviors:TextBoxAssist.Placeholder="6.4"
         behaviors:TextBoxAssist.Suffix="km"
         AutomationProperties.Name="Distance"
         AutomationProperties.HelpText="Distance in kilometers." />
```

`Placeholder` appears only when `Text` is empty, including while focused. It is a hint, not a default value.
`Suffix` follows the value with a 4-pixel gap. With an empty value, it follows the placeholder when one is present.
`Icon` (`&#xE721;` in `WfSearchTextBoxStyle`) shows a Segoe Fluent Icons glyph on the right side of the TextBox, with the native clear (`DeleteButton`) button to its left whenever `Text` is non-empty, even while unfocused.
Neither decoration changes `Text`, `SelectedText`, source bindings, validation, or clipboard values.
The suffix reserves space in the native content host. TextBox.Padding and its binding source stay unchanged, including two-way bindings.
Long values scroll within the remaining space; decorations are clipped to the native content host and avoid the clear button and icon.
The native control template, focus underline, and validation adorner remain unchanged.

All properties support bindings and can be changed or removed at runtime; null or empty removes that decoration.
Load/unload handlers manage the adorner and restore the original padding and clear button state. A missing AdornerLayer is harmless;
the behavior retries when layout becomes available. Use a normal WPF window or an AdornerDecorator host.
Fluent text brushes provide theme and high-contrast colors. The text uses the input's font and display DPI.
Keep a visible label or `AutomationProperties.Name`, and include units in `AutomationProperties.HelpText` for assistive technology.
Drawn decoration text is not a replacement for an accessible label.

The Gallery's Behaviors page has editable examples and copyable XAML.
The implementation follows the attached-property and adorner approach in
[Microsoft's placeholder guide](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-add-a-watermark-to-a-textbox).

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

### Remembering column layouts

`GridViewLayout` captures a `ListView` / `GridView` column order and explicit widths through a two-way `State` binding.
Assign each column a unique, stable `GridViewLayout.Id`; headers can then change without losing the saved layout.
The application serializes the state through its own settings service. The library does not read or write settings files.

```xml
<ListView wf:GridViewLayout.IsEnabled="True"
          wf:GridViewLayout.State="{Binding ColumnLayout, Mode=TwoWay}"
          wf:GridViewSizing.LastColumnMinimumWidth="120">
    <ListView.View>
        <GridView>
            <GridViewColumn Header="Name"
                            Width="200"
                            wf:GridViewLayout.Id="Name" />
            <GridViewColumn Header="Actions"
                            wf:GridViewLayout.Id="Actions"
                            wf:GridViewSizing.IsFillColumn="True" />
        </GridView>
    </ListView.View>
</ListView>
```

Bind `State` to `IReadOnlyList<GridViewColumnLayout>?`. Each entry has an ID and nullable width; null preserves automatic sizing.
Explicitly marked fill-column widths are not saved, and that column continues to fill the list after reordering.
Unknown or duplicate saved IDs and invalid widths are ignored. New columns remain in their default relative order after known columns.
Set the bound state to null to restore the original widths and order. Event subscriptions are released while the list is unloaded.
`ColumnSort<T>.Restore(key, direction)` restores an application's saved sort selection; `Restore(null)` clears sorting.
Save sort keys independently from translated header text, and debounce storage writes in the consuming application.

### Item hover buttons

`WfItemHoverButtonStyle` shows an icon-only button in a `ListBox` or `ListView` item while the pointer is over that item, like the Delete button on a file in a list. It is hidden, not collapsed, otherwise, so every item's buttons keep fixed slots. Bind `HoverAction.IsAvailable` to hide the button on items where the action does not apply; a disabled button stays hidden too. Hidden buttons take no keyboard focus, so also offer the actions in the item's context menu. The button builds on `WfTransparentButtonStyle`, which fills subtly while hovered and pressed.

```xml
<Button Content="&#xE74D;"
        ToolTip="Delete"
        AutomationProperties.Name="Delete"
        wf:HoverAction.IsAvailable="{Binding CanDelete}"
        Style="{StaticResource WfItemHoverButtonStyle}" />
```
