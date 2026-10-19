# Phase 8: Print, PDF and Excel output

## Workflow

All 11 reports support In/Kết xuất, Excel and PDF. Apply criteria first; the output uses every matching row, not only the current grid page. Typing new filter values without applying them does not change output.

- In/Kết xuất opens a print preview using the shared seven-module shell. The In button opens the browser print dialog. Print CSS hides navigation/controls on paper and repeats table headers.
- PDF downloads a vector A4 landscape document generated with QuestPDF. Vietnamese text is embedded as selectable/searchable text. Reports and designed voucher templates use the captured output snapshot, including effective filters, saved document values, totals and signatures. The PDF does not claim PDF/UA accessibility conformance.
- Excel downloads a real single-sheet .xlsx with title, effective criteria, data timestamp, summary, column headings and all result rows. Dates are Excel dates; ordinary amounts are numeric with 3/4-decimal formats. Values exceeding 15 significant decimal digits are deliberately stored as text to preserve precision. Formula-like names remain literal text. Five metadata/header rows are frozen.
- Stock/commercial/journal document lists enable In for a selected document. Preview and PDF use a designed A4 portrait voucher with saved number/date, unit name/code, partner name/code/address when present, note, detail lines, total and function-specific signature lines. Unsaved editor changes are excluded. Fields absent from the database, such as warehouse/bin or amount-in-words, are not invented.
- Excel has two behaviors: report Excel retains the filtered result grid; selected-document Excel produces a designed, styled voucher sheet with merged header, numbered detail rows, numeric amounts, total, signatures, A4 portrait setup and a defined print area. Both document PDF and Excel use the same saved snapshot and omit internal/training disclaimers.
- The PDF option is a print rendition and uses the same PRINT permission as the print preview. Excel remains a separate EXPORT permission. Output preview hides Excel or PDF actions when the user lacks the matching permission.
- Master-data import/export remains the Phase 3 bounded .xlsx workflow; no new import semantics.

## Result consistency and limits

After a report is rendered, all its result rows and displayed columns are copied into a session-owned snapshot. Print, PDF and Excel read that same copy rather than rerunning SQL. Later database changes therefore do not silently alter the output of an already displayed result. The snapshot records UTC creation time. Return to the source report and use Làm tươi to obtain current DB data; the return link preserves applied filter values for reports.

Each session keeps at most 8 snapshots, each valid for 20 minutes. Expired/evicted snapshots require returning to the report and refreshing. Tokens are random and resolved only within the originating session; another session cannot use a copied token. Responses use no-store cache headers. Existing application authentication/deployment scope is unchanged.

Output is bounded to 5000 rows. Oversized results are refused, never silently truncated. Narrow filters for large output. Snapshot storage uses the existing in-process ASP.NET session, so recycling the application invalidates open outputs.

Empty results still produce headings, criteria and a no-data message in print/PDF; Excel has headings with no data rows. Balance-sheet group styling is retained in print/PDF; its existing parent/account rows remain separate in Excel, and must not be summed together. Report totals are captured from the full result. Business-scope notes for balance, goods amounts and debt are retained where needed.

## Dependencies and files

No packages, fonts, frameworks or external services installed. Added references only to already installed .NET Framework System.Drawing and System.Web.Abstractions. PDF uses Windows GDI+/Arial; Excel uses existing built-in ZIP/XML APIs. The PDF implementation is for this Windows demo environment; hosting/permissions/font availability must be checked on a deployment host.

Created:
- src/ReportingModule.Web/ReportOutput.cs
- src/ReportingModule.Web/ReportExcel.cs
- src/ReportingModule.Web/ReportPdf.cs
- src/ReportingModule.Web/Output.aspx and Output.aspx.cs
- tests/Phase8Checks.cs, Phase8WebChecks.cs, run-phase8.ps1, verify-phase8.py
- docs/PHASE8.md

Updated Stock.aspx.cs, Commerce.aspx.cs, Journal.aspx.cs, ReportingModule.Web.csproj, Content/site.css, README.md and IMPLEMENTATION_PLAN.md. No schema/migration changes. AGENTS.md/WALKTHROUGH.md unchanged.

## Validation

From repository root:

```powershell
& .\tests\run-phase8.ps1
& .\tests\run-phase3.ps1
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase8-precompiled"
```

Use a fresh ASPX precompile destination when repeating. Tests use the existing local Windows-auth SQL demo configuration; isolated Phase 8 documents/payments/journals are removed in finally blocks. The SQL-backed run required approved execution outside the agent sandbox.

Independent file QA uses the already bundled Python with pypdf, pypdfium2, Pillow and openpyxl:

```powershell
& 'C:\Users\DO HUU PHUOC\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tests/verify-phase8.py
```

Those libraries are QA tools, not application dependencies. The script validates PDF structure, renders pages for inspection, opens XLSX independently and reconciles known fixture values. Generated test output stays in ignored artifacts/phase8.

Results:
- Release C# build and ASPX/master precompile passed.
- All 11 reports passed hosted ASP.NET print/XLSX/PDF requests with effective-filter checks and nonempty fixtures across inventory, commercial, debt and balance reports. Initial no-filter output is rejected; invalid/foreign-session tokens are rejected.
- Selected stock receipt, sales invoice and journal print previews passed.
- Snapshot isolation, expiry, 5001-row rejection, all 73 test rows, HTML escaping and literal spreadsheet formula-like text passed.
- Independent XLSX checks confirmed numeric/date types and exact fixture totals: stock 130, revenue 300, purchases 1000, receivables 200, payables 900, assets/sources 100 and difference 0.
- Independent PDF parsing/rendering passed for 13 test PDFs; the 73-row long-text/Vietnamese fixture spans 4 pages. The A4 portrait voucher was rendered and visually checked for Vietnamese text, table, totals and signatures.
- Independent XLSX validation opened the designed voucher workbook, checked merged header, styles, A4 portrait settings, defined print area, partner and total values. No LibreOffice/Excel renderer was installed, so workbook page visuals were not rasterized in this environment.
- Phase 3 catalog service/Excel and Web Forms regression checks passed; temporary test masters removed.

Limit: native browser print-dialog interaction, physical printer output and live browser layout QA remain pending because IIS/IIS Express is unavailable. PDF files were actually rendered and inspected; request tests are not a substitute for browser interaction testing. No new implementation blocker remains.

Stop after Phase 8. Phase 9 quality/E2E/demo work is next.
