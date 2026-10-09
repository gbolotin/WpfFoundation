<#
.SYNOPSIS
Copies the shared common development rules into each repository.

.DESCRIPTION
The master copy of the common rules is the AGENTS.md in the OneDrive folder, above all
repositories. CI and cloud sessions only see a repository's own files, so each repository
keeps a copy at docs/agents/common-rules.md, which its AGENTS.md links to.

Edit the master copy, run this script, then commit the updated copy in each repository.
#>
param(
    [string] $Source = (Join-Path $env:OneDrive 'AGENTS.md'),
    [string] $RepositoriesRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string[]] $Repositories = @('WpfFoundation', 'DoViFixer', 'UPSWarden')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Source)) {
    throw "Common rules not found at '$Source'."
}

$header = '<!-- Copied from the shared AGENTS.md by WpfFoundation tools/SyncCommonRules. Edit the shared file and run the script instead of editing this copy. -->'
$rules = (Get-Content $Source -Raw).TrimEnd()
$content = "$header`r`n`r`n# Common development rules`r`n`r`n$rules`r`n"

foreach ($repository in $Repositories) {
    $repositoryPath = Join-Path $RepositoriesRoot $repository
    if (-not (Test-Path (Join-Path $repositoryPath '.git'))) {
        Write-Warning "Skipping '$repository': no git repository at '$repositoryPath'."
        continue
    }

    $target = Join-Path $repositoryPath 'docs\agents\common-rules.md'
    New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null

    if ((Test-Path $target) -and ((Get-Content $target -Raw) -eq $content)) {
        Write-Host "$repository is up to date."
        continue
    }

    [System.IO.File]::WriteAllText($target, $content, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Updated $target"
}
