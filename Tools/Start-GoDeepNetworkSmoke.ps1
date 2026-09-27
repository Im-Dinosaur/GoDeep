param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Z0-9]{8}$')][string]$RoomCode,
    [Parameter(Mandatory=$true)][ValidateRange(1,8)][int]$PeerId
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$buildDirectory = Join-Path $projectRoot '.utmp\network-20260928\build'
$executable = Join-Path $buildDirectory 'GoDeep.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the Windows development client first.' }
$logPath = Join-Path $buildDirectory ('smoke-' + $PeerId + '.log')
$arguments = @('-batchmode', '-nographics', '-godeepSmokeRoom', $RoomCode, '-godeepSmokeId', $PeerId.ToString(), '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $executable -WorkingDirectory $buildDirectory -ArgumentList $arguments -WindowStyle Hidden -PassThru
[pscustomobject]@{ peerId = $PeerId; processId = $process.Id; report = (Join-Path $buildDirectory ('smoke-' + $PeerId + '.json')) }
