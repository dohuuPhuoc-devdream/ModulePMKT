# Run from a Windows account allowed to access the configured local SQL demo.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
 New-Item -ItemType Directory -Force artifacts/quality | Out-Null
 foreach($phase in 3..9) {
  $script=Join-Path $PSScriptRoot ("run-phase$phase.ps1")
  & $script *> "artifacts/quality/phase$phase.log"
  Write-Output "PASS: Phase $phase (details: artifacts/quality/phase$phase.log)"
 }
 & sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify.sql *> artifacts/quality/schema.log
 if($LASTEXITCODE -ne 0){throw 'Schema checks failed; see artifacts/quality/schema.log'}
 & sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql *> artifacts/quality/inventory.log
 if($LASTEXITCODE -ne 0){throw 'Inventory checks failed; see artifacts/quality/inventory.log'}
 Write-Output 'PASS: independent SQL checks'
} finally {Pop-Location}
