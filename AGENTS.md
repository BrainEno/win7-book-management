# AGENTS.md

## Project goal
Build a small, dependable, beginner-friendly, completely offline bookstore inventory/sales application for Windows 7 SP1.

## Hard compatibility constraints
- Runtime target: **.NET Framework 4.8**.
- Desktop UI: **Windows Forms (WinForms)**.
- Database: **SQLite**, stored locally as a file.
- The released application must run without Internet access.
- Final installer must bundle all application runtime DLLs and the .NET Framework 4.8 offline runtime.
- Do not introduce runtime dependencies that require Windows 10/11.
- Do not migrate the application to .NET (Core/5+/6+/8+), Electron, Flutter, or a browser-hosted runtime.
- Prefer conservative dependencies with explicit .NET Framework 4.8 support.
- Build artifacts must include every non-system DLL/native SQLite dependency required at runtime.

## Scope
The commercial baseline is intentionally small:
1. Book master data.
2. Suppliers.
3. Purchase receiving.
4. Sales checkout.
5. Document center.
6. Sales returns and purchase returns.
7. Inventory query and inventory movement history.
8. Reports and Excel export by date.
9. SQLite backup and restore.
10. Minimal settings and low-stock reminders.
11. Operating dashboard.
12. First-run onboarding and persistent beginner help.

Out of scope unless explicitly requested: cloud sync, mobile apps, multi-store networking, online accounts, complex accounting, CRM, microservices.

## Architecture
Keep one Windows desktop solution with clear folders/layers:
- Forms: UI only and input validation.
- Models: data contracts/entities.
- Services: business rules and transaction orchestration.
- Repositories: SQLite reads/writes.
- Database: connection/bootstrap/schema/migrations.
- Reporting: queries and Excel export.
- Infrastructure: backup, file paths, visual theme, onboarding/settings helpers.

Do not add architectural layers without a concrete need.

## Data invariants
- Money is stored as integer cents (long), never binary floating point.
- Quantities are integers for the book-only scope.
- ISBN is a business identifier, not the primary key.
- Primary keys are internal integer IDs.
- Historical documents preserve snapshots.
- Every stock-changing operation must execute inside one SQLite transaction, update current stock, append an inventory transaction row, and either commit all changes or none.
- Never mutate historical inventory transaction rows to "fix" current stock. Use an explicit adjustment transaction.
- Historical inventory for a date must be reproducible from the inventory ledger.
- Deleting a book that has transaction history is forbidden; deactivate it instead.

## Return invariants
- Never edit or delete an original sales or purchase document to represent a return.
- A return is always a new document linked to an original document.
- Sales returns increase stock and create positive SALE_RETURN inventory movements.
- Purchase returns decrease stock and create negative PURCHASE_RETURN inventory movements.
- A source line cannot be returned beyond its original quantity across all return documents.
- Purchase return cannot reduce current stock below zero.
- Return prices/costs come from the original document snapshot.
- Dashboard net sales must deduct sales returns by return date.

## UI and onboarding rules
- Chinese UI by default.
- Assume the operator may be a complete computer beginner.
- Every primary workflow should explain what to do next in plain language.
- The home page must retain a visible beginner workflow summary.
- First fresh database launch must automatically show an interactive onboarding overlay.
- The onboarding must be replayable later without clearing user data.
- Use explicit action labels such as “确认入库” and “确认退货”; avoid jargon-only buttons.
- Warn before navigating away from unfinished sales/purchase work.
- Destructive actions require confirmation.
- Validation errors must say what the user should correct.
- Use consistent palette, typography, spacing, button hierarchy, selected navigation state, cards and grid styling.
- Prefer installed-font fallback suitable for Win7.
- Responsive behavior should reduce spacing/navigation width before scrolling.
- Third-party WinForms UI libraries are allowed only when they explicitly support net48, add clear UX value, have acceptable licensing, and pass Win7 smoke-test expectations.

## Offline packaging rules
- The target customer PC must never need to search the web for DLLs.
- Installer must recursively include the full Release output, including native SQLite folders.
- Installer must bundle the official .NET Framework 4.8 offline runtime and install it only when missing.
- Installer minimum OS is Windows 7 SP1.
- If a prerequisite installation may require reboot, do not force-launch the app immediately.
- Packaging script must validate core runtime files before generating installer.
- Packaging process may use Internet on the development/CI machine to fetch official build prerequisites; the generated installer must not need Internet.
- Keep installer build reproducible through `build-installer.cmd`.

## SQLite rules
- Enable foreign keys for every connection.
- Prefer WAL where safe, but checkpoint before database-file backup.
- Schema changes must be versioned and forward-only.
- Database bootstrap must create a new usable database automatically.
- Backups must use a consistent SQLite backup/copy procedure.

## Excel/reporting rules
Required exports:
- Sales detail.
- Sales return detail.
- Purchase detail.
- Purchase return detail.
- Inventory snapshot.
- Inventory movement detail.
Exports must be valid .xlsx files and must not require Microsoft Excel.

## Testing expectations
At minimum, verify:
- clean database creation and forward schema upgrade;
- onboarding/settings persistence;
- purchase/sale/return inventory correctness and rollback;
- document return statuses;
- historical inventory snapshot;
- dashboard net sales;
- report date boundaries;
- Excel export;
- backup;
- Release self-test from the exact folder copied into installer;
- installer generation succeeds and contains app runtime plus .NET 4.8 offline prerequisite.

## Change discipline
Before changing behavior:
1. Read this file and README.md.
2. Preserve Win7/.NET Framework 4.8 compatibility.
3. Prefer the smallest coherent change.
4. Keep business logic out of Forms.
5. Update README/docs when user-visible behavior or setup changes.
6. Do not weaken inventory, return, offline packaging, or beginner-safety invariants for convenience.
