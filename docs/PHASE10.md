# Phase 10 — Authentication and security

Phase 10 adds local authentication, configurable permissions, user/role administration, working-unit access checks and append-only audit logging to the existing Web Forms ERP shell. This build is for localhost use. It is not approved for public Internet exposure.

## Database and first administrator

`database/007-security.sql` advances the local demo schema from version 5 to version 6. It creates `AppUser`, `AppRole`, `AppPermission`, `AppUserRole`, `AppRolePermission`, `AppUserUnit`, and `AuditLog`. Permissions use only seeded `MODULE.FUNCTION.ACTION` codes for approved screens and actions. `Administrator` receives the full allowlist; `User` receives a bounded operational/report set. Effective permissions are the union of enabled roles assigned to an enabled user. Unknown or missing permissions are denied.

The migration does not seed a real administrator credential. After `database/setup.sql`, run:

```powershell
.\tools\provision-admin.ps1
```

The script requests username, display name and password interactively, uses Windows Integrated Security, stores only a derived password hash, and never writes the password to a file or console output. It uses the .NET Framework PBKDF2-HMAC-SHA256 implementation directly and does not load the web assembly into the interactive PowerShell process, so it will not keep the build DLL locked. Run it only on the trusted local computer.

## Authentication policy

- Password hashing: PBKDF2-HMAC-SHA256, version 1, 310,000 iterations, 16-byte random salt and 32-byte derived key. Passwords must be 12–256 characters.
- Forms Authentication and ASP.NET session: 20-minute timeout; session idle tracking is enforced by the application.
- Five failed attempts cause a 15-minute lock. Guesses during an active lock do not extend its expiry. Login errors do not reveal whether an account exists.
- Login rotates the session ID; logout clears the session and auth ticket.
- Return destinations must be local paths. Login and authenticated postbacks use anti-CSRF tokens; ViewState MAC and event validation remain enabled.

## Unit and audit policy

Users are restricted to their assigned `AppUserUnit` rows. An Administrator with `SYSTEM.UNIT.ADMIN` can access all working units. There is no user-facing unit switcher. Query/form unit changes and output snapshot access are checked on the server.

The audit grid is read-only. A database trigger rejects ordinary audit updates and deletes. Login, logout, password changes, admin changes, permission denials and important catalog/document/payment/report actions are recorded without passwords, hashes, cookies, SQL connection strings or file contents.

Draft documents retain their existing editable behavior. Posted documents and payments remain immutable. This project does not support posted-document cancellation/reversal. No Phase 10.6 deferred business feature was added.

## Verification recorded

Commands run from the project root:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe' ReportingModule.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal
.\tests\run-phase10.ps1
.\tests\run-all.ps1
```

Release build and ASP.NET precompile passed. Focused Phase 10 tests passed for password policy/hash matching, normalized login, safe User-role rights, multi-role union, disabled-role removal, unit assignment, permission allowlist, five-failure lockout without expiry extension, last-Administrator protection and audit append-only enforcement. Phase 3–9 suites and independent SQL checks passed, including inventory opening 100 + receipt 50 - issue 20 = closing 130 and rejection of an issue of 200.

Actual localhost browser checks confirmed that the ERP page has the seven horizontal top-level modules and refresh action, the Login page does not show the protected shell, and anonymous requests to the workspace and admin URL redirect to Login with local return paths. These checks did not cover an authenticated user workflow.

After a user reported that every login attempt stopped with HTTP 400, the login CSRF validator was corrected to read the posted `Csrf` field. A Release build and staged ASP.NET precompile passed. A temporary IIS Express instance on `localhost:8081` accepted a form post with its CSRF token and displayed the generic invalid-credentials message for blank credentials, confirming the request passed CSRF validation; no password was entered. The regular `bin` assembly was initially locked by the interactive PowerShell terminal used for provisioning, because the original script loaded the web assembly with `Add-Type`. The script no longer does that; after closing the idle PowerShell terminal, the Debug build passed and replaced the `bin` assembly. Restart the paused Visual Studio/IIS Express session to load the updated code on port 8080.

## Still required

- An Administrator account is provisioned. The owner must complete authenticated browser checks for login/logout, timeout/lockout, permissions, user/role administration, unit tampering, CSRF, business mutations, reports, print and exports.
- Keyboard, viewport, focus, Vietnamese output filenames and logout/back behavior still need browser verification.
- Document and test a database backup/restore process. Configure the production IIS identity with least privilege and perform the final security review.
- Do not configure public DNS, port forwarding, a public IIS binding or TLS during Phase 10. SQL Server port 1433 must remain private. Public deployment requires separate approval after the readiness gate in `IMPLEMENTATION_PLAN.md` is satisfied.
