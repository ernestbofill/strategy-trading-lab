<#
.SYNOPSIS
Builds the backend for the Oracle server (Ubuntu, arm64) and installs it there as a new release.

.DESCRIPTION
Used by the Deploy backend workflow and the "Deploy backend to Oracle" VS Code task. Runs on Windows
PowerShell 5.1 and PowerShell 7. Needs the strategy-lab host in ~/.ssh/config, and on the server the
environment file deploy/backend/README.md describes.
#>
param([string]$SshHost = 'strategy-lab')

$ErrorActionPreference = 'Stop'

function Invoke-Native([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Failed with exit code ${LASTEXITCODE}: $Command" }
}

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$backend = Join-Path $repo 'backend'
$version = ([xml](Get-Content (Join-Path $backend 'Directory.Build.props'))).Project.PropertyGroup.Version
$out = Join-Path $backend 'artifacts/deploy'
$package = Join-Path $backend "artifacts/strategy-trading-lab-api-$version.tar.gz"
Write-Host "Deploying backend $version to $SshHost"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
Push-Location $backend
try {
    Invoke-Native { dotnet tool restore }
    # Self-contained: the server has no .NET installed.
    Invoke-Native { dotnet publish src/StrategyTradingLab.Api -c Release -r linux-arm64 --self-contained -o "$out/app" }
    Invoke-Native { dotnet ef migrations bundle --project src/StrategyTradingLab.Api --configuration Release -r linux-arm64 --self-contained -o "$out/efbundle" --force }
}
finally {
    Pop-Location
}
Copy-Item (Join-Path $PSScriptRoot 'install.sh'), (Join-Path $PSScriptRoot 'strategy-trading-lab-api.service') $out
# Relative paths: GNU tar (Git Bash) reads "C:" as a remote host.
Push-Location (Split-Path $package)
try { Invoke-Native { tar -czf (Split-Path -Leaf $package) -C deploy . } }
finally { Pop-Location }

$remote = "/tmp/strategy-trading-lab-api-$version"
Invoke-Native { scp $package "${SshHost}:$remote.tar.gz" }
$install = 'rm -rf {0} && mkdir {0} && tar -xzf {0}.tar.gz -C {0} && sudo bash {0}/install.sh {1}; status=$?; rm -rf {0} {0}.tar.gz; exit $status' -f $remote, $version
Invoke-Native { ssh $SshHost $install }

$live = Invoke-RestMethod 'https://140-238-209-178.sslip.io/api/version'
Write-Host "Live: $($live.version)"
