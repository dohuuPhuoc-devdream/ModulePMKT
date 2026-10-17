# IMPLEMENTATION_PLAN.md — V3 FINAL
Status: PHASE 10 IMPLEMENTED; build, precompile, focused security checks and Phase 3–9 regressions pass; browser acceptance is partial; public deployment is NOT approved
Architecture: LOCKED

## Locked
- [x] Web, ASP.NET Framework Web Forms, C#/ASPX/HTML/CSS/JS
- [x] SQL Server/T-SQL
- [x] Horizontal persistent navigation; no sidebar
- [x] Modules: Hệ thống, Tổng hợp, Phải thu, Phải trả, Bán hàng, Mua hàng, Tồn kho
- [x] No standalone Reports menu
- [x] Context-sensitive toolbars; Làm tươi everywhere
- [x] Master Excel import/export
- [x] Reports Print/PDF/Excel
- [x] Output choices are specific to each function; Excel/PDF are the standard choices, and bilingual templates are included only where that business function needs them.
- [x] Seed data + manual entry
- [x] Taste Skill restrained only

## Phase 1 — Bootstrap/shell
Target: .NET Framework 4.8, installed reference assemblies and System.Web available.
Use native Web Forms/CSS/JavaScript, no external dependencies. Current workspace contains only nine documentation/skill files; no prior application or database.
MSBuild 2022 and aspnet_compiler are installed. WebApplication targets and IIS Express have not been found; verify build and record hosting limitations honestly.
- [x] Inspect installed .NET Framework/Web Forms tooling
- [x] Create solution/project with sensible installed compatible Framework (.NET Framework 4.8)
- [x] Configure empty connectionStrings section; no credentials, connection or DB use
- [x] Shared header/master layout
- [x] Seven-module horizontal navigation + nested dropdowns
- [x] Workspace + compact toolbar styles
- [x] Release C# build and ASP.NET page/master precompilation
- [ ] Browser/run smoke test: no IIS Express executable, W3SVC service or WebApplication targets found in inspected installed locations

### Phase 1 delivery (2026-09-30)
- Created ReportingModule.sln, .gitignore, README.md and src/ReportingModule.Web/ with ReportingModule.Web.csproj, Web.config, Navigation.cs, Site.Master/.cs, Default.aspx/.cs, Content/site.css and Scripts/site.js.
- Modified IMPLEMENTATION_PLAN.md only among governing documents. AGENTS.md and WALKTHROUGH.md unchanged.
- Dependencies: installed .NET Framework 4.8/System.Web, System, System.Core only. No NuGet/npm packages, frameworks, fonts or libraries installed.
- Shared Master Page renders the persistent header and exactly seven horizontal modules. The navigation model contains 25 leaf destinations matching WALKTHROUGH; report groups expand in nested dropdowns. Native details/summary supports keyboard and touch; JavaScript closes sibling menus/outside clicks and handles Escape.
- Screen IDs are resolved against the navigation allowlist; unknown IDs return 404. Text is HTML-encoded. Contextual actions are disabled and visibly explained until their implementation phase. Refresh performs a GET for the current valid screen and reports only that the placeholder reloaded; it does not claim a DB refresh.
- Design read: dense ERP workspace using native controls/CSS, restrained 3/1/7 guidance. No sidebar, dashboard cards, invented modules, business data or report values.
- Build command (PowerShell): & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo
- Build result: succeeded, 0 warnings, 0 errors. Missing web targets are explicitly reported as a message; standard C# compilation remains available through Microsoft.CSharp.targets. The optional WebApplication import activates when installed; no proprietary or fabricated targets were added.
- Markup validation: & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p 'D:\Module đề xuất báo cáo PMKT\src\ReportingModule.Web' 'D:\Module đề xuất báo cáo PMKT\artifacts\precompiled'
- Markup result: succeeded without diagnostics, validating ASPX/Master compilation in addition to the C# build. Re-run with a fresh output directory. Generated bin/obj/artifacts are ignored.
- Navigation validation: loaded the compiled assembly and verified exact seven module labels/order, 25 unique leaf IDs, every leaf resolves, and unknown IDs are rejected. Passed.
- Real limitation: browser rendering, keyboard/touch interaction and HTTP refresh/404 behavior have not been executed in a running web server. Local IIS Express and WebApplication targets were not found; no W3SVC service was returned. Visual Studio web-project loading/hosting needs the ASP.NET web tooling. No tooling installed without approval. This is an environment limitation, not an architecture decision.
- Next authorized work: none. Stop after Phase 1. Planned Phase 2 is SQL schema/seed, requiring a separate user instruction. Resolve hosting to complete the outstanding browser smoke test.

## Phase 2 — Database
Authorized by user: next phase. Scope: versioned schema, repeatable synthetic seed and SQL verification only.
Proposed local demo database: ReportingModuleDemo on .\SQLEXPRESS; Windows authentication, no stored password. Initial sandbox connection failed in the client SSL/security package; retry outside sandbox requested.
Design: separate master data, opening balances per unit/item, warehouse movements, commercial documents and general-ledger headers/details. Stock facts originate only in warehouse movements; commercial links must not double-count stock. Posted-document validation/stock concurrency belongs to Phase 4 and will not be claimed complete here.
- [x] Normalized schema, PK/FK/checks/indexes
- [x] Re-runnable setup
- [x] Seed data including HANG001 opening 100

### Phase 2 delivery
- Files created: database/setup.sql, 001-schema.sql, 002-seed.sql, verify.sql, README.md. Files updated: root README.md, IMPLEMENTATION_PLAN.md. Application code and V3 requirements unchanged.
- Created local ReportingModuleDemo on .\SQLEXPRESS (SQL Server Express 16.0.1200.5) using Windows authentication. No external data or new dependencies; no credentials committed.
- 12 business tables plus SchemaVersion/SeedHistory. Masters, per-unit opening stock, commercial headers/details, warehouse headers/details, and minimal journal/accounts structure. FKs do not cascade; details have numbered composite keys; monetary line amounts are computed, document totals derived from details.
- Numeric defaults and status/source-link semantics recorded in database/README.md. No proprietary FAST schema claimed. No payment/tax/report implementation added.
- Seed: DVDEMO, 10 items, 5 customers, 5 suppliers; HANG001 opening 100 at 2026-01-01, all other openings zero. No posted documents seeded and no report output hard-coded.
- Setup: sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/setup.sql
- Verification: sqlcmd -S '.\SQLEXPRESS' -E -C -b -l 10 -f 65001 -i database/verify.sql
- Initial schema execution failed because sqlcmd QUOTED_IDENTIFIER was off; transaction rolled back. Added explicit required SET options; corrected setup succeeded. A second complete setup succeeded without duplicate seed records.
- Verification passed: schema/seed markers, seed baseline, HANG001 100, trusted constraints, exact fractional line amount/derived total and zero price, plus 12 negative cases covering duplicates, blank name, prices, opening balance, FK, referenced deletion, wrong commercial partner, status, quantity and journal line shape. All fixtures rolled back; DBCC CHECKCONSTRAINTS returned no violations.
- Scope limit: nonempty posting, stock check/write concurrency, header/detail application transactions, journal balance and source-document semantic validation remain future business-layer work. Acceptance 130/reject-200 is NOT complete. Web application still does not connect to the DB.
- Phase 2 blocker: none after approved local SQL access. Existing Phase 1 hosting limitation remains. Stop before Phase 3; next phase is master-data CRUD/search/refresh and the approved import/export work.

## Phase 3 — Master data
Authorized by user: next phase. Implement four catalogs using an allowlisted shared ADO.NET service and Web Forms screen, preserving the existing shell.
Defaults: immutable codes on edit, server validation, optimistic conflict detection, FK-safe deletion; literal code/name search; export all matching rows, not only the current page. Excel import inserts only and rejects duplicates/invalid rows atomically. Native .NET ZIP/XML will handle a bounded single-sheet .xlsx format without installing libraries; no .xls/macros/formulas support. Document file limits and template explicitly.
- [x] Hàng hóa, Khách hàng, Nhà cung cấp, minimal Đơn vị
- [x] CRUD/search/refresh
- [x] Excel import/export
- [x] Server validation/FK-safe delete

### Phase 3 delivery
- Added Catalog.aspx/.cs, CatalogDefinition.cs, CatalogStore.cs, CatalogExcel.cs; four allowlisted catalogs share ADO.NET operations and a server-rendered Web Forms grid/editor/import panel.
- Updated project references/content, Web.config (local integrated SQL configuration, upload limit and cookie settings), Site.Master/.cs, Default.aspx.cs and CSS. The seven approved modules are unchanged; catalogs now route to functional screens. AGENTS.md/WALKTHROUGH.md and database schema unchanged.
- Added tests/Phase3Checks.cs, tests/Phase3WebChecks.cs, tests/run-phase3.ps1 and docs/PHASE3.md; updated README.md and this plan.
- Dependencies: built-in System.Data, System.Configuration, System.Xml, System.Xml.Linq and System.IO.Compression only. No new packages or tools installed. Excel support is a documented bounded .xlsx exchange format, with safe text cells, numeric/date cells and strict structure/size checks.
- Server rules: parameterized values, code/name literal search, 20-row paging, immutable codes, explicit lengths/decimal/date validation, original-value optimistic update/delete protection, FK-safe deletion. Existing unit opening dates cannot change after balances/documents exist.
- Import defaults finalized: insert-only, reject duplicates, no overwrite, whole-batch transaction rollback on any invalid/conflicting row. Export uses all filtered rows, maximum 5000. Templates contain exact stable column headers.
- Build: MSBuild Release succeeded with 0 warnings and 0 errors; ASP.NET compiler succeeded to artifacts/phase3-final-precompiled. Existing missing WebApplication targets warning message/hosting limitation remains.
- Service/SQL tests passed for all four catalogs: create/read/update/delete, literal and injection-like search, duplicate/required/precision checks, stale updates/deletes, atomic import rollback, referenced deletion and protected unit date. Temporary test rows removed; no document/stock data changed.
- Excel tests passed: four catalog round-trips, numeric/date types, templates, duplicate/invalid-row rejection, formula and oversized-file rejection. Existing bundled openpyxl independently read the generated files and supplied an import fixture. It is not an application dependency.
- Hosted ASP.NET tests passed: real page render reading SQL, seven-module shell, encrypted viewstate, filter/refresh postbacks, new/invalid/cancel forms, import-panel open, selection, save/edit/delete postbacks with redirect and DB assertions, all four catalog pages, invalid route 404. Fixed a ViewStateEncryptionMode lifecycle error discovered by these requests; set it in the page directive.
- Reproduction: tests/run-phase3.ps1 builds and runs both test suites (Windows/SQL demo access required). Sandbox SQL Windows authentication failed; approved execution outside sandbox succeeded. No secrets committed.
- Validation limit: request harness is not a visual browser test. IIS/IIS Express browser checks, keyboard interaction and viewport review remain pending. ApplicationHost is used only by tests, not as an invented deployment host. No public deployment/authentication setup performed.
- Stop after Phase 3. Next is Phase 4 inventory vertical slice; 100+50-20=130/reject-200 acceptance is still pending, not claimed complete.

## Phase 4 — Inventory vertical slice
Authorized: next phase. Implement warehouse receipt/issue drafts, atomic posting and three real SQL-backed inventory reports only. Posted documents are immutable in this phase; edit/delete applies only to drafts. No reversal/cancellation of posted stock until a coherent later workflow is implemented.
Posting will serialize per unit, aggregate repeated item lines and verify historical running balances (including later movements) so backdated issues cannot cause negative stock. Reports include posted warehouse facts only; period opening includes earlier movements. Output controls remain disabled until Phase 8.
- [x] Phiếu nhập kho + details
- [x] Phiếu xuất kho + details
- [x] Transactions/stock validation
- [x] Báo cáo hàng nhập/xuất/N-X-T
- [x] 100+50-20=130
- [x] issue 200 rejected

### Phase 4 delivery
- Added Stock.aspx/.cs, StockStore.cs, database/003-inventory.sql, database/verify-inventory.sql, tests/Phase4Checks.cs, tests/Phase4WebChecks.cs, tests/run-phase4.ps1, docs/PHASE4.md. Updated navigation routing, project entries, CSS, setup/version compatibility, README/database README and this plan. Corrected UTF-8 response handling in the Phase 3 test harness. No architecture changes or new dependencies.
- Database migration version 2 applied/reapplied without resets. StockLines TVP and SaveStockDocument/DeleteStockDraft/InventoryReport procedures implement parameterized SQL, transactional header/detail writes, immutable posted documents, rowversion checks and unit-scoped locking.
- Three report grids use posted data, effective unit/date/item filters, inclusive date boundaries, pre-period opening calculation and all-result totals. Refresh retains effective criteria; report grids do not load until criteria are applied. Output remains disabled until Phase 8.
- SQL stock validation checks every historical prefix for affected items, aggregates repeated item lines, and rejects backdates that would make later balances negative. Explicit decimal aggregate casts preserve fractional stock precision.
- Service/SQL tests passed: lifecycle/edit/delete/rowversion, rollback retaining drafts, 130/reject-200, missing/invalid details and references, repeated items, duplicate numbers, posted immutability, date boundaries, empty ranges, independent SQL reconciliation, backdating, concurrent issues and fractional 0.001 rejection. Isolated test unit/item/documents removed.
- Hosted Web Forms acceptance passed: PN001 +50 and PX001 -20 created via postbacks for DVDEMO/HANG001; N-X-T grid/SQL closing 130; issue 200 returned an insufficient-stock error with no header or stock change. Multiline draft create/reload/edit/delete also passed. Three report screens, refresh and period opening 150 on 2026-01-03 verified.
- PN001/PX001 are intentionally retained as synthetic demo documents; reruns validate them instead of overwriting. No REJECT200 header or orphan details. Independent database/verify-inventory.sql reconciled 100/50/20/130; DBCC CHECKCONSTRAINTS reported no violations.
- Phase 3 service/Excel and hosted page regression suite passed after route changes; temporary test masters removed. A harness encoding issue was fixed (SimpleWorkerRequest output bytes decoded as UTF-8) to validate Vietnamese error text correctly.
- Release build and ASP.NET compilation passed; final commands/results recorded in docs/PHASE4.md. No IIS/IIS Express installation; visual browser testing remains pending, while server-request E2E is now verified. No claim that R04 revenue or other later-phase reports are complete.
- Stop after Phase 4. Next is Phase 5 sales/purchase subset and reports, requiring the next instruction.

## Phase 5 — Sales/Purchase
Authorized: next phase. Reuse commercial header/detail tables. Sales invoice aliases share SALES_INVOICE. Purchase receipt is an inventory receipt with supplier; purchase invoice is a payable document linked to a posted receipt and does not create a second stock movement. Sales invoices create one linked issue. Orders never affect stock/revenue. Posted documents are immutable. Standalone manual orders/invoices only; order conversion/fulfillment, taxes, discounts and payments are outside this subset. Revenue by partner; analysis by month/item. No new dependencies.
- [x] Đơn hàng bán; Hóa đơn bán hàng
- [x] Đơn hàng mua; Phiếu nhập mua
- [x] Chi tiết bán hàng; Doanh thu; Phân tích doanh thu
- [x] Chi tiết mua hàng; Phân tích mua hàng

### Phase 5 delivery (2026-09-30)
- Framework remains .NET Framework 4.8 / Web Forms, SQL Server / ADO.NET; no new dependencies.
- Created Commerce.aspx/.cs, CommerceStore.cs, database/004-commerce.sql, tests/Phase5Checks.cs, tests/Phase5WebChecks.cs, tests/run-phase5.ps1 and docs/PHASE5.md.
- Updated Navigation.cs and ReportingModule.Web.csproj to route the 11 approved sales/purchase destinations; no menus added. Updated StockStore.cs to share line validation. Updated database/003-inventory.sql with optional output ID/result suppression for nested atomic commercial posting, database/001-schema.sql version guard and database/setup.sql migration inclusion. Updated README.md, database/README.md and this plan. AGENTS.md/WALKTHROUGH.md unchanged.
- Sales invoices have one linked warehouse issue enforced by the filtered unique index. Purchase receipts affect stock once; purchase invoices link to a same-unit/same-supplier posted receipt and affect payables without stock movement. Per-unit transaction locking and historical stock checks protect posting. Posted invoices cannot be reposted/edited/deleted; draft updates/deletes require matching rowversion.
- Sales aliases use shared records. Purchase receipts and invoices are separate records. Orders are manual standalone documents and excluded from stock/revenue; order conversion/fulfillment is deferred. No taxes, discounts, payment, general-ledger posting or report output implemented at this phase. These are subset limits, not architecture questions.
- Five real SQL reports implemented: sales/purchase detail, customer revenue and month/item sales/purchase analysis. Applied unit/date/partner filters survive refresh; totals cover all results. Dense reusable existing CSS, no sidebar/cards/motion additions. Print/output remains disabled until Phase 8.
- Build command: & 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal. Result: passed, no compiler diagnostics.
- ASPX validation: & 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\aspnet_compiler.exe' -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase5-precompiled". Result: exit 0.
- SQL setup ran twice successfully; schema version 3. tests/run-phase5.ps1 passed service/SQL and actual Web Forms request tests, including atomic oversell rollback, concurrent invoices, optimistic conflicts, aliases, all five reports and refresh. Temporary fixture rows removed.
- tests/run-phase4.ps1 and database/verify-inventory.sql passed regression/independent acceptance. HANG001 still closes at 130; no rejected-200 header or orphan details; DBCC constraints passed.
- Real limitation remains: IIS/IIS Express/WebApplication targets missing, so visual browser smoke test is pending. ASPX precompile and hosted request tests passed but are not visual QA. No new Phase 5 blockers.
- Stop after Phase 5. Next phase is receivable/payable reports with explicitly documented minimal settlement facts if needed; no Phase 6 implementation started.
## Phase 6 — Receivable/Payable
- [x] Công nợ khách hàng
- [x] Công nợ nhà cung cấp
- [x] Minimal invoice-level payment facts and UI; semantics documented in docs/PHASE6.md

### Phase 6 delivery
- Authorized by next phase. Framework remains .NET Framework 4.8 Web Forms; no new dependencies. Existing dense ERP styling reused, no new menus or architecture changes.
- Added database/005-debt.sql (schema version 4) with ThanhToanHoaDon, RecordInvoicePayment and DebtReport. Updated setup and earlier migration version guards. No seed payments or opening company balances added.
- Added DebtStore.cs and Commerce.Payments.cs; updated CommerceStore.cs, Commerce.aspx.cs, Navigation.cs and project includes. Existing invoice view hosts partial receipt/payment entry and immutable history; only posted invoices can be settled.
- Debt grids compute opening + posted invoices in period - payments in period = closing, per partner. Unit/date/partner filters are parameterized, inclusive and retained by refresh; totals span all pages. Drafts/orders excluded. Payment facts aggregated separately from invoice lines to avoid multiplying totals.
- Payment validation: positive decimal(28,4), date >= invoice date, matching unit/type, unique reference per invoice, no overpayment. Per-unit transaction locking prevents concurrent overpayment; request token replay is idempotent. Payments do not affect stock or revenue.
- Limits clearly documented/displayed: amounts cover recorded goods invoices without imported initial debt, advances, tax/discounts, multi-invoice allocation, payment reversal or general-ledger posting. Print/output remains Phase 8. These are subset limits, not blockers requiring architecture decisions.
- Added tests/Phase6Checks.cs, tests/Phase6WebChecks.cs, tests/run-phase6.ps1 and docs/PHASE6.md; updated README.md/database README and this plan. AGENTS.md/WALKTHROUGH.md unchanged.
- Build: tests/run-phase6.ps1 invokes MSBuild ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal; passed without compiler diagnostics. ASP.NET compiler -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase6-precompiled" returned exit 0.
- SQL setup applied/reapplied successfully. Service tests passed for AR/AP period arithmetic and precision, partial/full settlement, invalid/duplicate/cross-unit requests, same-request replay, changed-payload replay, concurrency and independent payable SQL reconciliation.
- Actual Web Forms request tests passed for payment entry/history/error and both report grids/filter refresh/opening, plus existing Phase 5 document/report postbacks. Fixtures removed. database/verify-inventory.sql passed; HANG001 remains 130; DBCC constraints passed.
- Real limitation remains visual browser QA: IIS/IIS Express/WebApplication targets are missing. Server rendering/postback and ASPX compilation passed; no new blocker in this phase.
- Stop after Phase 6. Next is Phase 7 minimal general accounting and representative balance sheet; no Phase 7 implementation yet.
## Phase 7 — General/Financial
- [x] Phiếu kế toán tổng hợp minimal
- [x] Bảng cân đối kế toán representative report

### Phase 7 delivery
- Authorized by next phase. .NET Framework 4.8 / native Web Forms / SQL Server retained; no dependencies installed. Scope is a manual teaching ledger and representative balance report, not a statutory statement or automatic posting integration.
- Created Journal.aspx/.cs, JournalStore.cs, database/006-ledger.sql, tests/Phase7Checks.cs, tests/Phase7WebChecks.cs, tests/run-phase7.ps1 and docs/PHASE7.md.
- Updated Navigation.cs, ReportingModule.Web.csproj, Content/site.css (group/account rows only), database/setup.sql, 001-schema.sql/003-inventory.sql version guards, README.md/database README and this plan. AGENTS.md/WALKTHROUGH.md unchanged; no added menu destinations.
- Migration version 5 adds six internal teaching account definitions, TaiKhoanBaoCao mapping and JournalLines/SaveJournal/DeleteJournal/BalanceReport. No company balances or journals seeded. Rerun refuses conflicting mapping/name overwrites.
- Server validates one positive Debit/Credit side per line, explicit decimal(28,4), accounts, date and number. Drafts can be unbalanced; posting requires exact total Debit = Credit. Header/details are atomic, rowversion protects edits/deletes and posted journals are immutable. Per-unit locks coordinate writes/reports.
- Balance hierarchy: assets/account rows, liabilities/account rows, equity/account rows, sources total and difference. Opening is posted balance before From, closing through To inclusive. Negative balances retain signs. Applied filters survive refresh; no double-counted hierarchy totals. Unmapped posted accounts fail explicitly.
- Scope shown in UI/docs: manual general journals only, no imported opening debt, automatic commercial/stock/payment posting, statutory chart/layout, income closing, reversals or subledger reconciliation. Print/PDF/Excel stays Phase 8.
- Build: tests/run-phase7.ps1 invokes MSBuild ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal; passed without compiler diagnostics. ASP.NET compiler -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase7-precompiled" returned exit 0.
- Migration setup applied/reapplied successfully. Service tests passed atomic draft/post lifecycle, 0.0001 imbalance rejection, validation/conflicts/concurrent duplicate posting and SQL reconciliation. Temporary journal/unit fixtures removed.
- Hosted Web Forms tests passed draft add/edit/delete, balance validation errors, posting, immutable view, hierarchical report, effective-filter refresh and period opening. Assets 130.1234 reconciled with liabilities 30 + equity 100.1234 in isolated test data.
- database/verify-inventory.sql passed: HANG001 remains 130; no rejected-issue header/orphan data; DBCC constraints passed.
- Existing tooling limitation remains visual browser QA (IIS/IIS Express/WebApplication targets absent). Server request tests and ASPX precompile passed. No new blocker.
- Stop after Phase 7. Next is Phase 8 Print/PDF/Excel matching current effective filters; no Phase 8 implementation started.
## Phase 8 — Output
- [x] Print view
- [x] PDF
- [x] Report Excel
- [x] Master Excel import/export
- [x] Output matches filters

### Phase 8 delivery
- Authorized by next phase. All 11 report screens now support Print/PDF/Excel; selected stock/commercial/journal documents have internal print previews. No new menus or database changes.
- Created ReportOutput.cs, ReportExcel.cs, ReportPdf.cs, Output.aspx/.cs, tests/Phase8Checks.cs, Phase8WebChecks.cs, run-phase8.ps1, verify-phase8.py and docs/PHASE8.md. Updated three source page code-behinds, project references/includes, print CSS, README.md and this plan. AGENTS.md/WALKTHROUGH.md unchanged.
- Uses session-owned copies of all result rows plus effective criteria, columns, summary and timestamp. Unapplied edits and subsequent DB changes do not change a displayed result's output. All filtered rows are included, not just the current grid page. Random tokens are session-scoped, 20-minute TTL, at most 8 snapshots; output rejects >5000 rows without truncation.
- HTML preview keeps persistent navigation; print CSS hides shell/actions on paper. PDF is a real A4 landscape image-based document using installed .NET System.Drawing/Windows Arial, with wrapped cells, repeated headers and numbered pages; text is not searchable/selectable. PDF limit 200 pages. XLSX uses native ZIP/XML, real numeric/date cells, safe literal strings and precise text fallback beyond Excel's 15 significant digits. No packages/fonts installed; System.Drawing/System.Web.Abstractions references recorded.
- Master .xlsx import/export retained and regression-tested. Report business-scope notes carried into output. Selected-document output uses saved DB values, excluding unsaved editor changes.
- Build: tests/run-phase8.ps1 invokes Release MSBuild; passed without compiler diagnostics after removing unused test fields. ASP.NET compiler -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase8-precompiled" passed, exit 0.
- Hosted request checks passed all 11 report output formats, effective filters, mandatory criteria, session isolation/invalid token, and selected receipt/invoice/journal printing. Isolated fixture data removed. Snapshot expiry/copy isolation/size rejection and 73-row escaping/formula-safety checks passed.
- Independent bundled pypdf/openpyxl/pypdfium2 QA passed; 12 PDFs parsed/rendered, including a 4-page Vietnamese long-text fixture. Rendered output visually inspected for wrapping, glyphs, hierarchy, margins and pagination. XLSX numeric/date types and fixture totals independently reconciled (inventory 130, revenue 300, purchase 1000, debt 200/900, balance 100/100 with zero difference).
- tests/run-phase3.ps1 passed catalog CRUD/Excel and Web Forms regression checks. No persistent demo/company data added.
- Remaining environment limit: browser print dialog/physical printing and live browser interaction QA remain pending without IIS/IIS Express. PDF rendering was verified independently. No new blocker.
- Stop after Phase 8. Next is Phase 9 end-to-end quality, restrained polish and demo/readme verification.
## Phase 9 — Quality
- [x] Hosted ASP.NET E2E/security/transaction tests (live IIS browser E2E pending)
- [x] Empty/error states, pagination/sticky headers
- [x] Taste polish 3/1/7 (captured HTML inspection; full responsive audit pending)
- [x] README/setup/demo

### Phase 9 delivery (2026-09-30)
- Completed automated quality pass, restrained native CSS polish, current general-information page, friendly output-error recovery, and setup/demo documentation. No new dependencies, schema changes or menus; .NET Framework 4.8 remains the target.
- Created tests/Phase9WebChecks.cs, tests/run-phase9.ps1, tests/run-all.ps1, docs/DEMO.md and docs/PHASE9.md. Updated Default.aspx/.cs, Output.aspx.cs, Content/site.css and Catalog/Stock/Commerce/Journal.aspx.cs selected-row styles, README.md, database/README.md and this plan. AGENTS.md/WALKTHROUGH.md unchanged.
- tests/run-all.ps1 passed Phase 3–9 suites and both independent SQL verification scripts. Logs: artifacts/quality. Release MSBuild passed without compiler diagnostics; aspnet_compiler -v / -p "$PWD\src\ReportingModule.Web" "$PWD\artifacts\phase9-precompiled" passed (exit 0).
- New checks verify exactly seven modules/25 leaves, route availability/refresh, all 11 report filters, pagination, stored-markup encoding, tampered/cross-session ViewState and forged-event rejection, friendly output errors. Fixtures removed. HANG001 remains 130; issue 200 rejected atomically.
- Actual ASP.NET-generated inventory HTML inspected in a temporary static browser preview; compact shell/grid and Enter/Escape menu behavior checked. This is not live browser E2E. IIS/IIS Express/WebApplication targets remain missing; live interaction, full responsive audit and printing remain pending. No host/dependencies installed.
- Current scope/security/output limits and manual demo steps recorded in README and docs/DEMO.md. No new persistent demo facts added.
- Stop after Phase 9. No further implementation phase is defined; remaining work is host setup and manual acceptance when authorized.

## Hosting and manual acceptance — follow-up (2026-09-30)
- User requested the next phase after Phase 9. Remaining scope is running the existing application on a supported local Web Forms host and performing browser acceptance; no additional business module is planned.
- Rechecked before install: IIS Express was missing; Visual Studio Installer was present. It has since been installed successfully as documented below.
- Proposed environment change: add IIS Express and ASP.NET Framework web-project tooling to the existing Visual Studio 2022 installation. Keep .NET Framework 4.8 and the current project/database. No application framework or business dependency change.
- User explicitly approved installation. Visual Studio Installer modify completed with exit code 0, adding Microsoft.VisualStudio.ComponentGroup.Web and Microsoft.VisualStudio.Component.IISExpress with their required Microsoft tooling dependencies. No application packages, stack changes or database migrations. No restart requested.
- Installer approved and completed below. Build and localhost smoke checks passed. Full docs/DEMO.md browser/output acceptance and the requested hostname configuration remain to be completed after the user specifies the domain scope/name.
- This follow-up changed only IMPLEMENTATION_PLAN.md; no source, database or dependencies changed. Previous Phase 9 test results remain recorded; no unnecessary rerun performed for this documentation-only update.

### Hosting installation result
- IIS Express installed at C:\Program Files\IIS Express\iisexpress.exe; official WebApplication targets now present under Visual Studio 2022 Community. Release MSBuild passed using the installed web targets.
- First non-elevated installer call returned 5007; elevated retry via Windows UAC succeeded, exit 0. Installer log: %TEMP%\dd_setup_20260930133521.log.
- IIS Express launched as the current Windows user, http://localhost:8080/, PID recorded in artifacts/hosting/iis.pid. Home and SQL-backed item catalog returned HTTP 200; HANG001 was loaded from the database. Real HTTP report postback with DVDEMO/HANG001 and year 2026 returned HTTP 200 and closing 130, with no data writes.
- Browser opened the live general-information and inventory-filter pages. Full browser interaction/print acceptance remains pending; no claim of complete manual E2E.
- Added tools/run-local.ps1 (build + localhost IIS Express, optional Port); parser validation passed. Updated README and this plan. User can keep the server running to use the application. No firewall/public binding added.

### IIS Express access incident (2026-10-02)
- User reported that localhost no longer opened and displayed an HTTP 500/IIS error page. The browser later showed connection refused. IIS Express event ID 2299 reported unhealthy application worker/recycle but did not include the underlying ASP.NET exception; no matching detailed ASP.NET event was present in the inspected Application log.
- Restarted IIS Express on localhost:8080. Current home route returns HTTP 200. SQL-backed item page returns HTTP 200 and HANG001. Inventory filter postback returns HTTP 200 with closing 130.
- IIS Express runs as a foreground process despite its hidden window; it must be restarted after process/Windows shutdown. `tools/run-local.ps1` builds and starts it when needed. No always-on Windows service or public hostname is configured.
- A permanent local hostname needs the exact name the user wants (e.g. `ketoan.test`) and remains machine-only. A public Internet domain requires a registered domain and public hosting/network/security setup; user has not specified the domain or asked to expose the demo externally. No DNS, hosts file, firewall or external access was changed.

### Public Internet readiness review (2026-10-02)
- The user now requests Internet access under a PicoCorp-like domain. Read-only review found the current Web.config is local-only: SQL Express with Windows Integrated Security and TrustServerCertificate=True; debug compilation is enabled; the web app has no login/authentication or authorization. The app supports creating, editing, posting and deleting business-demo records, so a public anonymous deployment would let visitors change the shared database and must not be enabled as-is.
- `picocorp.com` is already registered and resolves to an unrelated pipeline inspection company; do not configure or use it. The official VNNIC lookup portal can check .vn registrations, but no candidate registration was made. A different extension or variant must be checked live and registered by its owner.
- Local IIS Express is not a permanent Internet host. Public deployment needs a Windows Web Forms-capable host, a hosted SQL Server with deployment credentials/backup, DNS control, HTTPS and authentication. This may incur a domain registration and recurring hosting charge. The database currently lives on local SQL Express; do not open TCP 1433 to the Internet.
- Before deployment: decide which users may sign in, add server-side login and access protections, configure production errors/cookies/TLS, move database credentials to protected hosting configuration, deploy to a Windows host/SQL service, test there, then point the chosen domain and enforce HTTPS. Existing public deployment authorization does not cover a specific purchase, domain registration, or exposing the unauthenticated demo; ask immediately before any such external access or paid transaction.
- No production DNS, firewall, public binding, paid service or data transfer configured during this review.
### Free domain provider selected (2026-10-03)
- Recommended free dynamic-DNS subdomain: `picocorp.duckdns.org` if available. DuckDNS officially supports one subdomain per account plus HTTPS IP-update API; user must authenticate with Google/GitHub/Twitter/persona. `.com` is already owned by another organization.
- Opened DuckDNS official home page in the browser. Did not select an external identity provider, create account, register a subdomain, or view/store a DNS token. User must sign in personally and reserve an available PicoCorp subdomain; do not send the account token in chat.
- After reservation, local updater can be configured to read a token from a protected local secret store. Internet routing still requires router ISP/public-IP feasibility. Public hosting must wait for authentication in the app, production configuration, and user confirmation immediately before enabling public inbound access.
## Defaults
Inspect environment and choose a compatible installed .NET Framework version; do not ask architecture again.
Use decimal with explicit SQL precision for money/quantity.
Use parameterized ADO.NET or existing repo convention; do not introduce ORM unless already present/approved.
Record dependencies before adding.

Acceptance: HANG001 100 + 50 - 20 = 130; issue 200 rejected atomically.

## Phase 10 — Completion pass: identity, RBAC, audit, workflow integrity, UX and public-readiness
Status: IMPLEMENTED LOCALLY — public deployment gate remains closed pending manual acceptance and operations checks
Priority: P0 items are mandatory before Internet exposure. P1 items complete the internship/demo system coherently. P2 items are explicitly deferred unless later requested.

### Phase 10 review basis
Phase 1–9 already provide the locked Web Forms architecture, four master catalogs, inventory, sales/purchase, invoice-level debt/payment facts, manual general journal, representative balance report, Print/PDF/Excel, tests and the local IIS Express host. Preserve those working features.

The review identified these remaining gaps:
1. No authentication or user administration.
2. No configurable authorization matrix; a simple hard-coded Admin/User split is insufficient for an accounting system.
3. No admin UI for users/roles/permissions/audit.
4. The shell has no authenticated user/unit identity and limited active-navigation/breadcrumb feedback.
5. Public-production security configuration is not complete.
6. Real browser/manual acceptance is incomplete.
7. Existing business scope deliberately omits tax/discounts, automatic GL posting, order conversion, payment reversal, statutory accounting and posted-document cancellation. These omissions must be handled deliberately rather than silently expanded.
8. Posted records are immutable, which protects history, but operators need a clear correction policy. For this internship system, Phase 10 will document and expose a safe status/reversal policy only where it can be implemented without corrupting stock/debt; it will NOT add a full enterprise accounting reversal engine.
9. The application needs clearer auditability for privileged mutations before multiple Internet users are allowed.

### Scope rule
Do NOT redesign the seven-module architecture and do NOT turn this into a full ERP. Phase 10 completes security, administration, auditability and consistency of the existing approved subset.

---

### 10.1 Identity and authentication — P0
- [ ] Add `Login.aspx` outside the authenticated ERP master shell.
- [ ] Add logout and authenticated identity to the shared header.
- [ ] Use native ASP.NET Framework Forms Authentication (or an equally native framework mechanism) with server-side checks.
- [ ] Store passwords only as salted adaptive password hashes; never plaintext or reversible encryption. Prefer framework-supported PBKDF2 with a random per-user salt and documented parameters.
- [ ] Add application user storage with: immutable UserId, Username, PasswordHash metadata, DisplayName, IsEnabled, failed-login/lockout state, CreatedAt/CreatedBy, UpdatedAt/UpdatedBy and LastLoginAt.
- [ ] Usernames are unique and normalized consistently.
- [ ] Provision the first administrator through a local setup command/script or protected environment input. Never commit a real administrator password.
- [ ] Generic invalid-login message; do not reveal whether a username exists.
- [ ] Add bounded failed-login throttling/temporary lockout.
- [ ] Regenerate/renew authenticated session state after login as appropriate; logout invalidates auth ticket/session.
- [ ] Add sensible idle/session timeout and redirect expired sessions to Login.
- [ ] Preserve requested internal destination only when it is a validated local return URL.

### 10.2 Role/permission model — P0
Do not hard-code the whole application as only `Admin` versus `User`.

Implement:
- [ ] `AppUser`
- [ ] `AppRole`
- [ ] `AppPermission`
- [ ] `AppUserRole`
- [ ] `AppRolePermission`

Seed at minimum:
- [ ] `Administrator` — full approved application permissions.
- [ ] `User` — safe default operational/report permissions defined explicitly, not implicit full access.

Permission model is `MODULE/FUNCTION/ACTION`, for example:
- `CUSTOMER.VIEW`, `CUSTOMER.CREATE`, `CUSTOMER.EDIT`, `CUSTOMER.DELETE`, `CUSTOMER.IMPORT`, `CUSTOMER.EXPORT`
- `SALES_INVOICE.VIEW`, `SALES_INVOICE.CREATE`, `SALES_INVOICE.EDIT`, `SALES_INVOICE.DELETE_DRAFT`, `SALES_INVOICE.POST`, `SALES_INVOICE.PRINT`
- `STOCK_ISSUE.VIEW`, `STOCK_ISSUE.CREATE`, `STOCK_ISSUE.EDIT`, `STOCK_ISSUE.DELETE_DRAFT`, `STOCK_ISSUE.POST`, `STOCK_ISSUE.PRINT`
- `SALES_REPORT.VIEW`, `SALES_REPORT.EXPORT`, `SALES_REPORT.PRINT`
- equivalent permissions for the approved purchase, debt, journal, financial and catalog functions.
- [ ] Permission codes are stable/allowlisted and seeded/versioned; administrators select them rather than typing arbitrary permission strings.
- [ ] Default deny when a permission is missing.
- [ ] Authorization is enforced server-side at page/action/service boundaries. Hidden menu/buttons are only UX, never the security boundary.
- [ ] Direct URL, forged postback and output-token access cannot bypass permissions.
- [ ] Output/report snapshot access requires both session ownership and permission for the originating function.
- [ ] Decide and document whether one user may have multiple roles; if supported, effective permissions are the union of enabled assigned roles.
- [ ] Prevent removing/disabling the final enabled Administrator account.
- [ ] Prevent an administrator from accidentally removing their own last path to administration without an explicit safeguard.

### 10.3 Admin functions inside `Hệ thống` — P0/P1
Only users with the corresponding administration permissions see these menu items.

`Hệ thống`
- existing: Thông tin chung
- existing: Danh mục đơn vị cơ sở
- [ ] Quản lý người dùng
- [ ] Vai trò & phân quyền
- [ ] Nhật ký hệ thống

#### Quản lý người dùng
- [ ] List/search users.
- [ ] Create user.
- [ ] Edit display/profile fields.
- [ ] Enable/disable user.
- [ ] Assign/remove roles.
- [ ] Admin reset password to a temporary/new password without ever displaying the old password.
- [ ] Optional `must change password` on next login if implemented coherently; otherwise document that admin sets a new password securely.
- [ ] Show last login and lock state.
- [ ] Unlock a temporarily locked user.
- [ ] Do not physically delete users referenced by audit records; disable them instead.

#### Vai trò & phân quyền
- [ ] List roles.
- [ ] Create/rename/disable non-system roles.
- [ ] Permission matrix grouped by the seven modules and functions.
- [ ] Columns/actions appropriate to each function: View, Create, Edit, Delete Draft, Post, Import, Export, Print, Admin where applicable.
- [ ] Save role permissions atomically.
- [ ] Show a readable effective-permission summary.
- [ ] Protect built-in Administrator semantics from accidental lockout.
- [ ] UI menu visibility and toolbar action availability derive from the same effective permission service used by server authorization.

#### Nhật ký hệ thống
- [ ] Read-only audit grid with date/time, user, unit, module/function, action, entity/document identifier, outcome and safe summary.
- [ ] Filter by date, user, module/action and outcome.
- [ ] Audit at minimum: login success/failure, logout, user/role/permission changes, catalog create/edit/delete/import, document create/edit/delete draft/post, payment recording, journal posting and security-sensitive output denial.
- [ ] Never log plaintext passwords, password hashes, auth cookies, connection strings, DuckDNS tokens or uploaded file contents.
- [ ] Audit records are append-only to normal application users; no ordinary UI delete/edit.

### 10.4 Working unit/context — P1
The existing data model is unit-aware (`DonVi`/DVDEMO). Make that context explicit for multi-user operation.
- [ ] Header shows current working unit together with authenticated user.
- [ ] Define how a user obtains access to units. For this project either add `AppUserUnit` or explicitly allow all units for Administrator and assigned units for User; document the chosen rule.
- [ ] A user cannot query/mutate a unit they are not allowed to access by changing query/form values.
- [ ] Report filters and document entry default to current/allowed unit but preserve existing explicit unit behavior.
- [ ] If unit switching is implemented, validate it server-side and refresh context safely.

### 10.5 Existing business-workflow consistency review — P1
Do not silently add enterprise features. Verify and polish the approved subset.

#### Catalogs
- [ ] Consistent duplicate/required/FK-safe delete messages.
- [ ] Import preview/result clearly reports row failures and whole-batch rollback.
- [ ] Permission-check Import/Export separately from normal View/Edit.

#### Draft/Posted documents
Existing rule remains: drafts editable/deletable; posted documents immutable.
- [ ] Show status clearly in list/form/print.
- [ ] Disable/hide invalid actions in UI and still reject them server-side.
- [ ] Require explicit confirmation before `Ghi sổ/Post`.
- [ ] After posting, display why edit/delete is unavailable.
- [ ] Preserve existing atomic stock/commercial posting and concurrency protections.
- [ ] Do NOT implement physical deletion of posted accounting/stock/commercial facts.

#### Correction policy
- [ ] Document a clear correction policy in UI/docs: this internship subset does not support arbitrary editing/deleting of posted documents.
- [ ] If a posted-document cancellation/reversal is implemented in Phase 10, it must be an explicit new status/action, fully audited, permission-protected, transaction-safe, and must correctly reverse stock/debt/report effects. It must pass backdated/concurrency tests.
- [ ] If that complete invariant cannot be guaranteed, DO NOT implement cancellation; keep posted documents immutable and state that correction/reversal is outside this project scope. This is preferred over a partial unsafe implementation.

#### Sales/Purchase/Inventory coherence
- [ ] Re-run reconciliation that sales invoices create exactly one linked issue; purchase receipts change stock; purchase invoices link to receipts without a second stock movement; drafts/orders do not affect stock/revenue.
- [ ] Re-run backdated stock checks and duplicate/replay protection.
- [ ] Ensure report/document links make source relationships understandable where available.

#### Debt/payment coherence
- [ ] Preserve invoice-level partial/full settlement and overpayment prevention.
- [ ] Show invoice amount, paid amount and remaining amount clearly.
- [ ] Payment records remain immutable unless a complete reversal workflow is added; otherwise document payment reversal as out of scope.
- [ ] Debt reports reconcile opening + posted invoices - payments = closing.

#### General accounting/financial report
- [ ] Keep the manual teaching ledger scope explicit.
- [ ] Clearly label the balance report as a representative/practice report, not a statutory Vietnamese financial statement.
- [ ] Reconcile posted journal debit = credit and balance report source totals.
- [ ] Do NOT automatically post sales/purchase/stock/payment into the general ledger in Phase 10; that would be a new accounting architecture.

### 10.6 Explicitly deferred business features — NOT missing blockers
These were intentionally outside the approved internship subset and must NOT be auto-added by Codex:
- VAT/tax invoice engine and tax declarations.
- Discounts/promotions.
- Multi-currency/exchange-rate accounting.
- Full sales-order -> delivery -> invoice conversion workflow.
- Full purchase-order -> receipt -> invoice matching.
- Returns/credit notes/debit notes.
- Cash/bank module.
- Automatic subledger -> general-ledger posting.
- Payment reversal/multi-invoice allocation/advances unless separately authorized.
- Inventory costing methods (FIFO/weighted average) and cost of goods sold.
- Accounting period close/reopen.
- Statutory chart of accounts and legally compliant financial statements.
- Electronic invoice/tax authority integration.
- Approval workflow/digital signatures.
- Multi-company/tenant isolation.
- Backup scheduler/HA/disaster recovery platform.
These can be future work, but are not required to call the current internship reporting module complete.

### 10.7 UI/UX completion — P1
Current visual direction is ACCEPTED. Preserve the compact horizontal ERP layout, restrained blue/neutral palette and high information density. Do not redesign into a SaaS dashboard.

- [ ] Add project-owned application identity/brand (for example `PicoCorp Accounting`) without copying FAST logos/trademark assets.
- [ ] Header right side shows `DisplayName/Username | Role (or concise role summary) | Current Unit | Đăng xuất`; retain a subtle `Môi trường thực hành` indicator where useful.
- [ ] Clear active state for current top-level module and current submenu.
- [ ] Consistent breadcrumbs: `Phân hệ / Nhóm / Chức năng`.
- [ ] Nested menus: consistent spacing, hover/focus, keyboard support, viewport-edge collision handling and no clipping.
- [ ] Standardize page title, toolbar height, button order, disabled/loading states, form spacing and grid density.
- [ ] Keep `Làm tươi` on every required data/report screen.
- [ ] Add consistent success/error/warning/info feedback; validation messages appear near relevant input and/or a concise summary.
- [ ] Confirmation for destructive actions and posting.
- [ ] Prevent duplicate submits with disabled/loading feedback.
- [ ] Clearly distinguish selected row, Draft/Posted, disabled actions, totals and read-only records.
- [ ] Long grids: sticky header, intentional horizontal scrolling, numeric/money right alignment, totals and pagination.
- [ ] Reports clearly show applied filter summary, data timestamp and no-data state.
- [ ] Report `Tìm/Lọc` must make it obvious whether criteria are only edited or already applied.
- [ ] Output actions remain tied to the current effective report snapshot/filter.
- [ ] Login page visually belongs to the same product but does not render protected ERP navigation before authentication.
- [ ] Admin screens use the same dense list/form patterns; no dashboard cards.
- [ ] Keep Taste targets approximately DESIGN 3 / MOTION 1 / DENSITY 7.

### 10.8 Accessibility and viewport QA — P1
Desktop ERP is primary; smaller screens must remain usable, not necessarily mobile-first.
- [ ] Test 1920, 1440, 1366 and 1280 widths.
- [ ] Test a narrow/tablet viewport; preserve accounting columns via horizontal scroll rather than crushing them.
- [ ] Keyboard-only login, horizontal/nested menu, forms, filters, dialogs/panels and toolbar actions.
- [ ] Visible focus indicators.
- [ ] Programmatic labels for inputs and understandable validation/error text.
- [ ] Basic color contrast and browser text zoom.
- [ ] Avoid hover-only functionality that cannot be reached by keyboard/touch.

### 10.9 Production security hardening — P0
- [ ] Production `debug="false"`.
- [ ] Safe custom errors; remote users never receive stack traces, SQL details, local paths or secrets.
- [ ] Harden auth/session cookies: HttpOnly, Secure in HTTPS production, appropriate SameSite, scoped path and sensible expiration.
- [ ] Preserve ViewState MAC/event validation; never disable them to make a test pass.
- [ ] Add/test anti-CSRF protection for authenticated state-changing workflows; do not treat ViewState/event validation as a universal CSRF solution.
- [ ] Validate local return URLs; prevent open redirects.
- [ ] Continue parameterized SQL and server-side validation everywhere, including new admin/auth code.
- [ ] Security headers: `X-Content-Type-Options: nosniff`, restrictive `Referrer-Policy`, frame protection and a tested CSP compatible with Web Forms/postbacks.
- [ ] HTTPS is mandatory in production after certificate setup; localhost development remains usable.
- [ ] Review `.xlsx` upload path, extension/content structure, request/file limits and non-executable storage/processing.
- [ ] No source-controlled credentials/secrets/tokens/certificate keys.
- [ ] SQL Server remains private; never expose TCP 1433 to the Internet.
- [ ] Do not disclose whether protected resources exist through inconsistent auth error behavior.
- [ ] Audit privileged/security-relevant actions without logging secrets.

### 10.10 Error/recovery and operational UX — P1
- [ ] Friendly 403 Access Denied page distinct from 404 and 500.
- [ ] Friendly 404 and production 500 pages that retain safe navigation when appropriate.
- [ ] Session-expired behavior does not lose security context or accidentally resubmit mutations.
- [ ] User-facing errors for DB unavailable/timeouts are generic; detailed diagnostics stay server-side.
- [ ] Define safe behavior when export snapshot expires: tell user to rerun the report.
- [ ] Add application/version/environment indication somewhere unobtrusive for troubleshooting.

### 10.11 Live-browser and regression acceptance — P0/P1
Now that IIS Express is installed, perform actual browser QA rather than request-harness-only validation.
- [ ] Login/logout/timeout/lockout.
- [ ] Admin menu appears only with admin permissions.
- [ ] User menu/actions are filtered by effective permissions.
- [ ] Direct URL/forged postback cannot bypass authorization.
- [ ] User/unit scope cannot be bypassed by changing posted/query values.
- [ ] User/role/permission management works and cannot remove the last admin.
- [ ] Audit events are written for required actions.
- [ ] Catalog CRUD/search/import/export.
- [ ] Stock receipt/issue draft/post and immutable posted behavior.
- [ ] Sales/purchase workflows and linked stock movements.
- [ ] Payment/debt behavior.
- [ ] Journal and representative balance report.
- [ ] All 11 report filters, refresh, paging and empty state.
- [ ] Print preview, PDF and Excel download in a real browser.
- [ ] Vietnamese text/filenames.
- [ ] Back/refresh after logout cannot perform an authenticated mutation.
- [ ] Cross-user/session output token rejected.
- [ ] Tampered auth/session/ViewState/event data rejected safely.
- [ ] Injection-like login/admin inputs do not change SQL/auth behavior.
- [ ] Open redirect tests.
- [ ] Production error mode does not leak internals.
- [ ] Release build and ASP.NET precompile pass.
- [ ] Existing Phase 3–9 automated suites remain green.
- [ ] `HANG001: 100 + 50 - 20 = 130` remains true.
- [ ] Attempted issue 200 remains rejected atomically.

### 10.12 Public deployment readiness gate — P0
ALL must pass before enabling public inbound access:
- [ ] Authentication/RBAC/admin/audit acceptance complete.
- [ ] Production security hardening complete.
- [ ] Release build/precompile/regression/live-browser acceptance complete.
- [ ] Database backup and restore procedure documented and actually tested once.
- [ ] Production configuration contains no debug mode or source-controlled secrets.
- [ ] SQL Server is not publicly reachable.
- [ ] Deployment machine has an explicit Windows account/service/app-pool identity and minimum required filesystem/DB permissions.
- [ ] Owner explicitly approves public cutover after reviewing the final checklist.

### 10.13 Public deployment plan — AFTER Phase 10, do not execute inside this phase
Reserved DNS: `picocorp.duckdns.org`.

1. Use Windows IIS as persistent Web Forms host, not IIS Express.
2. Publish Release output to a dedicated IIS site/application pool.
3. Use protected production DB configuration; SQL stays private/local.
4. Configure DuckDNS updater using a protected local secret. Never commit or paste the token into repository files.
5. Confirm WAN/public-IP/CGNAT feasibility before relying on router port forwarding.
6. Expose only required web ports in router/firewall; never SQL Server.
7. Obtain/install a valid TLS certificate for `picocorp.duckdns.org`.
8. Bind HTTPS; only after HTTPS works, redirect HTTP -> HTTPS.
9. Verify cookies/security headers/login from an external network.
10. Re-run the full public smoke path: login -> permitted CRUD/document -> report -> PDF/Excel -> logout.
11. Document how to stop IIS/public binding, revoke/update DNS, rotate secrets and restore the DB.

### Phase 10 Definition of Done
- Authentication/login/logout works.
- Users, roles and permissions are configurable from admin-only `Hệ thống` screens.
- Menu visibility AND server actions use the same effective permission model.
- Unit access is server-enforced.
- Audit log covers authentication, administration and important mutations.
- Existing Draft/Posted and accounting-subset limitations are clear and safe.
- No unsafe partial cancellation/reversal feature is introduced.
- UI keeps the approved horizontal dense ERP style with improved identity, active states, breadcrumbs, feedback and admin screens.
- Production errors/cookies/CSRF/headers/secrets are hardened.
- Real browser and Phase 3–9 regression acceptance pass.
- App may be marked READY FOR PUBLIC DEPLOYMENT.
- Phase 10 itself MUST NOT open router/firewall ports, expose SQL, or make the site public.

### Codex Phase 10 execution instruction
Read `AGENTS.md`, `WALKTHROUGH.md`, this `IMPLEMENTATION_PLAN.md`, existing Phase 3–9 docs/tests and `.agents/skills/design-taste-frontend/SKILL.md` before changing code.

Preserve the locked V3 stack, seven modules, horizontal navigation, existing business invariants and completed Phase 1–9 behavior.

Implement Phase 10 in this order:
A. inspect existing Web.config/Site.Master/navigation/common page/service/output patterns and current DB migrations;
B. design/version the user-role-permission-unit-audit schema and permission allowlist;
C. authentication + central authorization service;
D. admin-only Hệ thống screens;
E. unit scope + audit integration;
F. UI/UX consistency and safe error pages;
G. production-security configuration;
H. automated regression/security tests;
I. live IIS Express browser acceptance.

Do not add the explicitly deferred ERP features in 10.6. Do not implement a partial posted-document reversal. Do not expose the application publicly during this phase. Do not open firewall/router ports, expose SQL Server, purchase/register services, or store DuckDNS/admin/database secrets in the repository.

At completion update this Phase 10 section with:
- exact DB migration/schema version;
- exact files/dependencies changed;
- actual password hashing parameters and auth/session policy;
- roles/permissions seeded and effective-permission rules;
- unit-access rule;
- audit coverage;
- security configuration;
- build/precompile/test commands and results;
- real-browser QA completed versus remaining;
- explicit deferred items;
- blockers, if any, before IIS + HTTPS public deployment.

Then STOP before public Internet cutover.

### Phase 10 actual implementation results (2026-10-03)

#### Database/schema
- Added migration `database/007-security.sql`; it requires ledger schema version 5, creates schema version 6, and is included by `database/setup.sql`.
- Added `AppUser`, `AppRole`, `AppPermission`, `AppUserRole`, `AppRolePermission`, `AppUserUnit`, and `AuditLog`. Permissions use the stable allowlist `MODULE.FUNCTION.ACTION`, generated only from the 28 approved navigation functions (25 business functions plus 3 Hệ thống admin functions) and their function-appropriate actions.
- Seeded `Administrator` with all 131 allowlisted permission codes and `User` with 104 operational/report permissions. `User` has no `SYSTEM.ADMIN_*` rights and no unit-administration right. Effective rights are the union of enabled roles assigned to an enabled user; missing rights deny by default. Admin UI selects seeded codes; it cannot enter arbitrary permissions. Built-in roles cannot be edited/disabled. The application prevents disabling/demoting the last enabled Administrator.
- `AuditLog` is append-only: a database trigger rejects ordinary UPDATE/DELETE with SQL error 51603. Authentication and admin writes audit in their transaction. Business mutation audits are recorded after successful store operations; failures/denials are also logged where the request path can safely do so.
- Applied migration to local `ReportingModuleDemo`. Verified schema version 6, seeded counts and required codes; `database/verify.sql` passed. No business schema or inventory behavior was changed by this migration.

#### Authentication, permissions and unit access
- Added standalone `Login.aspx`; Forms Authentication tickets are 20 minutes with sliding renewal. ASP.NET session idle timeout is 20 minutes. Login clears/abandons the pre-auth session and saves a new session ID. Logout audits, signs out, clears the session, and sends no-cache headers. `returnUrl` is restricted to an app-local path.
- Passwords use version 1 PBKDF2-HMAC-SHA256, 310,000 iterations, a cryptographically random 16-byte per-user salt and 32-byte derived key. Password minimum/maximum is 12/256 characters. Comparison is constant-time. Unknown usernames perform a dummy hash. Five failed attempts set a 15-minute lock; repeated attempts during an active lock do not extend its expiry. The response is generic for wrong username/password/locked account. No plaintext password is stored or committed.
- The first real Administrator is not seeded. Run `tools/provision-admin.ps1` locally; it prompts for the password as a secure string, hashes it, provisions one user, and does not print or persist the secret. The owner must perform this bootstrap before signing in.
- The same `PermissionService` controls server page/action checks, menu visibility and toolbar availability. Output snapshots also require session ownership, originating function permission and unit access. All approved screen IDs are checked before permission mapping; unknown screen IDs return 404.
- Added `AppUserUnit`. Users may access only assigned units; users with `SYSTEM.UNIT.ADMIN` (the built-in Administrator) may access all. There is no unit switcher. Current unit is selected from allowed units and every submitted/query unit is checked at the page/store boundary; report/output snapshots retain and recheck the unit.

#### Audit coverage and business integrity
- Audits login success/failure, logout, password change, denied permission/output/unit requests, user create/update/disable/unlock, role create/update/disable and permission changes, and catalog/document/payment/journal/report-output actions through the shared authorized-action wrapper.
- Audit summaries contain safe operation descriptions and identifiers; they do not include credentials, hashes, cookies, connection strings, uploaded contents or tokens.
- Existing draft/posted behavior is retained: drafts can be edited/deleted within their permissions; posted documents remain immutable. No cancellation/reversal was added. Correcting a posted document or payment is outside the current project scope and requires an explicitly designed complete workflow later.
- The focused security test verifies credential hashing, permission union/default exclusions, assigned-unit behavior, lockout, final-admin protection and append-only audit. Phase 3–9 regression and independent SQL checks pass, retaining HANG001 opening 100 + receipt 50 - issue 20 = closing 130 and atomic rejection of issue 200.

#### Security configuration and UI
- `Web.config`: `debug=false`, Framework target 4.8, Forms/cookie/session auth, HttpOnly, SameSite=Lax, 20-minute timeouts, ViewState MAC and event validation on, bounded request size, friendly remote custom errors, and local-development-compatible Secure cookie behavior. `RequireHttps` defaults false for localhost; production cutover must set it true behind HTTPS.
- `Global.asax`: secure flags on HTTPS cookies, HSTS for non-local HTTPS, rejection of non-local HTTP when `RequireHttps=true`, and server-side trace logging for unhandled exceptions.
- Added nosniff, strict-origin-when-cross-origin referrer policy, frame denial, restrictive self-only CSP with inline script/style allowance needed by current Web Forms controls, and a restrictive Permissions-Policy. Added anti-CSRF session token checks on login and authenticated postbacks, plus per-user/session `ViewStateUserKey`.
- Added branded login, current-user/role/unit header, permission-filtered seven-module navigation, breadcrumbs, admin screens, change-password/logout and friendly denied/not-found/error pages. Refresh is present on all approved leaves including admin screens. Visual direction remains compact horizontal ERP; no dashboard/sidebar/framework/dependency was introduced.

#### Files and dependencies
- New: `database/007-security.sql`; `src/ReportingModule.Web/Security.cs`, `AdminStore.cs`, `Login.aspx`, `Login.aspx.cs`, `Logout.aspx`, `Logout.aspx.cs`, `Denied.aspx`, `NotFound.aspx`, `Error.aspx`, `ChangePassword.aspx`, `ChangePassword.aspx.cs`, `SystemAdmin.aspx`, `SystemAdmin.aspx.cs`, `Global.asax`, `Global.asax.cs`; `tools/provision-admin.ps1`; `tests/PhaseTestAuth.cs`, `tests/Phase10Checks.cs`, `tests/set-test-identity.ps1`, `tests/run-phase10.ps1`; `docs/PHASE10.md`.
- Updated: `IMPLEMENTATION_PLAN.md`, `README.md`, `database/README.md`, `database/setup.sql`, `database/001-schema.sql`, `database/003-inventory.sql`; `src/ReportingModule.Web/ReportingModule.Web.csproj`, `Web.config`, `Site.Master`, `Site.Master.cs`, `Navigation.cs`, `Default.aspx.cs`, `Catalog.aspx.cs`, `CatalogStore.cs`, `Stock.aspx.cs`, `StockStore.cs`, `Commerce.aspx.cs`, `CommerceStore.cs`, `DebtStore.cs`, `Journal.aspx.cs`, `JournalStore.cs`, `Output.aspx.cs`, `ReportOutput.cs`, `Content/site.css`; `tests/Phase3WebChecks.cs` through `Phase9WebChecks.cs` and `tests/run-phase3.ps1` through `run-phase9.ps1`.
- No application package, framework dependency or public network configuration was added. Test fixtures use temporary credentials and do not contain a real credential.

#### Build, tests and live browser actually run
- Release build: `MSBuild.exe ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal` — PASS.
- ASP.NET Web Forms precompile: `aspnet_compiler.exe -v / -p src\ReportingModule.Web <new artifacts output directory>` — PASS via `tests/run-phase10.ps1`.
- `tests/run-phase10.ps1` — PASS: password hash/version/policy, normalized username, User vs Administrator permissions, union and disabled-role behavior, unit assignment, allowlisted permission, five-failure/15-minute lockout and non-extending active lock, final enabled administrator guard, append-only audit trigger.
- `tests/run-all.ps1` — PASS: Phase 3, 4, 5, 6, 7, 8, 9 plus independent SQL checks. Per-phase logs are in `artifacts/quality`.
- Browser: on actual `http://localhost:8080`, confirmed the current ERP workspace renders with all seven horizontal module roots and the refresh action; confirmed standalone login has no protected shell; confirmed anonymous direct requests to `Default.aspx` and `SystemAdmin.aspx` redirect to Login with a local `ReturnUrl`. No credential was entered in the browser. This is a limited smoke check, not completion of every Phase 10.11 workflow.
- Follow-up login defect: the login form posts the `Csrf` field, while `LoginPageBase` was checking for `csrfToken`, so the valid login postback was always rejected with HTTP 400 before credentials were checked. Updated `Security.cs` to validate the actual `Csrf` field. A Release build to `artifacts/login-fix-build` and staged ASP.NET precompile passed. On a temporary IIS Express instance bound only to `localhost:8081`, submitting the login form with blank credentials passed CSRF validation and returned the generic invalid-credentials message rather than HTTP 400. No password was entered.
- Follow-up DLL lock: PID 36360 was the interactive PowerShell terminal used to run `tools/provision-admin.ps1`. Its `Add-Type` loaded `ReportingModule.Web.dll`, which stayed locked for the lifetime of that shell. Removed the assembly load from the provisioner and implemented the matching .NET Framework PBKDF2-HMAC-SHA256 call directly. Verified a generated test hash with `PasswordHash.Verify`. Closed the idle provisioning shell; the Debug MSBuild then passed and replaced `bin/ReportingModule.Web.dll`. Restart the paused IIS Express/Visual Studio debugging session to load the updated assembly.

#### Remaining manual checks and public deployment blockers
- Still require an owner-provisioned real administrator and authenticated browser walkthrough of login/logout, lockout/timeout, menu/toolbar permission filtering, user/role administration, last-admin UI guard, unit tampering, CSRF/forged postback, logout/back-cache, all CRUD/document/payment/report/export/print workflows, Vietnamese filenames and responsive/keyboard/focus checks. Automated request/security tests cover only the cases listed above.
- A database backup/restore procedure has not been documented or tested. Production IIS identity/least-privilege database setup, production HTTPS binding/certificate, external-network security/header validation, and final owner public-cutover approval are not done.
- The app remains local-only. `RequireHttps=false` is for localhost. No router/firewall/public binding, DuckDNS updater, TLS setup, or SQL Server exposure was performed. SQL Server port 1433 must remain private.
- The explicit 10.6 deferred list remains deferred; no tax, costing, automatic GL posting, order conversion, returns, payment reversal or other excluded workflow was added. No partial posted-document cancellation/reversal is implemented.
- Therefore Phase 10 code/build/test work is implemented locally, but the project is **NOT READY FOR PUBLIC DEPLOYMENT**. Public deployment must wait for the checks above and the 10.12 readiness gate.

#### Navigation UX follow-up (2026-10-12)
- Updated `src/ReportingModule.Web/Content/site.css` so top-level and nested menu disclosure indicators use the same compact chevron, rotate to show the expanded state, retain visible keyboard focus, and honor reduced-motion preferences. This replaces the always-down text glyph and the browser-default nested marker without changing click behavior, menu labels, routes, or the seven-module layout.
- Output guidance clarified: Excel and PDF are the standard output choices; bilingual print templates remain function-specific and are not assumed for every menu.
- Validation: static stylesheet review only. A live browser check could not run because `http://localhost:8080/Default.aspx` refused the connection. No application build was run because only CSS and this plan changed; visual confirmation remains pending when the local site is running.

#### Purchase workflow business-integrity follow-up (2026-10-12)
- User approved separating “Phiếu nhập mua hàng” (physical receipt/inventory) from “Hóa đơn mua hàng” (payable). `purchase-receipt` now routes to Stock.aspx and requires a supplier. `ap-invoice` remains a commercial invoice screen and requires a reference to a posted receipt from the same unit and supplier when posting.
- Added migration `database/008-purchase-workflow.sql`, schema version 7: adds `ChungTuKho.MaNCC` and `ChungTuThuongMai.ChungTuNhapMuaId`, supplier/type validation, FKs, and updated `SaveStockDocument` / `SaveCommerceDocument`. Setup remains repeatable; version guards in migrations 001, 003, and 007 now allow version 7.
- Existing posted purchase invoices and their generated receipt movements were backfilled to the new supplier/reference fields without changing statuses, replaying quantities, or changing stock totals. New purchase invoices do not generate inventory movements. Sales invoice stock issue behavior is unchanged.
- Relationship currently supports one receipt per invoice; a receipt can be referenced by multiple invoices. The system does not yet reconcile invoice lines against receipt lines, accept one invoice across multiple receipts, allocate partial quantities, or process tax/discount differences. These are documented limits, not hidden assumptions.
- UI changes: supplier column/field on purchase receipts, receipt selector on AP invoice, separate posting messages, workflow-specific help text, and new route. No menu/module, framework, dependency, or unrelated accounting behavior changed.
- Tests updated: purchase receipts are created independently, AP invoices require a valid link, no second stock movement occurs, reports/payables continue to derive from invoice lines, and the purchase receipt screen posts through ASP.NET. Cleanup removes fixture invoices before referenced receipts.
- Validation: local SQL setup migration to version 7 passed and was rerun; Release MSBuild passed; `tests/run-all.ps1` passed Phases 3–9 plus `verify.sql` and `verify-inventory.sql`; HANG001 remains opening 100 + receipt 50 - issue 20 = closing 130, and issue 200 is rejected atomically. Phase 8 also passed selected-document and purchase-receipt UI checks. This used the hosted ASP.NET test harness, not a manual interactive browser walkthrough.
- Final focused rerun after improving receipt selector labels: `tests/run-phase5.ps1` passed, including missing supplier, missing/unknown receipt, supplier mismatch, and no-double-stock checks. ASP.NET precompile passed to a fresh `artifacts/purchase-flow-precompiled-<timestamp>` directory.
- Files changed for this follow-up: `database/008-purchase-workflow.sql`; `database/setup.sql`, `001-schema.sql`, `003-inventory.sql`, `007-security.sql`, `database/README.md`; `src/ReportingModule.Web/Navigation.cs`, `Stock.aspx.cs`, `StockStore.cs`, `Commerce.aspx.cs`, `CommerceStore.cs`; `tests/Phase4Checks.cs`, `Phase5Checks.cs`, `Phase5WebChecks.cs`, `Phase6Checks.cs`, `Phase6WebChecks.cs`, `Phase8WebChecks.cs`; `docs/PHASE5.md`, `docs/PHASE6.md`, `README.md`, and this plan.
- No tax policy, purchase line matching, accounting journal automation, cancellation/reversal, or public deployment was added. Remaining production/public blockers from Phase 10 are unchanged.

#### Print-template and PDF-permission follow-up (2026-10-17)
- Updated selected stock/commercial/journal document output to use the FAST-style voucher reference as a field/layout guide. The template uses saved number/date, unit name/code, partner name/code/address when present, detail rows with STT, totals and function-specific signature headings. Warehouse/bin, amount-in-words, company address/tax number or any other fact absent from app data are not fabricated. Internal/training disclaimers were removed, and the same wording was removed from the visible shell and page guidance.
- PDF and browser print use the same A4 portrait voucher design. Excel now has a designed A4 portrait voucher sheet with a styled header, merged metadata, bordered detail grid, numeric quantity/price/amount, total, signatures, print area and page setup. Excel for reports remains the filtered result export. No company identifiers or sample company data from the reference image were copied.
- PDF and browser print preview now share the function's PRINT permission; Excel continues to require EXPORT. The preview hides each output option when the current user lacks its permission. This removes the case where users with print permission received a generic denial when selecting PDF.
- Changed `src/ReportingModule.Web/ReportOutput.cs`, `ReportPdf.cs`, `ReportExcel.cs`, `StockStore.cs`, `CommerceStore.cs`, `JournalStore.cs`, `Site.Master`, `Default.aspx`, `Journal.aspx.cs`, `Content/site.css`, `tests/Phase8Checks.cs`, `tests/run-phase8.ps1`, `tests/verify-phase8.py`, `docs/PHASE8.md`, and this plan. No database schema, application packages or new frameworks changed. Test compilation now explicitly references the already-installed `System.IO.Compression` assembly.
- Validation: Release MSBuild passed. `tests/run-phase8.ps1` passed voucher HTML/PDF/XLSX assertions, all 11 report HTML/XLSX/PDF requests, selected-document print checks, effective-filter/session checks, inventory 130 acceptance, and purchase-receipt UI checks; temporary fixtures were removed. `tests/run-all.ps1` then passed Phases 3–9 and independent `database/verify.sql` / `verify-inventory.sql` checks. `tests/verify-phase8.py` opened all generated XLSX files, parsed and rendered all 13 PDFs, checked voucher styles, partner/total values, A4 setup and print area, and reconciled report totals. The voucher PDF was visually inspected; its STT column was widened and the document preview uses an A4 portrait page rule. Excel opened through the independent workbook reader, but no native Excel/LibreOffice page rendering was available. The SQL-backed suite initially could not connect inside the sandbox; it completed with approved local SQL access. No real interactive browser/printer test was performed.
- Remaining manual check: restart/reload local IIS Express, select a saved document, open In, verify displayed supplier/customer and address, then test the browser print dialog and Excel Print Preview on the target machine. Statutory form data, warehouse/bin and amount-in-words remain omitted until actually modeled and confirmed. Public deployment is outside this change.
