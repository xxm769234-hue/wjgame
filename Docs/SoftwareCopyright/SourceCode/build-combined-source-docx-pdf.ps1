param(
    [string]$BackendDocxPath = "C:\Users\KMYF\Desktop\weijun-game-project-软著-源程序前30页后30页.docx",
    [string]$FrontendDocxPath = "",
    [string]$OutputBaseName = "书屿梦行V0.1-前后端源代码合并60页"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($FrontendDocxPath))
{
    $FrontendDocxPath = Join-Path $PSScriptRoot "书屿梦行V1.0-前端源代码节选30页.docx"
}

$softwareName = "书屿梦行"
$softwareVersion = "V0.1"
$linesPerPage = 50
$linesPerSection = 1500
$sourceDocumentPaths = @($BackendDocxPath, $FrontendDocxPath)
$sourceLabels = @("后端代码", "前端代码")
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$outputDocxPath = Join-Path $PSScriptRoot ($OutputBaseName + ".docx")
$outputPdfPath = Join-Path $PSScriptRoot ($OutputBaseName + ".pdf")

foreach ($sourceDocumentPath in $sourceDocumentPaths)
{
    if (-not (Test-Path -LiteralPath $sourceDocumentPath -PathType Leaf))
    {
        throw "Source DOCX was not found: $sourceDocumentPath"
    }
}

function Write-Utf8NoBom([string]$path, [string]$content)
{
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $content, $encoding)
}

function Escape-Xml([string]$value)
{
    return [System.Security.SecurityElement]::Escape($value)
}

function Escape-Html([string]$value)
{
    return [System.Security.SecurityElement]::Escape($value)
}

function Read-NumberedDocxLines([string]$path)
{
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $path).Path)
    try
    {
        $entry = $archive.GetEntry("word/document.xml")
        if ($null -eq $entry)
        {
            throw "The DOCX has no word/document.xml part: $path"
        }

        $reader = [System.IO.StreamReader]::new($entry.Open())
        try
        {
            $documentXml = [xml]$reader.ReadToEnd()
        }
        finally
        {
            $reader.Dispose()
        }
    }
    finally
    {
        $archive.Dispose()
    }

    $namespaceManager = [System.Xml.XmlNamespaceManager]::new($documentXml.NameTable)
    $namespaceManager.AddNamespace("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main")
    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($paragraph in $documentXml.SelectNodes("//w:body/w:p", $namespaceManager))
    {
        $text = (($paragraph.SelectNodes(".//w:t", $namespaceManager) | ForEach-Object { $_.InnerText }) -join "")
        if (-not [string]::IsNullOrWhiteSpace($text))
        {
            $lines.Add($text)
        }
    }

    if ($lines.Count -eq 0)
    {
        throw "The DOCX contains no source lines: $path"
    }

    $sourceLines = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines)
    {
        $normalizedLine = ($line -replace '^\s*\d{1,6}\s{2}', '').Replace("`t", "    ")
        if (-not [string]::IsNullOrWhiteSpace($normalizedLine))
        {
            $sourceLines.Add($normalizedLine)
        }
    }

    return $sourceLines
}

$backendSourceLines = Read-NumberedDocxLines $BackendDocxPath
$frontendSourceLines = Read-NumberedDocxLines $FrontendDocxPath
if ($backendSourceLines.Count -lt $linesPerSection)
{
    throw "The backend document contains only $($backendSourceLines.Count) source lines; at least $linesPerSection are required."
}
if ($frontendSourceLines.Count -lt $linesPerSection)
{
    throw "The frontend document contains only $($frontendSourceLines.Count) source lines; at least $linesPerSection are required."
}

$backendLines = New-Object System.Collections.Generic.List[string]
$frontendLines = New-Object System.Collections.Generic.List[string]
for ($index = 0; $index -lt $linesPerSection; $index++)
{
    $backendLines.Add($backendSourceLines[$index])
    $frontendLines.Add($frontendSourceLines[$index])
}

$allSourceLines = New-Object System.Collections.Generic.List[string]
foreach ($line in $backendLines) { $allSourceLines.Add($line) }
foreach ($line in $frontendLines) { $allSourceLines.Add($line) }

$totalLines = $allSourceLines.Count
if ($totalLines % $linesPerPage -ne 0)
{
    throw "Combined source line count ($totalLines) is not divisible by $linesPerPage."
}
$pageCount = [int]($totalLines / $linesPerPage)
$backendPageCount = [int]($backendLines.Count / $linesPerPage)

$displayLines = New-Object System.Collections.Generic.List[string]
for ($index = 0; $index -lt $backendLines.Count; $index++)
{
    $lineNumber = ($index + 1).ToString().PadLeft(5)
    $displayLines.Add($lineNumber + "  " + $backendLines[$index])
}
for ($index = 0; $index -lt $frontendLines.Count; $index++)
{
    $lineNumber = ($index + 1).ToString().PadLeft(5)
    $displayLines.Add($lineNumber + "  " + $frontendLines[$index])
}

$eastAsiaFont = "微软雅黑"
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

$headerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="0" w:after="0"/></w:pPr>
    <w:r><w:rPr><w:rFonts w:ascii="$eastAsiaFont" w:hAnsi="$eastAsiaFont" w:eastAsia="$eastAsiaFont"/><w:b/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t>$(Escape-Xml "$softwareName $softwareVersion 前后端源程序鉴别材料")</w:t></w:r>
  </w:p>
</w:hdr>
"@

$footerXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:p>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="0" w:after="0"/></w:pPr>
    <w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:eastAsia="$eastAsiaFont"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t>PAGE </w:t></w:r>
    <w:r><w:fldChar w:fldCharType="begin"/></w:r>
    <w:r><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
    <w:r><w:fldChar w:fldCharType="separate"/></w:r>
    <w:r><w:t>1</w:t></w:r>
    <w:r><w:fldChar w:fldCharType="end"/></w:r>
    <w:r><w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:eastAsia="$eastAsiaFont"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr><w:t> / $pageCount</w:t></w:r>
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
$coreXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>$(Escape-Xml "$softwareName $softwareVersion 前后端源程序鉴别材料")</dc:title>
  <dc:subject>$(Escape-Xml "计算机软件著作权登记源程序材料（后端代码与前端代码）")</dc:subject>
  <dc:creator>$(Escape-Xml $softwareName)</dc:creator>
  <cp:keywords>$(Escape-Xml "软件著作权;后端源代码;前端源代码;Unity;Java;C#")</cp:keywords>
  <dc:description>$(Escape-Xml "软件名称：$softwareName；版本号：$softwareVersion；后端代码 $($backendLines.Count) 行，前端代码 $($frontendLines.Count) 行，共 $totalLines 行、$pageCount 页，每页 $linesPerPage 行。后端采用原后端材料前半部分。")</dc:description>
  <dcterms:created xsi:type="dcterms:W3CDTF">$timestamp</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">$timestamp</dcterms:modified>
</cp:coreProperties>
"@

$appXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft Office Word</Application>
  <AppVersion>16.0000</AppVersion>
  <Pages>$pageCount</Pages>
  <Lines>$totalLines</Lines>
  <Words>$totalLines</Words>
  <Characters>$(($displayLines -join "`n").Length)</Characters>
</Properties>
"@

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("weijun-combined-source-" + [Guid]::NewGuid().ToString("N"))
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

    if (Test-Path -LiteralPath $outputDocxPath) { Remove-Item -LiteralPath $outputDocxPath -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::Open($outputDocxPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try
    {
        foreach ($file in Get-ChildItem -LiteralPath $temporaryRoot -Recurse -File)
        {
            $relativePath = $file.FullName.Substring($resolvedTempRoot.Length).TrimStart([char[]]@('\', '/'))
            $entryName = $relativePath.Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $archive.Dispose() }

    $htmlBuilder = New-Object System.Text.StringBuilder
    [void]$htmlBuilder.AppendLine('<!doctype html>')
    [void]$htmlBuilder.AppendLine('<html lang="zh-CN"><head><meta charset="utf-8"><title>' + (Escape-Html "$softwareName $softwareVersion 前后端源程序") + '</title>')
    [void]$htmlBuilder.AppendLine('<style>')
    [void]$htmlBuilder.AppendLine('@page { size: A4 portrait; margin: 0; }')
    [void]$htmlBuilder.AppendLine('html, body { margin: 0; padding: 0; background: #fff; }')
    [void]$htmlBuilder.AppendLine('.page { position: relative; box-sizing: border-box; width: 210mm; height: 297mm; padding: 15mm 12mm 13mm; page-break-after: always; overflow: hidden; }')
    [void]$htmlBuilder.AppendLine('.page:last-child { page-break-after: auto; }')
    [void]$htmlBuilder.AppendLine('.header { position: absolute; top: 5mm; left: 12mm; right: 12mm; height: 7mm; text-align: center; font-family: "微软雅黑", "Noto Sans SC", sans-serif; font-size: 9pt; font-weight: 700; color: #111; white-space: nowrap; }')
    [void]$htmlBuilder.AppendLine('.code { margin: 0; width: 178%; height: 266mm; overflow: hidden; white-space: pre; transform: scaleX(0.56); transform-origin: top left; font-family: Consolas, "微软雅黑", "Noto Sans SC", monospace; font-size: 7.2pt; line-height: 5.2mm; color: #111; tab-size: 4; }')
    [void]$htmlBuilder.AppendLine('.footer { position: absolute; bottom: 5mm; left: 12mm; right: 12mm; height: 6mm; text-align: center; font-family: Consolas, "微软雅黑", "Noto Sans SC", sans-serif; font-size: 8.5pt; color: #333; }')
    [void]$htmlBuilder.AppendLine('</style></head><body>')

    for ($pageIndex = 0; $pageIndex -lt $pageCount; $pageIndex++)
    {
        $sectionLabel = if ($pageIndex -lt $backendPageCount) { $sourceLabels[0] } else { $sourceLabels[1] }
        [void]$htmlBuilder.Append('<section class="page"><div class="header">')
        [void]$htmlBuilder.Append((Escape-Html "$softwareName $softwareVersion 源程序鉴别材料（$sectionLabel）"))
        [void]$htmlBuilder.Append('</div><pre class="code">')
        for ($lineIndex = 0; $lineIndex -lt $linesPerPage; $lineIndex++)
        {
            $sourceIndex = $pageIndex * $linesPerPage + $lineIndex
            [void]$htmlBuilder.Append((Escape-Html $displayLines[$sourceIndex]))
            if ($lineIndex -lt $linesPerPage - 1) { [void]$htmlBuilder.Append("`n") }
        }
        [void]$htmlBuilder.Append('</pre><div class="footer">PAGE ')
        [void]$htmlBuilder.Append(($pageIndex + 1).ToString())
        [void]$htmlBuilder.Append(' / ')
        [void]$htmlBuilder.Append($pageCount.ToString())
        [void]$htmlBuilder.Append('</div></section>')
    }
    [void]$htmlBuilder.AppendLine('</body></html>')

    $htmlPath = Join-Path $temporaryRoot "source.html"
    Write-Utf8NoBom $htmlPath $htmlBuilder.ToString()
    if (Test-Path -LiteralPath $outputPdfPath) { Remove-Item -LiteralPath $outputPdfPath -Force }
    $edgeCandidates = @(
        "C:\Program Files (x86)\Microsoft\Edge\Application\152.0.4191.53\msedge.exe",
        "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
    )
    $edgePath = $edgeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($edgePath)) { throw "Microsoft Edge was not found; cannot render the PDF locally." }
    $profilePath = Join-Path $temporaryRoot "edge-profile"
    $htmlUri = ([System.Uri]$htmlPath).AbsoluteUri
    & $edgePath --headless=new --disable-gpu --disable-gpu-compositing --disable-software-rasterizer --disable-extensions --disable-background-networking --disable-component-update --no-first-run --disable-sync --disable-default-apps --no-sandbox --no-pdf-header-footer --run-all-compositor-stages-before-draw --virtual-time-budget=1000 --user-data-dir="$profilePath" --print-to-pdf="$outputPdfPath" "$htmlUri" | Out-Null
    if (-not (Test-Path -LiteralPath $outputPdfPath -PathType Leaf)) { throw "Edge did not create the PDF: $outputPdfPath" }
}
finally
{
    if (Test-Path -LiteralPath $resolvedTempRoot) { Remove-Item -LiteralPath $resolvedTempRoot -Recurse -Force }
}

Write-Output "DOCX=$outputDocxPath"
Write-Output "PDF=$outputPdfPath"
Write-Output "BACKEND_LINES=$($backendLines.Count) (numbered 1-$($backendLines.Count))"
Write-Output "FRONTEND_LINES=$($frontendLines.Count) (numbered 1-$($frontendLines.Count))"
Write-Output "TOTAL_LINES=$totalLines"
Write-Output "PAGES=$pageCount"
Write-Output "LINES_PER_PAGE=$linesPerPage"
