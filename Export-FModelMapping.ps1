param(
    # [Parameter(Mandatory=$true)]
    # [string]$GameType,

    [Parameter(Mandatory=$true)]
    [string]$GameName,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputFolder,

    [Parameter(Mandatory=$false)]
    # [string]$FModelPath = "C:\Users\iristtzhou\source\repos\FModel\FModel\bin\Release\net8.0-windows\win-x64\FModel.exe"
    [string]$FModelPath = "C:\Users\iristtzhou\source\repos\FModel\FModel\bin\Debug\net8.0-windows\win-x64\FModel.exe"
)

Add-Type -AssemblyName System.Windows.Forms


function Write-Log {
    param($Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Write-Host "[$timestamp] $Message"
}

function Test-PathExists {
    param($Path, $Description)
    if (-not (Test-Path $Path)) {
        Write-Log "ERROR: $Description not found at: $Path"
        exit 1
    }
}


$appDataPath = [Environment]::GetFolderPath('ApplicationData')
$fmodelSettingsPath = Join-Path $appDataPath "FModel\AppSettings_Debug.json"
Write-Host "$fmodelSettingsPath"
# $gameBasePath = "Z:\jinmianye-cfs-sh\tmp\$GameType\$GameName"
$gameBasePath = "D:\GameData\$GameName"
$appSettingsSourcePath = "Z:\jinmianye-cfs-sh\smb_service\AppSettings\$GameName\AppSettings.json"
$outputCsvPath = Join-Path $OutputFolder "$GameName`_mesh_material_mapping.csv"


Write-Log "Validating paths..."
Test-PathExists $appSettingsSourcePath "Source AppSettings.json"
Test-PathExists $gameBasePath "Game directory"
Test-PathExists $FModelPath "FModel executable"

if (-not (Test-Path $OutputFolder)) {
    Write-Log "Creating output directory: $OutputFolder"
    New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null
}


$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
if (Test-Path $fmodelSettingsPath) {
    Write-Log "Backing up existing AppSettings.json"
    Copy-Item $fmodelSettingsPath "$fmodelSettingsPath.$timestamp.backup"
}


Write-Log "Updating FModel settings..."
$settings = Get-Content $appSettingsSourcePath -Raw | ConvertFrom-Json
$settings.GameDirectory = $gameBasePath
$settings.OutputDirectory = $OutputFolder

foreach ($endpoint in $settings.PerDirectory.PSObject.Properties.Value.Endpoints) {
    if ($endpoint.Overwrite -eq $true) {
        $endpoint.FilePath = "Z:\jinmianye-cfs-sh\smb_service\AppSettings\$GameName\$GameName.usmap"
        $endpoint.IsValid = $true
    }
}

$newPerDirectory = @{}
foreach ($key in $settings.PerDirectory.PSObject.Properties.Name) {
    $directorySettings = $settings.PerDirectory.$key
    $newKey = $gameBasePath
    $directorySettings.GameDirectory = $gameBasePath
    $newPerDirectory[$newKey] = $directorySettings
}
$settings.PerDirectory = $newPerDirectory

$settings | ConvertTo-Json -Depth 100 | Set-Content $fmodelSettingsPath

function Wait-ForWindow {
    param (
        [string]$WindowTitle,
        [int]$TimeoutSeconds = 30
    )
    Write-Log "Waiting for window: $WindowTitle"
    $timer = 0
    while ($timer -lt $TimeoutSeconds) {
        $window = Get-Process | Where-Object { $_.MainWindowTitle -match $WindowTitle }
        if ($window) { 
            Write-Log "Window found"
            return $true 
        }
        Start-Sleep -Seconds 1
        $timer++
    }
    Write-Log "Timeout waiting for window: $WindowTitle"
    return $false
}


Write-Log "Starting FModel..."
$process = Start-Process $FModelPath -PassThru

if (-not (Wait-ForWindow "FModel")) {
    Write-Log "ERROR: FModel failed to start"
    exit 1
}



Write-Log "Waiting for FModel window..."
$fmodel = Wait-ForWindow "FModel"
if ($null -eq $fmodel) {
    Write-Log "ERROR: FModel window not found"
    exit 1
}




Write-Log "Process completed"