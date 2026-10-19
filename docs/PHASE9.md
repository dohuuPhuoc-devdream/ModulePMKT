# Phase 9 — Quality (2026-09-30)

## Changes

- Replaced obsolete Phase 1 text on Thông tin chung with current workflow and scope; unknown destinations return 404.
- Output errors now show an encoded recovery message and return link, with HTTP status preserved and unexpected exceptions traced server-side.
- Added sticky grid headers, selected-row styling, consistent compact select/disabled controls, and wrapping horizontal menus below 480px. Native CSS only; no added menus, packages or motion.
- Added Phase9WebChecks.cs/run-phase9.ps1 and a combined run-all.ps1. Rewrote README and added DEMO.md.

## Evidence

`tests/run-all.ps1` passed Phase 3–9 service/SQL and hosted ASP.NET request suites, plus database/verify.sql and database/verify-inventory.sql. Logs are in artifacts/quality. These include CRUD/import, atomic stock/commercial posting, concurrent writes, payment bounds/idempotence, ledger balancing, report arithmetic, output/filter isolation and temporary-fixture cleanup.

New Phase 9 checks passed: seven modules/25 approved leaves, every destination returns 200 and contains navigation/refresh, all 11 reports apply filters, invalid output token gives friendly 404, unknown route gives 404, 25-row catalog pagination, encoded stored markup, and rejection of tampered ViewState, cross-session postback and forged selection events. Security checks cover these specific behaviors; authentication/authorization remains outside the implemented demo.

Release MSBuild passed without compiler diagnostics. ASPX precompile passed (exit 0):

```powershell
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase9-precompiled"
```

Independent SQL acceptance still gives HANG001 100 + 50 - 20 = 130 and rejects issue 200 atomically. No new persistent demo rows or migrations were added in Phase 9.

Browser inspection of actual captured ASP.NET inventory HTML confirmed compact horizontal navigation, toolbar, right-aligned values and grid layout. Enter opened the stock menu and Escape closed it. This used a temporary localhost static preview of test output; it did not run ASPX or validate live browser postbacks. Hosted ASP.NET requests tested postbacks separately.

## Remaining validation

Live browser E2E, full responsive/keyboard audit and physical/browser printing remain pending. Follow DEMO.md once a suitable host is available. The current report and voucher PDFs use QuestPDF vector text; the representative ledger remains manual and is not a statutory accounting implementation. See README for full scope limits.

## Files

Created: tests/Phase9WebChecks.cs, tests/run-phase9.ps1, tests/run-all.ps1, docs/DEMO.md, docs/PHASE9.md.

Changed: src/ReportingModule.Web/Default.aspx, Default.aspx.cs, Output.aspx.cs, Content/site.css, Catalog.aspx.cs, Stock.aspx.cs, Commerce.aspx.cs, Journal.aspx.cs; README.md, database/README.md, IMPLEMENTATION_PLAN.md.

AGENTS.md and WALKTHROUGH.md unchanged. Generated test logs/precompiled files/static captures are under ignored artifacts/.
