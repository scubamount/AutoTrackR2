$TrackRver = "2.06"

# Path to the config file
$appName = "AutoTrackR2"
$scriptFolder = Join-Path -Path $env:LOCALAPPDATA -ChildPath $appName
$configFile = Join-Path -Path $scriptFolder -ChildPath "config.ini"

# Function to load configuration from ini file
function Load-Config {
    param (
        [string]$configFile
    )
    
    if (Test-Path $configFile) {
        Write-Host "Config.ini found."
        $configContent = Get-Content $configFile | Where-Object { $_ -notmatch '^#|^\s*$' }
        $configContent = $configContent -replace '\\', '\\\\'
        return $configContent -replace '^([^=]+)=(.+)$', '$1=$2' | ConvertFrom-StringData
    } else {
        Write-Host "Config.ini not found."
       # exit
    }
}

# Function to clean ship names
function Clean-ShipName {
    param (
        [string]$name
    )
    while ($name -match '_(PU|AI|CIV|MIL|PIR)$') {
        $name = $name -replace '_(PU|AI|CIV|MIL|PIR)$', ''
    }
    while ($name -match '-00(1|2|3|4|5|6|7|8|9|0)$') {
        $name = $name -replace '-00(1|2|3|4|5|6|7|8|9|0)$', ''
    }
    return $name
}

# Function to process new log entries
function Read-LogEntry {
    param (
        [string]$line,
        [string]$userName,
        [string]$loadOut,
        [string]$apiUrl,
        [string]$apiKey,
        [string]$scriptFolder,
        [string]$videoPath,
        [bool]$offlineMode
    )

    # Define regex patterns
    $killPattern = "<Actor Death> CActor::Kill: '(?<EnemyPilot>[^']+)' \[\d+\] in zone '(?<EnemyShip>[^']+)' killed by '(?<Player>[^']+)' \[[^']+\] using '(?<Weapon>[^']+)' \[Class (?<Class>[^\]]+)\] with damage type '(?<DamageType>[^']+)'"
    $cleanupPattern = '^(.+?)_\d+$'
    $joinDatePattern = '<span class="label">Enlisted</span>\s*<strong class="value">([^<]+)</strong>'
    $ueePattern = '<p class="entry citizen-record">\s*<span class="label">UEE Citizen Record<\/span>\s*<strong class="value">#?(n\/a|\d+)<\/strong>\s*<\/p>'

    # Look for vehicle events
    if ($line -match "<(?<timestamp>[^>]+)> \[Notice\] <Vehicle Destruction> CVehicle::OnAdvanceDestroyLevel: Vehicle '(?<vehicle>[^']+)' \[\d+\] in zone '(?<vehicle_zone>[^']+)'") {
        $global:vehicle_id = $matches['vehicle']
        $global:location = $matches['vehicle_zone']
    }

    # Apply the regex pattern to the line
    if ($line -match $killPattern) {
        # Access the named capture groups from the regex match
        $enemyPilot = $matches['EnemyPilot']
        $enemyShip = $matches['EnemyShip']
        $player = $matches['Player']
        $weapon = $matches['Weapon']
        $damageType = $matches['DamageType']

        # Initialize variables
        $global:got_location = "NONE"
        if ($enemyShip -ne "vehicle_id") {
            $global:got_location = $global:location
        }

        # Fetch additional data from the API
        try {
            $page1 = Invoke-WebRequest -uri "https://robertsspaceindustries.com/citizens/$enemyPilot"
        } catch {
            $page1 = $null
        }

        if ($null -ne $page1) {
            # Check if the Autotrackr2 process is running
            if (-not (Get-Process -ID $parentApp -ErrorAction SilentlyContinue)) {
                Stop-Process -Id $PID -Force
            }

            if ($player -eq $userName -and $enemyPilot -ne $userName) {
                if ($enemyShip -match $cleanupPattern) {
                    $enemyShip = $matches[1]
                }
                if ($weapon -match $cleanupPattern) {
                    $weapon = $matches[1]
                }

                # Process kill data and send to API
                $killTime = (Get-Date).ToUniversalTime().ToString("dd MMM yyyy HH:mm 'UTC'", [System.Globalization.CultureInfo]::InvariantCulture)
                $joinDate2 = if ($($page1.content) -match $joinDatePattern) { $matches[1] -replace ',', '' } else { "-" }
                
                if ($($page1.content) -match $ueePattern) {
                    $citizenRecord = $matches[1]
                } else {
                    $citizenRecord = "n/a"
                }
                
                $victimPFP = if ($page1.images[0].src -like "/media/*") { "https://robertsspaceindustries.com$($page1.images[0].src)" } else { "https://cdn.robertsspaceindustries.com/static/images/account/avatar_default_big.jpg" }

                # Prepare data for API
                if ($null -ne $apiUrl -and -not $offlineMode) {
                    $data = @{
                        victim_ship     = $enemyShip
                        victim          = $enemyPilot
                        enlisted        = $joinDate2
                        rsi             = if ($citizenRecord -eq "n/a") { "-1" } else { $citizenRecord }
                        weapon          = $weapon
                        method          = $damageType
                        loadout_ship    = $loadOut
                        game_version    = $global:GameVersion
                        gamemode        = $global:GameMode
                        trackr_version  = $TrackRver
                        location        = $global:got_location
                    }

                    # Send API request
                    $headers = @{
                        "Authorization" = "Bearer $apiKey"
                        "Content-Type"  = "application/json"
                        "User-Agent"    = "AutoTrackR2"
                    }

                    try {
                        Invoke-RestMethod -Uri $apiUrl -Method Post -Body ($data | ConvertTo-Json -Depth 5) -Headers $headers
                    } catch {
                        Write-Host "API Call Failed: $_"
                    }
                }

                # Prepare to write to CSV
                $csvPath = "$scriptFolder\Kill-log.csv"
                $killData = [PSCustomObject]@{
                    KillTime         = $killTime
                    EnemyPilot       = $enemyPilot
                    EnemyShip        = $enemyShip
                    Enlisted         = $joinDate2
                    RecordNumber     = $citizenRecord
                    OrgAffiliation   = "-"
                    Player           = $player
                    Weapon           = $weapon
                    Ship             = $loadOut
                    Method           = $damageType
                    Mode             = $global:GameMode
                    GameVersion      = $global:GameVersion
                    TrackRver        = $TrackRver
                    Logged           = "API"
                    PFP              = $victimPFP
                }

                # Export to CSV
                if (-Not (Test-Path $csvPath)) {
                    $killData | Export-Csv -Path $csvPath -NoTypeInformation
                } else {
                    $killData | ConvertTo-Csv -NoTypeInformation | Select-Object -Skip 1 | Out-File -Append -Encoding utf8 -FilePath $csvPath
                }
            }
        }
    }
}

# Load the configuration
$config = Load-Config -configFile $configFile
$parentApp = (Get-Process -Name AutoTrackR2 -ErrorAction SilentlyContinue).ID

# Access config values
$logFilePath = $config.Logfile
$apiUrl = $config.ApiUrl
$apiKey = $config.ApiKey
$videoPath = $config.VideoPath
$visorWipe = $config.VisorWipe -eq '1'
$videoRecord = $config.VideoRecord -eq '1'
$offlineMode = $config.OfflineMode -eq '1'

Write-Host "OfflineMode: $offlineMode, VideoRecord: $videoRecord, VisorWipe: $visorWipe"

# Check log file path
if (-Not (Test-Path $logFilePath)) {
    Write-Host "Logfile not found."
    exit
}

# Load historic kills from CSV
$global:killTally = 0
if (Test-Path "$scriptFolder\Kill-Log.csv") {
    $historicKills = Import-CSV "$scriptFolder\Kill-log.csv"
    $currentDate = Get-Date
    $dateFormat = "dd MMM yyyy HH:mm UTC"
    
    foreach ($kill in $historicKills) {
        $killDate = [datetime]::ParseExact($kill.KillTime, $dateFormat, [System.Globalization.CultureInfo]::InvariantCulture)
        if ($killDate.Year -eq $currentDate.Year -and $killDate.Month -eq $currentDate.Month) {
            $global:killTally++
        }
        Write-Host "NewKill=throwaway,$($kill.EnemyPilot),$($kill.EnemyShip),$($kill.OrgAffiliation),$($kill.Enlisted),$($kill.RecordNumber),$($kill.KillTime), $($kill.PFP)"
    }
}
Write-Host "KillTally=$global:killTally"

# Match and extract username from game log
$global:userName = $null
Do {
    $authLog = Get-Content -Path $logFilePath
    foreach ($line in $authLog) {
        if ($line -match $loginPattern) {
            $global:userName = $matches['Player']
            Write-Host "PlayerName=$global:userName"
        }

        if ($line -match $loadoutPattern) {
            $entity = $matches['Entity']
            $ownerGEID = $matches['OwnerGEID']
            if ($ownerGEID -eq $global:userName -and $entity -match $shipManPattern) {
                $tryloadOut = $entity
                if ($tryloadOut -match $cleanupPattern) {
                    $global:loadOut = $matches[1]
                }
                Write-Host "PlayerShip=$global:loadOut"
            }
        }

        if ($line -match $versionPattern) {
            $global:GameVersion = $matches['gameversion']
        }
        if ($line -match $acPattern) {
            $global:GameMode = "AC"
        }
        if ($line -match $puPattern) {
            $global:GameMode = "PU"
        }
        Write-Host "GameMode=$global:GameMode"
    }

    if (-not $global:userName) {
        Write-Host "PlayerName=No Player Found..."
        Start-Sleep -Seconds 30
    }
} until ($null -ne $global:userName)

# Monitor the log file and process new lines as they are added
Get-Content -Path $logFilePath -Wait -Tail 0 | ForEach-Object {
    Read-LogEntry $_ -userName $global:userName -loadOut $global:loadOut -apiUrl $apiUrl -apiKey $apiKey -scriptFolder $scriptFolder -videoPath $videoPath -offlineMode $offlineMode
}
