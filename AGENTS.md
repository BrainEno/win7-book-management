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
- Sales order items preserve reporting-relevant book snapshots (store code, author, publisher, category, publication year and list price) plus the sale-time reference-cost source/value; later edits to book master data must not rewrite historical sales reporting.
- Sales orders preserve the selected payment method. Cash checkout also preserves amount received and change; non-cash checkout records received equal to the final payable amount and zero change.
- Sales returns preserve a refund method. Refund reporting is attributed by return date and refund method, never by rewriting the original payment snapshot.
- Held sales are drafts only: saving, loading, updating, or deleting a held sale must never change stock or append inventory ledger rows. Stock is revalidated only when the sale is actually checked out.
- Default purchase price is master-data assistance only; it may prefill a new purchase line but must never rewrite historical purchase prices.
- Every stock-changing operation must execute inside one SQLite transaction, update current stock, append an inventory transaction row, and either commit all changes or none.
- Never mutate historical inventory transaction rows to "fix" current stock. Use an explicit adjustment transaction.
- Historical inventory for a date must be reproducible from the inventory ledger.
- Deleting a book that has transaction history is forbidden; deactivate it instead.
- Sales discounts are stored as integer basis points (10000 = 100.00%). Apply line discount before whole-order discount, round each unit-price stage to integer cents using AwayFromZero, and preserve the pre-discount price plus both discount snapshots.
- `sales_order_items.unit_price_cent` is the actual final unit price paid after all discounts. Sales returns must continue to refund from that immutable snapshot.
- Generic dictionary values are never physically deleted from historical business data. Disabling a value prevents future selection; renaming a book category must update current book master data transactionally while historical documents remain snapshot-based.

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
- All primary Forms and dialogs must remain usable at Windows 7-era 1024×768 as well as Windows 10/11 4K with display scaling. Prefer `AutoScaleMode.Dpi`, wrapping/adaptive toolbars, non-wrapping grid headers, and hiding secondary table columns at narrow widths before forcing horizontal scrolling.
- Third-party WinForms UI libraries are allowed only when they explicitly support net48, add clear UX value, have acceptable licensing, and pass Win7 smoke-test expectations.
- **AntdUI 2.4.12 is the single preferred interactive UI component library.** When AntdUI provides a stable net48 control (Menu, Button, Input, InputNumber, Select, Checkbox, DatePicker, Table, etc.), prefer it over native WinForms interactive controls.
- Prefer AntdUI's **built-in Ant Design light theme and default state styling**. Do not hand-paint AntdUI controls with page-specific BorderColor / Radius / Hover / Selected colors unless a concrete business-state distinction requires it.
- UiTheme exists primarily for Win7/DPI/layout safety and for native WinForms layout surfaces that AntdUI does not replace. It must not become a parallel skin system over AntdUI.
- The main sidebar must use AntdUI.Menu rather than a custom stack of buttons, indicators, or hand-written hover/selected states.
- Onboarding must keep the underlying interface readable. Do not dim the entire guide Form with high `Opacity`; use a readable spotlight/snapshot approach where the target remains fully visible and the guide card remains opaque.

## Offline packaging rules
- The target customer PC must never need to search the web for DLLs.
- Installer must recursively include the full Release output, including native SQLite folders.
- Installer must bundle the official .NET Framework 4.8 offline runtime and install it only when missing.
- Installer minimum OS is Windows 7 SP1.
- If a prerequisite installation may require reboot, do not force-launch the app immediately.
- Packaging script must validate core runtime files before generating installer.
- Packaging process may use Internet on the development/CI machine to fetch official build prerequisites; the generated installer must not need Internet.
- Use Inno Setup 6.7.3 as the installer compiler. Keep `MinVersion=6.1sp1`, keep the packaging tool outside business logic, and preserve the ability to replace it later without changing application code.
- Keep installer build reproducible through `build-installer.cmd`.
- The BOOK DESK application icon must be reproducibly generated before compilation and embedded in the executable; installer/shortcuts/title bars must use the same icon.

## SQLite rules
- Enable foreign keys for every connection.
- Prefer WAL where safe, but checkpoint before database-file backup.
- Schema changes must be versioned and forward-only.
- Database bootstrap must create a new usable database automatically.
- Backups must use a consistent SQLite backup/copy procedure.

## Excel/reporting rules
Required exports:
- Sales detail.
- Template-style monthly sales workbook.
- Sales return detail.
- Purchase detail.
- Template-style monthly purchase workbook.
- Purchase return detail.
- Operating daily summary.
- Operating monthly summary.
- Inventory snapshot.
- Inventory movement detail.
Exports must be valid .xlsx files and must not require Microsoft Excel.

Print-grade workbook invariants:
- All exported workbooks use A4 paper, explicit margins, fit-to-one-page-width scaling, a bounded print area, page header/footer and page numbers.
- Repeating table-header rows are configured for multi-page detail exports. Wide operational tables use landscape orientation; compact summaries may use portrait.
- Print settings are part of automated export tests and must not depend on Microsoft Excel being installed.

Operating summary invariants:
- Daily/monthly operating summaries distinguish gross sales, sales returns and net sales by business date.
- Show original amount, discount amount, order count, sales/return/net quantity, average order value, weighted effective discount, payment-channel net receipts and sale-time frozen reference cost/profit.
- Reference profit remains an operational estimate, never an accounting-profit claim.

Monthly purchase workbook invariants:
- Purchase month is based on the purchase document business date; purchase returns are deducted by return date.
- First sheet shows daily purchase, purchase-return and net-purchase metrics plus a month total.
- Include a supplier summary and one detail sheet for every calendar day in the month.
- Daily detail keeps purchase receiving and purchase-return sections distinct and vertically merges order-level fields across multi-line documents.

Monthly sales workbook invariants:
- First sheet is a readable month matrix with four frozen leading columns and up to 31 four-column day groups: day-shift quantity / received amount for white and night shifts.
- Keep gross sales, sales returns and net sales separate. Net sales is gross sales minus sales returns occurring in the report period; a later return must not retroactively alter the closed month's gross-sales row.
- Payment reconciliation is payment-method based: sales collection, refund and net receipt are separate rows.
- Preserve the approved template's core visual grammar: Songti, yellow structural cells, light-blue received-amount cells, explicit widths/heights, merged headers/sections, thin inner borders and stronger group/order separators.
- Every calendar day in the target month has its own detail sheet, even when it has no sales.
- Daily detail rows are sorted by operation time then order; order-level cells are vertically merged across multi-line orders.
- The first 30 daily-detail columns stay compatible with the approved template; system-specific payment/category fields may be appended, never inserted into those 30 positions.
- Monthly summary uses final order/line amounts, not cash tendered amount. Cash amount received and change remain order-level detail fields.
- Reference cost is an operational estimate frozen at sale time: use the most recent reviewed purchase cost at or before the sale, otherwise the then-current default purchase price. Label it explicitly as reference cost; it is not an accounting-cost method.
- Generic report exports must use readable fixed widths, frozen headers, filters, explicit numeric/date formats and visible table borders rather than raw AutoSize-only output.
- Report shift boundary, report store name and optional system-category → monthly-business-line mapping come from app settings, not hard-coded UI text.
- Category mapping entries use one `system category=monthly line` pair per line. Invalid targets must be rejected by the settings UI; unmapped values may use the documented fallback matcher.

## Testing expectations
At minimum, verify:
- legacy csproj source manifest validation passes, and both Debug x86 and Release x86 compile from a clean checkout;
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
7. This is a legacy non-SDK .NET Framework project. Every added or removed `.cs` file must be added to or removed from `Win7BookManagement.csproj` explicitly in the same change. Do not reintroduce wildcard `Compile Include="**\\*.cs"`; run `scripts/validate-csproj-sources.ps1` before merging.
