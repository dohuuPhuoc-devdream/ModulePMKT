# Phase 7: journal and representative balance sheet

## Scope

ASP.NET Framework 4.8 Web Forms, C#/ADO.NET and SQL Server/T-SQL; no new dependencies. Existing seven-module shell and native dense ERP styling remain. Only the approved Tổng hợp > Phiếu kế toán tổng hợp and Báo cáo tài chính > Bảng cân đối kế toán destinations are implemented here.

This is a teaching ledger and representative balance sheet, not a statutory financial statement or proprietary FAST convention. It uses only manually entered, posted general-journal facts. Commercial invoices, stock quantities and invoice payments are not automatically posted into this ledger. No initial company balances or journal documents are seeded. The screen states these limits.

## Teaching chart and mapping

Migration 006 adds six account definitions and a database mapping, without monetary data:

| Internal account | Label | Group | Displayed balance |
| --- | --- | --- | --- |
| TS_TIEN | Tiền | Assets | Debit minus credit |
| TS_KH | Phải thu | Assets | Debit minus credit |
| TS_HANG | Hàng tồn kho | Assets | Debit minus credit |
| NV_NCC | Phải trả | Liabilities | Credit minus debit |
| NV_VON | Vốn góp | Equity | Credit minus debit |
| NV_LN | Kết quả tích lũy | Equity | Credit minus debit |

These are internal teaching codes, not official account numbers. Mapping lives in TaiKhoanBaoCao, referencing TaiKhoan. Setup is repeatable and refuses to overwrite conflicting names/mappings. There is no extra account menu. Only mapped accounts are available for journal entry; unknown/unmapped account writes are rejected. The report also rejects existing posted facts with missing mappings rather than silently omitting them.

## Journal workflow

Select unit and list date interval, choose Mới, enter number/date/note, then account/debit/credit lines. Thêm dòng and Bỏ dòng cuối manage up to 100 lines; Tính lại recalculates totals. Enter `0` on the unused side of each line. Use a dot for decimals.

- Each nonblank line has exactly one positive side, maximum 24 integer digits and 4 fractional digits.
- Drafts may be unbalanced but line/account/date validation still applies.
- Posting requires exact total Debit = total Credit, including a difference as small as 0.0001.
- Header/detail save and posting are one transaction. Updates/deletes require matching rowversion and DRAFT status.
- Posted journals are immutable in this phase. Reversal/cancellation is not implemented.
- Posting serializes per unit; duplicate numbers cannot be posted twice. Unit opening date is enforced.
- The list supports literal number search, 20-row paging, select/view, draft edit/delete and refresh. Output is disabled until Phase 8.

## Balance report

Apply unit, From and To dates. Opening means cumulative posted ledger balances strictly before From; closing means balances through To inclusive. From cannot precede the unit opening date. With no posted ledger data, mapped rows show zero.

Rows display Assets and its accounts, Liabilities and its accounts, Equity and its accounts, Total sources (Liabilities + Equity), and Assets minus Total sources. Section and account rows are visually differentiated; the small hierarchy is kept together without paging. Do not sum both parent rows and their children.

Opposite-side balances retain their negative sign; they are not clamped or silently reclassified. There is no partner-level gross-up, revenue/expense closing engine, automatic retained-profit calculation, cash-flow report, statutory short/long-term classification or subledger reconciliation in this representative subset. NV_LN records only explicitly entered journal amounts.

Refresh retains effective dates. Report reads are protected by the shared unit lock while posting uses the same unit's exclusive lock. All monetary values come from SQL ledger facts and all filter values are parameterized.

## Validation

From the workspace root in PowerShell:

```powershell
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
& .\tests\run-phase7.ps1
sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify-inventory.sql
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase7-precompiled"
```

Use a fresh precompile destination on repeat runs. SQL Windows authentication required approved execution outside the agent sandbox. Tests create temporary isolated units/journals and remove them in finally blocks.

Results:
- Release C# build and ASPX/master precompile passed without compiler diagnostics.
- Setup applied twice successfully, schema version 5; no balances/documents seeded or overwritten.
- Service tests passed: zero ledger, unbalanced drafts excluded, failed posting preserves drafts/no partial new header, exact 0.0001 rejection, stale edits/deletes, line/precision/date/account validation, duplicate/posted/cross-unit rejection, parameterized search, concurrent duplicate posting.
- Balance checks passed: assets 130.1234 = liabilities 30 + equity 100.1234; opening/closing/inclusive dates, independent SQL reconciliation and signed opposite-side balances.
- Actual ASP.NET request checks passed: list, add detail row, imbalance error, draft create/edit/delete, balanced posting, view-only posted entry, hierarchical report, refresh of applied dates, opening balances and invalid route 404.
- Inventory remains independent: HANG001 still 100 + 50 - 20 = 130, failed issue absent; SQL constraint verification passed.

Visual browser/keyboard QA remains pending because IIS/IIS Express and Visual Studio WebApplication targets are unavailable. The test harness exercises ASP.NET requests/postbacks, not visual browser rendering. No additional Phase 7 blocker remains.

## Files

Created Journal.aspx/.cs, JournalStore.cs, database/006-ledger.sql, tests/Phase7Checks.cs, tests/Phase7WebChecks.cs, tests/run-phase7.ps1 and this document. Updated navigation, project entries, two CSS hierarchy rules, setup/version guards, README/database README and IMPLEMENTATION_PLAN.md. AGENTS.md/WALKTHROUGH.md are unchanged.

Stop after Phase 7. Phase 8 output has not started.
