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
    $OutputFileName = ConvertFrom-Utf8Base64 "5Lmm5bG/5qKm6KGMVjEuMC3liY3nq6/mupDku6PnoIHoioLpgIkzMOmhtS5kb2N4"
}

$softwareName = ConvertFrom-Utf8Base64 "5Lmm5bG/5qKm6KGM"
$softwareVersion = "V1.0"
$eastAsiaFont = ConvertFrom-Utf8Base64 "5a6L5L2T"
$headerSuffix = ConvertFrom-Utf8Base64 "44CA5YmN56uv5rqQ56iL5bqP44CASG9tZVBhZ2VVSS5jcw=="
$footerPrefix = ConvertFrom-Utf8Base64 "56ysIA=="
$footerSuffix = ConvertFrom-Utf8Base64 "IOmhte+8jOWFsSAzMCDpobU="
$titleSuffix = ConvertFrom-Utf8Base64 "IOWJjeerr+a6kOS7o+eggeiKgumAie+8iDMw6aG177yJ"
$documentSubject = ConvertFrom-Utf8Base64 "6K6h566X5py66L2v5Lu26JGX5L2c5p2D55m76K6w5rqQ56iL5bqP5p2Q5paZ"
$documentCreator = ConvertFrom-Utf8Base64 "5Lmm5bG/5qKm6KGM6aG555uu57uE"
$documentKeywords = ConvertFrom-Utf8Base64 "6L2v5Lu26JGX5L2c5p2DO+WJjeerr+a6kOS7o+eggTtVbml0eTtDIw=="
$descriptionFormat = ConvertFrom-Utf8Base64 "5rqQ5paH5Lu277yaezB977yb6Z2e56m65rqQ56CB6KGM5bqP5Y+377yaMS17MX3vvJvmr4/pobU1MOihjO+8jOWFsTMw6aG144CC"
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

function Escape-Xml([string]$value)
{
    return [System.Security.SecurityElement]::Escape($value)
}

function Get-CharacterDisplayWidth([char]$character)
{
    if ([int]$character -gt 255)
    {
        return 2
    }

    return 1
}

function Split-SourceLine([string]$line, [int]$maximumWidth)
{
    $segments = New-Object System.Collections.Generic.List[string]
    $builder = New-Object System.Text.StringBuilder
    $currentWidth = 0

    foreach ($character in $line.ToCharArray())
    {
        $characterWidth = Get-CharacterDisplayWidth $character
        if ($builder.Length -gt 0 -and $currentWidth + $characterWidth -gt $maximumWidth)
        {
            $segments.Add($builder.ToString())
            [void]$builder.Clear()
            $currentWidth = 0
        }

        [void]$builder.Append($character)
        $currentWidth += $characterWidth
    }

    if ($builder.Length -gt 0)
    {
        $segments.Add($builder.ToString())
    }

    if ($segments.Count -eq 0)
    {
        $segments.Add([string]::Empty)
    }

    return $segments
}

function Write-Utf8NoBom([string]$path, [string]$content)
{
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}

$displayLines = New-Object System.Collections.Generic.List[string]
$sourceLines = Get-Content -LiteralPath $sourcePath -Encoding UTF8
$nonBlankSourceLines = @($sourceLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

if ($nonBlankSourceLines.Count -lt $requiredSourceLines)
{
    throw "Expected at least $requiredSourceLines non-blank source lines, but found $($nonBlankSourceLines.Count)."
}

for ($index = 0; $index -lt $requiredSourceLines; $index++)
{
    $expandedLine = $nonBlankSourceLines[$index].Replace("`t", "    ")
    $lineNumber = ($index + 1).ToString().PadLeft(5)
    $displayLines.Add($lineNumber + "  " + $expandedLine)
}

$lastSourceLine = $requiredSourceLines
if ($displayLines.Count -ne $requiredSourceLines)
{
    throw "Expected $requiredSourceLines source lines, but generated $($displayLines.Count)."
}

$bodyBuilder = New-Object System.Text.StringBuilder
for ($index = 0; $index -lt $displayLines.Count; $index++)
{
    $escapedLine = Escape-Xml $displayLines[$index]
    [void]$bodyBuilder.Append("<w:p><w:pPr><w:keepLines/><w:noWrap/><w:spacing w:before=`"0`" w:after=`"0`" w:line=`"200`" w:lineRule=`"exact`"/></w:pPr><w:r><w:rPr><w:rFonts w:ascii=`"Consolas`" w:hAnsi=`"Consolas`" w:eastAsia=`"$eastAsiaFont`"/><w:sz w:val=`"16`"/><w:szCs w:val=`"16`"/></w:rPr><w:t xml:space=`"preserve`">")
    [void]$bodyBuilder.Append($escapedLine)
    [void]$bodyBuilder.Append('</w:t>')

    if (($index + 1) % $linesPerPage -eq 0 -and $index + 1 -lt $displayLines.Count)
    {
        [void]$bodyBuilder.Append('<w:br w:type="page"/>')
    }

    [void]$bodyBuilder.Append('</w:r></w:p>')
}

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <w:body>
    $($bodyBuilder.ToString())
    <w:sectPr>
      <w:headerReference w:type="default" r:id="rId1"/>
      <w:footerReference w:type="default" r:id="rId2"/>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1021" w:right="851" w:bottom="851" w:left="851" w:header="454" w:footer="454" w:gutter="0"/>
      <w:pgNumType w:start="1"/>
      <w:cols w:space="425"/>
      <w:docGrid w:linePitch="200"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

$headerTitle = Escape-Xml "$softwareName$softwareVersion$headerSuffix"
$headerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="0" w:after="0"/></w:pPr>
    <w:r><w:rPr><w:rFonts w:ascii="$eastAsiaFont" w:hAnsi="$eastAsiaFont" w:eastAsia="$eastAsiaFont"/><w:b/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t>$headerTitle</w:t></w:r>
  </w:p>
</w:hdr>
"@

$footerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="0" w:after="0"/></w:pPr>
    <w:r><w:rPr><w:rFonts w:ascii="$eastAsiaFont" w:hAnsi="$eastAsiaFont" w:eastAsia="$eastAsiaFont"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t>$footerPrefix</w:t></w:r>
    <w:r><w:fldChar w:fldCharType="begin"/></w:r>
    <w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
    <w:r><w:fldChar w:fldCharType="separate"/></w:r>
    <w:r><w:t>1</w:t></w:r>
    <w:r><w:fldChar w:fldCharType="end"/></w:r>
    <w:r><w:rPr><w:rFonts w:ascii="$eastAsiaFont" w:hAnsi="$eastAsiaFont" w:eastAsia="$eastAsiaFont"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t>$footerSuffix</w:t></w:r>
  </w:p>
</w:ftr>
"@

$stylesXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:docDefaults>
    <w:rPrDefault><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:eastAsia="$eastAsiaFont"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr></w:rPrDefault>
    <w:pPrDefault><w:pPr><w:spacing w:before="0" w:after="0" w:line="200" w:lineRule="exact"/></w:pPr></w:pPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/><w:qFormat/></w:style>
</w:styles>
"@

$settingsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:updateFields w:val="true"/>
  <w:defaultTabStop w:val="420"/>
  <w:compat><w:compatSetting w:name="compatibilityMode" w:uri="http://schemas.microsoft.com/office/word" w:val="15"/></w:compat>
</w:settings>
"@

$contentTypesXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/settings.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
  <Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>
  <Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
"@

$packageRelationshipsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
"@

$documentRelationshipsXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
  <Relationship Id="rId4" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings" Target="settings.xml"/>
</Relationships>
"@

$timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$documentTitle = Escape-Xml "$softwareName$softwareVersion$titleSuffix"
$documentDescription = Escape-Xml ([string]::Format($descriptionFormat, $SourceRelativePath, $lastSourceLine))
$coreXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>$documentTitle</dc:title>
  <dc:subject>$documentSubject</dc:subject>
  <dc:creator>$documentCreator</dc:creator>
  <cp:keywords>$documentKeywords</cp:keywords>
  <dc:description>$documentDescription</dc:description>
  <dcterms:created xsi:type="dcterms:W3CDTF">$timestamp</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">$timestamp</dcterms:modified>
</cp:coreProperties>
"@

$appXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft Office Word</Application>
  <AppVersion>16.0000</AppVersion>
  <Pages>30</Pages>
  <Lines>1500</Lines>
  <Words>1500</Words>
  <Characters>$(($displayLines -join "`n").Length)</Characters>
</Properties>
"@

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("weijun-source-docx-" + [Guid]::NewGuid().ToString("N"))
$resolvedTempRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
$systemTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
if (-not $resolvedTempRoot.StartsWith($systemTempRoot, [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Refusing to use a temporary path outside the system temp directory: $resolvedTempRoot"
}

try
{
    New-Item -ItemType Directory -Path $temporaryRoot, (Join-Path $temporaryRoot "_rels"), (Join-Path $temporaryRoot "word"), (Join-Path $temporaryRoot "word/_rels"), (Join-Path $temporaryRoot "docProps") -Force | Out-Null

    Write-Utf8NoBom (Join-Path $temporaryRoot "[Content_Types].xml") $contentTypesXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "_rels/.rels") $packageRelationshipsXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/document.xml") $documentXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/header1.xml") $headerXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/footer1.xml") $footerXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/styles.xml") $stylesXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/settings.xml") $settingsXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "word/_rels/document.xml.rels") $documentRelationshipsXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "docProps/core.xml") $coreXml
    Write-Utf8NoBom (Join-Path $temporaryRoot "docProps/app.xml") $appXml

    if (Test-Path -LiteralPath $outputPath)
    {
        Remove-Item -LiteralPath $outputPath -Force
    }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($outputPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try
    {
        foreach ($file in Get-ChildItem -LiteralPath $temporaryRoot -Recurse -File)
        {
            $relativePath = $file.FullName.Substring($resolvedTempRoot.Length).TrimStart([char[]]@('\', '/'))
            $entryName = $relativePath.Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally
    {
        $archive.Dispose()
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
Write-Output "SOURCE_RANGE=1-$lastSourceLine"
Write-Output "PAGES=$pageCount"
Write-Output "LINES_PER_PAGE=$linesPerPage"
Write-Output "FORMATTED_SOURCE_LINES=$($displayLines.Count)"
