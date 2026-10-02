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
