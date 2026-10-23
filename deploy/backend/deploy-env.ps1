<#
.SYNOPSIS
Uploads the API's environment file to the Oracle server as /etc/strategy-trading-lab/api.env and restarts the API.

.DESCRIPTION
The values live in a local file that is never committed: deploy/backend/production.env by default (*.env is
gitignored), made from production.env.example. The server keeps the old file as api.env.previous and puts it back if
the API isn't healthy with the new one. Prints variable names only, never values. Runs on Windows PowerShell 5.1 and
PowerShell 7. Needs the strategy-lab host in ~/.ssh/config.
#>
param(
    [string]$EnvFile = (Join-Path $PSScriptRoot 'production.env'),
    [string]$SshHost = 'strategy-lab'
)

$ErrorActionPreference = 'Stop'

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Failed with exit code ${LASTEXITCODE}: $Command" }
}

if (-not (Test-Path $EnvFile)) {
    throw "$EnvFile doesn't exist: copy production.env.example next to it and fill in the values."
}

# The same checks as the server's, before anything is sent.
$lines = Get-Content $EnvFile
$bad = @(for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -notmatch '^([A-Z][A-Z0-9_]*=.*|#.*|\s*)$') { $i + 1 } })
if ($bad) { throw "Not KEY=value on line(s) $($bad -join ', ') of $EnvFile" }
$keys = @($lines | Where-Object { $_ -match '^([A-Z][A-Z0-9_]*)=' } | ForEach-Object { $Matches[1] })
foreach ($key in 'DATABASE_URL', 'DATABASE_URL_UNPOOLED') {
    if (-not ($lines -match "^$key=.+")) { throw "$key is missing or empty in $EnvFile" }
}
$known = (Get-Content (Join-Path $PSScriptRoot 'production.env.example') | Where-Object { $_ -match '^#?\s*([A-Z][A-Z0-9_]*)=' } |
    ForEach-Object { $Matches[1] })
$unknown = @($keys | Where-Object { $_ -notin $known })
if ($unknown) { Write-Warning "Not in production.env.example (a typo?): $($unknown -join ', ')" }
Write-Host "Uploading $($keys.Count) variables to ${SshHost}: $($keys -join ', ')"

# A private folder per run; install-env.sh deletes the upload whatever happens.
$remote = ssh $SshHost 'mktemp -d /tmp/strategy-trading-lab-env.XXXXXX'
if ($LASTEXITCODE -ne 0) { throw "Can't reach $SshHost" }
try {
    Invoke-Native { scp $EnvFile "${SshHost}:$remote/api.env" }
    Invoke-Native { scp (Join-Path $PSScriptRoot 'install-env.sh') "${SshHost}:$remote/install-env.sh" }
    Invoke-Native { ssh $SshHost "sudo bash $remote/install-env.sh $remote/api.env" }
}
finally {
    ssh $SshHost "rm -rf $remote"
}

$health = Invoke-RestMethod 'https://140-238-209-178.sslip.io/api/health'
Write-Host "Live: $($health | ConvertTo-Json -Compress)"
