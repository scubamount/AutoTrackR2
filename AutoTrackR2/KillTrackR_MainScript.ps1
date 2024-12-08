$TrackRver = "2.0"

# Path to the config file
$appName = "AutoTrackR2"
$scriptFolder = Join-Path -Path $env:LOCALAPPDATA -ChildPath $appName
$configFile = Join-Path -Path $scriptFolder -ChildPath "config.ini"

# Read the config file into a hashtable
if (Test-Path $configFile) {
    Write-Output "PlayerName=Config.ini found."
    $configContent = Get-Content $configFile | Where-Object { $_ -notmatch '^#|^\s*$' }

    # Escape backslashes by doubling them
    $configContent = $configContent -replace '\\', '\\\\'

    # Convert to key-value pairs
    $config = $configContent -replace '^([^=]+)=(.+)$', '$1=$2' | ConvertFrom-StringData
} else {
    Write-Output "Config.ini not found."
    exit
}

# Access config values
$logFilePath = $config.Logfile
$apiUrl = $config.ApiUrl
$apiKey = $config.ApiKey
$videoPath = $config.VideoPath
$visorWipe = $config.VisorWipe
$videoRecord = $config.VideoRecord
$offlineMode = $config.OfflineMode

if ($offlineMode -eq 1){
	$offlineMode = $true
} else {
	$offlineMode = $false
}
Write-Output "PlayerName=OfflineMode: $offlineMode"

if ($videoRecord -eq 1){
	$videoRecord = $true
} else {
	$videoRecord = $false
}
Write-Output "PlayerName=VideoRecord: $videoRecord"

if ($visorWipe -eq 1){
	$visorWipe = $true
} else {
	$visorWipe = $false
}
Write-Output "PlayerName=VisorWipe: $visorWipe"

If (Test-Path $logFilePath) {
	Write-Output "PlayerName=Logfile found"
} else {
	Write-Output "Logfile not found."
}
If ($null -ne $apiUrl){
Write-output "PlayerName=$apiURL"
}

# Ship Manufacturers
$prefixes = @(
    "ORIG",
    "CRUS",
    "RSI",
    "AEGS",
    "VNCL",
    "DRAK",
    "ANVL",
    "BANU",
    "MISC",
    "CNOU",
    "XIAN",
    "GAMA",
    "TMBL",
    "ESPR",
    "KRIG",
    "GRIN",
    "XNAA"
)

# Define the regex pattern to extract information
$killPattern = "<Actor Death> CActor::Kill: '(?<EnemyPilot>[^']+)' \[\d+\] in zone '(?<EnemyShip>[^']+)' killed by '(?<Player>[^']+)' \[[^']+\] using '(?<Weapon>[^']+)' \[Class (?<Class>[^\]]+)\] with damage type '(?<DamageType>[^']+)'"
$puPattern = '<\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z> \[Notice\] <ContextEstablisherTaskFinished> establisher="CReplicationModel" message="CET completed" taskname="StopLoadingScreen" state=[^ ]+ status="Finished" runningTime=\d+\.\d+ numRuns=\d+ map="megamap" gamerules="SC_Default" sessionId="[a-f0-9\-]+" \[Team_Network\]\[Network\]\[Replication\]\[Loading\]\[Persistence\]'
$acPattern = "ArenaCommanderFeature"
$loadoutPattern = '\[InstancedInterior\] OnEntityLeaveZone - InstancedInterior \[(?<InstancedInterior>[^\]]+)\] \[\d+\] -> Entity \[(?<Entity>[^\]]+)\] \[\d+\] -- m_openDoors\[\d+\], m_managerGEID\[(?<ManagerGEID>\d+)\], m_ownerGEID\[(?<OwnerGEID>[^\[]+)\]'
$shipManPattern = "^(" + ($prefixes -join "|") + ")"
# $loginPattern = "\[Notice\] <AccountLoginCharacterStatus_Character> Character: createdAt [A-Za-z0-9]+ - updatedAt [A-Za-z0-9]+ - geid [A-Za-z0-9]+ - accountId [A-Za-z0-9]+ - name (?<Player>[A-Za-z0-9_-]+) - state STATE_CURRENT" # KEEP THIS INCASE LEGACY LOGIN IS REMOVED 
$loginPattern = "\[Notice\] <Legacy login response> \[CIG-net\] User Login Success - Handle\[(?<Player>[A-Za-z0-9_-]+)\]"
$cleanupPattern = '^(.+?)_\d+$'
$versionPattern = "--system-trace-env-id='pub-sc-alpha-(?<gameversion>\d{4}-\d{7})'"

# Lookup Patterns
$joinDatePattern = '<span class="label">Enlisted</span>\s*<strong class="value">([^<]+)</strong>'
$ueePattern = '<p class="entry citizen-record">\s*<span class="label">UEE Citizen Record<\/span>\s*<strong class="value">#?(n\/a|\d+)<\/strong>\s*<\/p>'

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$process = Get-Process | Where-Object {$_.Name -like "AutoTrackR2"}
$global:killTally = 0


# Load historic kills from csv
if (Test-Path "$scriptFolder\Kill-Log.csv") {
	$historicKills = Import-CSV "$scriptFolder\Kill-log.csv" | Sort-Object Descending
	foreach ($kill in $historicKills) {
		Write-Output "NewKill=throwaway,$($kill.EnemyPilot),$($kill.EnemyShip),$($kill.OrgAffiliation),$($kill.Enlisted),$($kill.RecordNumber),$($kill.KillTime),$($kill.PFP)"
		$global:killTally++
	}
}
Write-Output "KillTally=$global:killTally"

# Match and extract username from gamelog
Do {
    # Load gamelog into memory
    $authLog = Get-Content -Path $logFilePath

    # Initialize variable to store username
    $global:userName = $null
	$global:loadout = "Player"

    # Loop through each line in the log to find the matching line
    foreach ($line in $authLog) {
        if ($line -match $loginPattern) {
            $global:userName = $matches['Player']
            Write-Output "PlayerName=$global:userName"
        }
		# Get Loadout
		if ($line -match $loadoutPattern) {
			$entity = $matches['Entity']
			$ownerGEID = $matches['OwnerGEID']

			If ($ownerGEID -eq $global:userName -and $entity -match $shipManPattern) {
				$tryloadOut = $entity
				If ($tryloadOut -match $cleanupPattern){
					if ($null -ne $matches[1]){
						$global:loadOut = $matches[1]
					}
				}
			}
		}
		Write-Output "PlayerShip=$global:loadOut"

		If ($line -match $versionPattern){
			$GameVersion = $matches['gameversion']
		}
		if ($line -match $acPattern){
			$GameMode = "AC"
		}
		if ($line -match $puPattern){
			$GameMode = "PU"
		}
		Write-Output "GameMode=$GameMode"

	}
    # If no match found, print "Logged In: False"
    if (-not $global:userName) {
		Write-Output "PlayerName=No Player Found..."
        Start-Sleep -Seconds 30
    }

    # Clear the log from memory
    $authLog = $null
} until ($null -ne $global:userName)

# Function to process new log entries and write to the host
function Read-LogEntry {
    param (
        [string]$line
    )
    
    # Apply the regex pattern to the line
    if ($line -match $killPattern) {
        # Access the named capture groups from the regex match
        $enemyPilot = $matches['EnemyPilot']
        $enemyShip = $matches['EnemyShip']
        $player = $matches['Player']
		$weapon = $matches['Weapon']
		$damageType = $matches['DamageType']
		$ship = $global:loadOut

		Try {
			$page1 = Invoke-WebRequest -uri "https://robertsspaceindustries.com/citizens/$enemyPilot"
		} Catch {
			$page1 = $null
		}

		If ($null -ne $page1){
	
			If ($enemyShip -eq $global:lastKill){
				$enemyShip = "Passenger"
			} Else {
				$global:lastKill = $enemyShip
			}

			If ($player -eq $global:userName -and $enemyPilot -ne $global:userName){
				If ($enemyShip -match $cleanupPattern){
					$enemyShip = $matches[1]
				}
				If ($weapon -match $cleanupPattern){
					$weapon = $matches[1]
				}
				If ($weapon -eq "KLWE_MassDriver_S10"){
					$global:loadOut = "AEGS_Idris"
					$ship = "AEGS_Idris"
				}
				if ($damageType -like "*bullet*") {
					$ship = "Player"
				}
				If ($ship -match $cleanupPattern){
					$ship = $matches[1]
				}
				if ($ship -notmatch $shipManPattern){
					$ship = "Player"
				}
				If ($enemyShip -notmatch $shipManPattern) {
					$enemyShip = "Player"
				}
			
				# Repeatedly remove all suffixes
				while ($enemyShip -match '_(PU|AI|CIV|MIL|PIR)$') {
					$enemyShip = $enemyShip -replace '_(PU|AI|CIV|MIL|PIR)$', ''
				}
				# Repeatedly remove all suffixes
				while ($ship -match '_(PU|AI|CIV|MIL|PIR)$') {
					$ship = $ship -replace '_(PU|AI|CIV|MIL|PIR)$', ''
				}
				while ($enemyShip -match '-00(1|2|3|4|5|6|7|8|9|0)$') {
					$enemyShip = $enemyShip -replace '-00(1|2|3|4|5|6|7|8|9|0)$', ''
				}while ($ship -match '-00(1|2|3|4|5|6|7|8|9|0)$') {
					$ship = $ship -replace '-00(1|2|3|4|5|6|7|8|9|0)$', ''
				}

				$KillTime = (Get-Date).ToUniversalTime().ToString("d MMM yyyy H:mm 'UTC'")
			
				# Get Enlisted Date
				if ($($page1.content) -match $joinDatePattern) {
					$joinDate = $matches[1]
					$joinDate2 = $joinDate -replace ',', ''
				} else {
					$joinDate2 = "-"
				}

				# Check if there are any matches
				$enemyOrgs = $page1.links[3].innerHTML

				if ($null -eq $enemyOrgs) {
					$enemyOrgs = "-"
				}

				# Get UEE Number
				if ($($page1.content) -match $ueePattern) {
					# The matched UEE Citizen Record number is in $matches[1]
					$citizenRecord = $matches[1]
				} else {
					$citizenRecord = "-"
				}
				If ($citizenRecord -eq "n/a") {
					$citizenRecordAPI = "-1"
					$citizenRecord = "-"
				} Else {
					$citizenRecordAPI = $citizenRecord
				}

				# Get PFP
				if ($page1.images[0].src -like "/media/*") {
					$victimPFP = "https://robertsspaceindustries.com$($page1.images[0].src)"
				} Else {
					$victimPFP = $page1.images[0].src
				}

				$global:killTally++
				Write-Output "KillTally=$global:killTally"
				Write-Output "NewKill=throwaway,$enemyPilot,$enemyShip,$enemyOrgs,$joinDate2,$citizenRecord,$killTime,$victimPFP"

				$GameMode = $GameMode.ToLower()
				# Send to API
				# Define the data to send
				If ($null -ne $apiUrl -and $offlineMode -eq $false){
					$data = @{
						victim_ship		= $enemyShip
						victim			= $enemyPilot
						enlisted		= $joinDate
						rsi				= $citizenRecordAPI
						weapon			= $weapon
						method			= $damageType
						loadout_ship	= $ship
						game_version	= $GameVersion
						gamemode		= $GameMode
						trackr_version	= $TrackRver
					}

					# Headers which may or may not be necessary
					$headers = @{
						"Authorization" = "Bearer $apiKey"
						"Content-Type" = "application/json"
						"User-Agent" = "AutoTrackR2"
					}

					try {
						# Send the POST request with JSON data
						Invoke-RestMethod -Uri $apiURL -Method Post -Body ($data | ConvertTo-Json -Depth 5) -Headers $headers
						$logMode = "API"
					} catch {
						# Catch and display errors
						$apiError = $_
						# Add to output file
						$logMode = "Err-Local"
					}
				} Else {
					$logMode = "Local"
				}
			
				# Define the output CSV path
				$csvPath = "$scriptFolder\Kill-log.csv"

				# Create an object to hold the data
				$killData = [PSCustomObject]@{
					KillTime         = $killTime
					EnemyPilot       = $enemyPilot
					EnemyShip        = $enemyShip
					Enlisted         = $joinDate2
					RecordNumber     = $citizenRecord
					OrgAffiliation   = $enemyOrgs
					Player           = $player
					Weapon           = $weapon
					Ship             = $ship
					Method           = $damageType
					Mode             = $GameMode
					GameVersion      = $GameVersion
					TrackRver		 = $TrackRver
					Logged			 = $logMode
					PFP				 = $victimPFP
				}

				# Export to CSV
				if (-Not (Test-Path $csvPath)) {
					# If file doesn't exist, create it with headers
					$killData | Export-Csv -Path $csvPath -NoTypeInformation
				} else {
					# Append data to the existing file without adding headers
					$killData | ConvertTo-Csv -NoTypeInformation | Select-Object -Skip 1 | Out-File -Append -Encoding utf8 -FilePath $csvPath
				}

				$sleeptimer = 10

				# VisorWipe
				If ($visorwipe -eq $true -and $enemyShip -ne "Passenger" -and $damageType -notlike "*Bullet*"){
					# send keybind for visorwipe
					start-sleep 1
					$sleeptimer = $sleeptimer -1
					&"$scriptFolder\visorwipe.ahk"
				}
			
				# Record video
				if ($recording -eq $true -and $enemyShip -ne "Passenger"){
					# send keybind for windows game bar recording
					Start-Sleep 2
					$sleeptimer = $sleeptimer -9
					&"$scriptFolder\videorecord.ahk"
					Start-Sleep 7

					$latestFile = Get-ChildItem -Path $videoPath | Where-Object { -not $_.PSIsContainer } | Sort-Object CreationTime -Descending | Select-Object -First 1
					# Check if the latest file is no more than 10 seconds old
					if ($latestFile) {
						$fileAgeInSeconds = (New-TimeSpan -Start $latestFile.CreationTime -End (Get-Date)).TotalSeconds
						if ($fileAgeInSeconds -le 10) {
							# Generate a timestamp in ddMMMyyyy-HH:mm format
							$timestamp = (Get-Date).ToString("ddMMMyyyy-HHmm")
        
							# Extract the file extension to preserve it
							$fileExtension = $latestFile.Extension

							# Rename the file, preserving the original file extension
							Rename-Item -Path $latestFile.FullName -NewName "$enemyPilot.$enemyShip.$timestamp$fileExtension"
						} else {}
					} else {}
				}
				Start-Sleep $sleeptimer
			}
		}
	}

	# Get Logged-in User
	If ($line -match $loginPattern) {
		# Load gamelog into memory
		$authLog = Get-Content -Path $logFilePath
		$authLog = $authlog -match $loginPattern
		$authLog = $authLog | Out-String

		# Extract User Name
		$nameExtract = "name\s+(?<PlayerName>[^\s-]+)"

		If ($authLog -match $nameExtract -and $global:userName -ne $nameExtract){
			$global:userName = $matches['PlayerName']
			Write-Output "PlayerName=$global:userName"
		}
	}

	# Detect PU or AC
	if ($line -match $puPattern) {
		$GameMode = "PU"
		Write-Output "GameMode=$GameMode"
	}
	if ($line -match $acPattern) {
		$GameMode = "AC"
		Write-Output "GameMode=$GameMode"
	}

	#Set loadout 
	if ($line -match $loadoutPattern) {
		$entity = $matches['Entity']
		$ownerGEID = $matches['OwnerGEID']

        If ($ownerGEID -eq $global:userName -and $entity -match $shipManPattern) {
			$tryloadOut = $entity
			If ($tryloadOut -match $cleanupPattern){
				$global:loadOut = $matches[1]
			}
			Write-Output "PlayerShip=$global:loadOut"
		}
	}
}

# Monitor the log file and process new lines as they are added
Get-Content -Path $logFilePath -Wait -Tail 0 | ForEach-Object {
    Read-LogEntry $_
}
#>