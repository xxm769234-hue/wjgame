param(
    [string]$SourceRelativePath = "Assets/_Imported/weijun-game-project/Assets/Scripts/UI/HomePageUI.cs",
    [string]$OutputFileName = ""
)

$ErrorActionPreference = "Stop"

function ConvertFrom-Utf8Base64([string]$value)
{
    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($value))
}

if ([string]::IsNullOrWhiteSpace($OutputFileName))
{
    $OutputFileName = ConvertFrom-Utf8Base64 "5Lmm5bG/5qKm6KGMVjEuMC3liY3nq6/mupDku6PnoIHoioLpgIkzMOmhtS5wZGY="
}

$softwareName = ConvertFrom-Utf8Base64 "5Lmm5bG/5qKm6KGM"
$softwareVersion = "V1.0"
$eastAsiaFont = ConvertFrom-Utf8Base64 "5a6L5L2T"
$headerSuffix = ConvertFrom-Utf8Base64 "44CA5YmN56uv5rqQ56iL5bqP44CASG9tZVBhZ2VVSS5jcw=="
$sourceFileLabel = ConvertFrom-Utf8Base64 "5rqQ5paH5Lu277ya"
$pageCount = 30
$linesPerPage = 50
$requiredSourceLines = $pageCount * $linesPerPage
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$sourcePath = Join-Path $projectRoot $SourceRelativePath
$outputPath = Join-Path $PSScriptRoot $OutputFileName

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf))
{
    throw "Source file was not found: $sourcePath"
}

function Escape-Html([string]$value)
{
    return [System.Security.SecurityElement]::Escape($value)
}

function Write-Utf8NoBom([string]$path, [string]$content)
{
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}

$sourceLines = Get-Content -LiteralPath $sourcePath -Encoding UTF8
$nonBlankSourceLines = @($sourceLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($nonBlankSourceLines.Count -lt $requiredSourceLines)
{
    throw "Expected at least $requiredSourceLines non-blank source lines, but found $($nonBlankSourceLines.Count)."
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("weijun-source-pdf-" + [Guid]::NewGuid().ToString("N"))
$resolvedTempRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
$systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $resolvedTempRoot.StartsWith($systemTempRoot, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to use a temporary path outside the system temp directory: $resolvedTempRoot"
}

$htmlBuilder = New-Object System.Text.StringBuilder
[void]$htmlBuilder.AppendLine('<!doctype html>')
[void]$htmlBuilder.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8"><title>' + (Escape-Html "$softwareName $softwareVersion $headerSuffix") + '</title>')
[void]$htmlBuilder.AppendLine('<style>')
[void]$htmlBuilder.AppendLine('@page { size: A4 portrait; margin: 0; }')
[void]$htmlBuilder.AppendLine('html, body { margin: 0; padding: 0; background: #fff; }')
[void]$htmlBuilder.AppendLine('.page { position: relative; box-sizing: border-box; width: 210mm; height: 297mm; padding: 15mm 12mm 13mm; page-break-after: always; overflow: hidden; }')
[void]$htmlBuilder.AppendLine('.page:last-child { page-break-after: auto; }')
[void]$htmlBuilder.AppendLine('.header { position: absolute; top: 5mm; left: 12mm; right: 12mm; height: 7mm; text-align: center; font-family: "' + $eastAsiaFont + '", "Noto Sans SC", sans-serif; font-size: 9pt; font-weight: 700; color: #111; white-space: nowrap; }')
[void]$htmlBuilder.AppendLine('.code { margin: 0; width: 178%; height: 266mm; overflow: hidden; white-space: pre; transform: scaleX(0.56); transform-origin: top left; font-family: Consolas, "' + $eastAsiaFont + '", "Noto Sans SC", monospace; font-size: 7.2pt; line-height: 5.2mm; color: #111; tab-size: 4; }')
[void]$htmlBuilder.AppendLine('.footer { position: absolute; bottom: 5mm; left: 12mm; right: 12mm; height: 6mm; text-align: center; font-family: "' + $eastAsiaFont + '", "Noto Sans SC", sans-serif; font-size: 8.5pt; color: #333; }')
[void]$htmlBuilder.AppendLine('</style></head><body>')

for ($pageIndex = 0; $pageIndex -lt $pageCount; $pageIndex++)
{
    [void]$htmlBuilder.Append('<section class="page"><div class="header">')
    [void]$htmlBuilder.Append((Escape-Html "$softwareName $softwareVersion $headerSuffix"))
    [void]$htmlBuilder.Append('</div><pre class="code">')

    for ($lineIndex = 0; $lineIndex -lt $linesPerPage; $lineIndex++)
    {
        $sourceIndex = $pageIndex * $linesPerPage + $lineIndex
        $line = $nonBlankSourceLines[$sourceIndex].Replace("`t", "    ")
        $lineNumber = ($sourceIndex + 1).ToString().PadLeft(5)
        [void]$htmlBuilder.Append((Escape-Html ($lineNumber + "  " + $line)))
        if ($lineIndex -lt $linesPerPage - 1)
        {
            [void]$htmlBuilder.Append("`n")
        }
    }

    [void]$htmlBuilder.Append('</pre><div class="footer">PAGE ')
    [void]$htmlBuilder.Append(($pageIndex + 1).ToString())
    [void]$htmlBuilder.Append(' / 30')
    [void]$htmlBuilder.Append('</div></section>')
}

[void]$htmlBuilder.AppendLine('</body></html>')

try
{
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    $htmlPath = Join-Path $temporaryRoot "source.html"
    Write-Utf8NoBom $htmlPath $htmlBuilder.ToString()

    if (Test-Path -LiteralPath $outputPath)
    {
        Remove-Item -LiteralPath $outputPath -Force
    }

    $edgeCandidates = @(
        "C:\Program Files (x86)\Microsoft\Edge\Application\152.0.4191.53\msedge.exe",
        "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    )
    $edgePath = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($edgePath))
    {
        throw "Microsoft Edge was not found; cannot render the PDF locally."
    }

    $profilePath = Join-Path $temporaryRoot "edge-profile"
    $htmlUri = ([System.Uri]$htmlPath).AbsoluteUri
    & $edgePath --headless=new --disable-gpu --disable-gpu-compositing --disable-software-rasterizer --disable-extensions --disable-background-networking --disable-component-update --no-first-run --disable-sync --disable-default-apps --no-sandbox --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=1000 --user-data-dir="$profilePath" --print-to-pdf="$outputPath" "$htmlUri" | Out-Null
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf))
    {
        throw "Edge did not create the PDF: $outputPath"
    }
}
finally
{
    if (Test-Path -LiteralPath $resolvedTempRoot)
    {
        Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force
    }
}

Write-Output "OUTPUT=$outputPath"
Write-Output "SOURCE=$sourcePath"
Write-Output "SOURCE_NONBLANK_RANGE=1-$requiredSourceLines"
Write-Output "PAGES=$pageCount"
Write-Output "LINES_PER_PAGE=$linesPerPage"
Write-Output "FORMATTED_SOURCE_LINES=$requiredSourceLines"
