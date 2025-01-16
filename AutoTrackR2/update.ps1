$repoUrl = "https://api.github.com/repos/BubbaGumpShrump/AutoTrackR2/releases/latest"
$outputMsi = Join-Path -Path $env:TEMP -ChildPath "AutoTrackR2_Setup.msi"
$tempFolder = Join-Path -Path $env:TEMP -ChildPath "AutoTrackR2"
$headers = @{ "User-Agent" = "Mozilla/5.0" }

# Fetch latest release data
$response = Invoke-RestMethod -Uri $repoUrl -Headers $headers

# Find the MSI asset
$asset = $response.assets | Where-Object { $_.name -eq "AutoTrackR2_Setup.msi" }

if ($asset -ne $null) {
    $downloadUrl = $asset.browser_download_url
    Write-Host "Downloading $($asset.name) from $downloadUrl"
    Invoke-WebRequest -Uri $downloadUrl -OutFile $outputMsi -Headers $headers
    Write-Host "Download completed: $outputMsi"

    # Extract MSI contents
    if (Test-Path $tempFolder) {
        Remove-Item -Recurse -Force $tempFolder
    }
    Write-Host "Extracting MSI files..."

    # Unpack the MSI installer to the temporary folder using msiexec with /a (administrative install) and /qb (quiet mode)
    Start-Process msiexec.exe -ArgumentList "/a `"$outputMsi`" /qb TARGETDIR=`"$tempFolder`"" -Wait

    # Generate checksums of extracted files and current directory files
    $tempFiles = Get-ChildItem -Path $tempFolder -Recurse
    $currentFiles = Get-ChildItem -Path (Get-Location) -Recurse

    $tempChecksums = @{}
    $currentChecksums = @{}

    # Generate checksums for the temp folder files
    foreach ($file in $tempFiles) {
        if (-not $file.PSIsContainer) {
            $tempChecksums[$file.FullName] = Get-FileHash $file.FullName -Algorithm SHA256
        }
    }

    # Generate checksums for the current directory files
    foreach ($file in $currentFiles) {
        if (-not $file.PSIsContainer) {
            $currentChecksums[$file.FullName] = Get-FileHash $file.FullName -Algorithm SHA256
        }
    }

    # Compare and overwrite files if changed or missing, excluding update.ps1
    foreach ($file in $tempChecksums.Keys) {
        $relativePath = $file.Substring($tempFolder.Length)
        
        # Skip the update.ps1 file
        if ($relativePath -eq "\update.ps1") {
            continue
        }

        $currentFilePath = Join-Path -Path (Get-Location) -ChildPath $relativePath

        if (-not (Test-Path $currentFilePath) -or ($currentChecksums[$currentFilePath].Hash -ne $tempChecksums[$file].Hash)) {
            Write-Host "Copying $relativePath to current directory"
            Copy-Item -Path $file -Destination $currentFilePath -Force
        }
    }

    Write-Host "Files are successfully updated."
} else {
    Write-Host "AutoTrackR2_Setup.msi not found in the latest release."
}