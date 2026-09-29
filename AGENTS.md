# AGENTS.md

## Project goal
Build a small, dependable, completely offline bookstore inventory/sales application for Windows 7 SP1.

## Hard compatibility constraints
- Runtime target: **.NET Framework 4.8**.
- Desktop UI: **Windows Forms (WinForms)**.
- Database: **SQLite**, stored locally as a file.
- The released application must run without Internet access.
- Do not introduce runtime dependencies that require Windows 10/11.
- Do not migrate the application to .NET (Core/5+/6+/8+), Electron, Flutter, or a browser-hosted runtime.
- Prefer conservative dependencies with explicit .NET Framework 4.8 support.
- Build artifacts must include every non-system DLL/native SQLite dependency required at runtime.

## Scope
The first commercial baseline is intentionally small:
1. Book master data.
2. Suppliers.
3. Purchase receiving.
4. Sales checkout / sales history.
5. Inventory query and inventory movement history.
6. Reports and Excel export by date.
7. SQLite backup and restore.
8. Minimal settings and low-stock reminders.
9. A lightweight operating dashboard.

Out of scope unless explicitly requested: cloud sync, mobile apps, multi-store networking, online accounts, complex accounting, CRM, microservices.

## Architecture
Keep one Windows desktop solution with clear folders/layers:
- Forms: UI only and input validation.
- Models: data contracts/entities.
- Services: business rules and transaction orchestration.
- Repositories: SQLite reads/writes.
- Database: connection/bootstrap/schema/migrations.
- Reporting: queries and Excel export.
- Infrastructure: backup, file paths, visual theme, logging/helpers.

Do not add architectural layers without a concrete need.

## Data invariants
- Money is stored as integer cents (long), never binary floating point.
- Quantities are integers for the initial book-only scope.
- ISBN is a business identifier, not the primary key.
- Primary keys are internal integer IDs.
- Sales and purchase documents preserve item snapshots needed for historical reporting.
- Every stock-changing operation must:
  1. execute inside one SQLite transaction;
  2. update current stock;
  3. append an inventory transaction row;
  4. either commit all changes or commit none.
- Never mutate historical inventory transaction rows to "fix" current stock. Use an explicit adjustment transaction.
- Historical inventory for a date must be reproducible from the inventory ledger.
- Deleting a book that has transaction history is forbidden; deactivate it instead.

## SQLite rules
- Enable foreign keys for every connection.
- Prefer WAL where it is safe, but checkpoint before database-file backup.
- Schema changes must be versioned and forward-only.
- Database bootstrap must create a new usable database automatically.
- Backups must use a consistent SQLite backup/copy procedure, not copy an actively-written file blindly.

## Date/time rules
- Store timestamps in an unambiguous sortable format.
- Report date filters are inclusive by local calendar date.
- A stock snapshot for a date means stock at the end of that local calendar day.

## Excel/reporting rules
Required exports:
- Sales detail by date range.
- Inventory snapshot for a selected date.
- Inventory movement detail by date range.
- Purchase detail by date range.
Exports must be valid .xlsx files and must not require Microsoft Excel to be installed.

## UI rules
- Chinese UI by default.
- The application should feel modern and calm without sacrificing Windows 7 compatibility.
- Use a consistent palette, typography, spacing, button hierarchy, navigation selected state, cards and grid styling.
- Prefer Segoe UI / Microsoft YaHei UI / Microsoft YaHei with safe installed-font fallback.
- Optimize for keyboard/mouse desktop use, barcode scanners acting as keyboard input, and common 1366x768-or-larger displays.
- Responsive behavior should reduce spacing and navigation width before introducing scrolling.
- Preserve strong contrast and visible focus/selection states.
- Primary actions must be visually distinct from secondary actions.
- Warn before navigating away from unfinished sales/purchase work.
- Destructive actions require confirmation.
- Validation errors must explain what the operator should correct.
- Long operations must not silently freeze without feedback.
- Third-party WinForms UI libraries are allowed only when they explicitly support net48, add clear UX value, have acceptable licensing, and pass the same build plus Win7 smoke-test expectations. Do not couple core business logic to a UI vendor.

## Build and dependency rules
- Solution must build in Release mode for .NET Framework 4.8.
- Keep NuGet dependency count low.
- SQLite and Excel libraries must be pinned to known versions.
- UI packages, if introduced, must also be pinned.
- Do not depend on a network service at runtime.
- Do not commit bin/, obj/, packages/, database files, exports, or user backups.

## Testing expectations
At minimum, verify:
- clean database creation and forward schema upgrade;
- purchase increases stock and creates ledger rows;
- sale decreases stock and creates ledger rows;
- failed sale/purchase rolls back completely;
- insufficient stock is rejected unless an explicit future requirement changes this;
- historical inventory snapshot remains correct after later transactions;
- date-range sales/purchase/movement queries use correct inclusive boundaries;
- dashboard queries and low-stock settings work;
- key Forms can be constructed without runtime exceptions;
- Excel export creates a readable workbook;
- backup produces a restorable database.

## Change discipline
Before changing behavior:
1. Read this file and README.md.
2. Preserve Win7/.NET Framework 4.8 compatibility.
3. Prefer the smallest coherent change.
4. Keep business logic out of Forms.
5. Update README/docs when user-visible behavior or setup changes.
6. Do not weaken the inventory-ledger invariants for convenience.
