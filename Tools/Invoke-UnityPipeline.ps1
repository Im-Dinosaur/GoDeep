param(
    [Parameter(Mandatory=$true)][string]$Command,
    [string]$ParametersJson = '{}',
    [int]$TimeoutSec = 45
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$pipelineDescriptor = Get-Content -LiteralPath (Join-Path $projectRoot 'Library\Pipeline\.unity-pipeline-port') -Raw | ConvertFrom-Json
$pipelineHeaders = @{ Authorization = 'Bearer ' + $pipelineDescriptor.evalToken }
$pipelineBody = @{ command = $Command; parameters = ($ParametersJson | ConvertFrom-Json) } | ConvertTo-Json -Depth 15 -Compress
$pipelineResult = Invoke-RestMethod -Uri ('http://127.0.0.1:' + $pipelineDescriptor.port + '/api/exec') -Method Post -Headers $pipelineHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($pipelineBody)) -TimeoutSec $TimeoutSec
$pipelineResult | ConvertTo-Json -Depth 20
