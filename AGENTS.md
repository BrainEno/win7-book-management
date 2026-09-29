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
The commercial baseline is intentionally small:
1. Book master data.
2. Suppliers.
3. Purchase receiving.
4. Sales checkout.
5. Document center for sales, purchases, sales returns and purchase returns.
6. Sales returns and purchase returns based on original documents.
7. Inventory query and inventory movement history.
8. Reports and Excel export by date.
9. SQLite backup and restore.
10. Minimal settings and low-stock reminders.
11. A lightweight operating dashboard.

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
- Return prices/costs come from the original document snapshot, not from the current book master price.
- Original documents must expose returned and remaining-returnable quantities.
- Dashboard net sales must deduct sales returns by the return date.

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
- Returns belong to the date/time when the return is actually processed.

## Excel/reporting rules
Required exports:
- Sales detail by date range.
- Sales return detail by date range.
- Purchase detail by date range.
- Purchase return detail by date range.
- Inventory snapshot for a selected date.
- Inventory movement detail by date range.
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
- sales return restores stock and cannot exceed source quantity;
- purchase return deducts stock and cannot exceed source quantity/current stock;
- failed sale/purchase/return rolls back completely;
- original document return status/quantities remain correct;
- historical inventory snapshot remains correct after returns;
- dashboard net sales deducts sales returns;
- date-range sales/purchase/return/movement queries use correct inclusive boundaries;
- Excel export creates a readable workbook;
- backup produces a restorable database.

## Change discipline
Before changing behavior:
1. Read this file and README.md.
2. Preserve Win7/.NET Framework 4.8 compatibility.
3. Prefer the smallest coherent change.
4. Keep business logic out of Forms.
5. Update README/docs when user-visible behavior or setup changes.
6. Do not weaken inventory or return invariants for convenience.
