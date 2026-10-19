param([string]$ConnectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=ReportingModuleDemo;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10')
# Run from a Windows account allowed to access the configured local SQL demo.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
$lock=$null
$previousIdentityState=$env:MODULEPMKT_TEST_IDENTITY_PREPARED
$identityPrepared=$false
try {
 $lockPath=Join-Path $root 'artifacts\quality\run-all.lock'
 New-Item -ItemType Directory -Force (Split-Path $lockPath -Parent) | Out-Null
 try { $lock=[System.IO.File]::Open($lockPath,[System.IO.FileMode]::OpenOrCreate,[System.IO.FileAccess]::ReadWrite,[System.IO.FileShare]::None) }
 catch { throw 'Another full test suite is running; wait for it to finish before retrying.' }
 & '.\tests\set-test-identity.ps1' -Action Enable -ConnectionString $ConnectionString
 $identityPrepared=$true
 $env:MODULEPMKT_TEST_IDENTITY_PREPARED='1'
 New-Item -ItemType Directory -Force artifacts/quality | Out-Null
 foreach($phase in 3..9) {
  $script=Join-Path $PSScriptRoot ("run-phase$phase.ps1")
  & $script -ConnectionString $ConnectionString *> "artifacts/quality/phase$phase.log"
  if($LASTEXITCODE -ne 0){throw "Phase $phase failed; see artifacts/quality/phase$phase.log"}
  Write-Output "PASS: Phase $phase (details: artifacts/quality/phase$phase.log)"
 }
 & sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify.sql *> artifacts/quality/schema.log
 if($LASTEXITCODE -ne 0){throw 'Schema checks failed; see artifacts/quality/schema.log'}
 & sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql *> artifacts/quality/inventory.log
 if($LASTEXITCODE -ne 0){throw 'Inventory checks failed; see artifacts/quality/inventory.log'}
 $bundledPython=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
 $pythonExe=if(Test-Path -LiteralPath $bundledPython){$bundledPython}else{(Get-Command python -ErrorAction Stop).Source}
 & $pythonExe tests/verify-phase8.py *> artifacts/quality/output-verification.log
 if($LASTEXITCODE -ne 0){throw 'PDF/Excel independent checks failed; see artifacts/quality/output-verification.log'}
 Write-Output 'PASS: independent PDF and Excel output checks'
 & '.\tests\run-phase11.ps1' *> artifacts/quality/phase11.log
 if($LASTEXITCODE -ne 0){throw 'Phase 11 PDF/source checks failed; see artifacts/quality/phase11.log'}
 Write-Output 'PASS: Phase 11 QuestPDF and TT99 source checks'
 Write-Output 'PASS: independent SQL checks'
} finally {
 if($identityPrepared){try{& '.\tests\set-test-identity.ps1' -Action Disable -ConnectionString $ConnectionString}catch{Write-Warning "Could not disable test account: $_"}}
 $env:MODULEPMKT_TEST_IDENTITY_PREPARED=$previousIdentityState
 if($lock){$lock.Dispose();Remove-Item -LiteralPath $lockPath -ErrorAction SilentlyContinue}
 Pop-Location
}
