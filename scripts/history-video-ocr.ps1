# Research-only OCR of the visible documentary date; never simulation input.
param([Parameter(Mandatory=$true)][string]$Folder)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
[Windows.Storage.StorageFile,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.FileAccessMode,Windows.Storage,ContentType=WindowsRuntime] | Out-Null
[Windows.Storage.Streams.IRandomAccessStream,Windows.Storage.Streams,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder,Windows.Graphics.Imaging,ContentType=WindowsRuntime] | Out-Null
[Windows.Graphics.Imaging.SoftwareBitmap,Windows.Graphics.Imaging,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine,Windows.Media.Ocr,ContentType=WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrResult,Windows.Media.Ocr,ContentType=WindowsRuntime] | Out-Null
$taskMethod = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.IsGenericMethod -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } | Select-Object -First 1
function Await-Result($Operation, [Type]$ResultType) {
    $task = $taskMethod.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $task.Wait()
    $task.Result
}
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if (-not $engine) { throw 'No Windows OCR engine is installed for the current language.' }
$rows = @()
foreach ($frame in (Get-ChildItem -LiteralPath $Folder -Filter 'frame-*.jpg' | Sort-Object Name)) {
    $file = Await-Result ([Windows.Storage.StorageFile]::GetFileFromPathAsync($frame.FullName)) ([Windows.Storage.StorageFile])
    $stream = Await-Result ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
    try {
        $decoder = Await-Result ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
        $bitmap = Await-Result ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
        try { $result = Await-Result ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult]) }
        finally { $bitmap.Dispose() }
        $dateLines = @($result.Lines | Where-Object {
            $_.Words.Count -gt 0 -and $_.Words[0].BoundingRect.X -lt 300 -and
            $_.Words[0].BoundingRect.Y -ge 525 -and $_.Words[0].BoundingRect.Y -lt 605 -and
            $_.Text -match '\d'
        } | ForEach-Object Text)
        $rows += [pscustomobject]@{File=$frame.Name;DateText=($dateLines -join ' | ');Text=$result.Text}
        if ($rows.Count % 50 -eq 0) { Write-Output "OCR $($rows.Count) frames" }
    } finally { $stream.Dispose() }
}
$json = ConvertTo-Json -InputObject $rows -Depth 4
[System.IO.File]::WriteAllText((Join-Path $Folder 'frame-ocr.json'), $json, [System.Text.UTF8Encoding]::new($false))
Write-Output "Wrote OCR for $($rows.Count) frames"
