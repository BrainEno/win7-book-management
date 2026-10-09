from pathlib import Path


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    if old not in text:
        raise RuntimeError(f"Expected source block not found in {path}: {old[:100]!r}")
    if text.count(old) != 1:
        raise RuntimeError(f"Expected exactly one source block in {path}, found {text.count(old)}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")


# Shared transaction input semantics.
replace_once(
    "src/Win7BookManagement/Models/TransactionLineInput.cs",
    """        // Purchase uses UnitPriceCent as the unit cost. Sales uses it as the\n        // operator-entered pre-discount selling price for backward compatibility.\n        public long UnitPriceCent { get; set; }\n\n        // Sales-only discount metadata. Purchase services intentionally ignore\n        // these fields.\n""",
    """        // Backward-compatible operator-entered unit amount. New purchase and\n        // sales flows also preserve BaseUnitPriceCent plus discount metadata.\n        public long UnitPriceCent { get; set; }\n\n        // Shared discount metadata. 10000 basis points = 100.00%.\n        // Purchase treats BaseUnitPriceCent as pre-discount unit cost; sales\n        // treats it as pre-discount selling price.\n""",
)

# Purchase document snapshots.
replace_once(
    "src/Win7BookManagement/Models/PurchaseDocument.cs",
    """            SupplierName = \"\";\n            Status = \"draft\";\n            Note = \"\";\n""",
    """            SupplierName = \"\";\n            Status = \"draft\";\n            Note = \"\";\n            OrderDiscountBasisPoints = 10000;\n""",
)
replace_once(
    "src/Win7BookManagement/Models/PurchaseDocument.cs",
    """        public string SupplierName { get; set; }\n        public DateTime PurchasedAt { get; set; }\n        public long TotalCent { get; set; }\n""",
    """        public string SupplierName { get; set; }\n        public DateTime PurchasedAt { get; set; }\n        public long SubtotalCent { get; set; }\n        public long LineDiscountCent { get; set; }\n        public int OrderDiscountBasisPoints { get; set; }\n        public long OrderDiscountCent { get; set; }\n        public long TotalCent { get; set; }\n""",
)
replace_once(
    "src/Win7BookManagement/Models/PurchaseDocument.cs",
    """        public int CurrentStock { get; set; }\n        public int Quantity { get; set; }\n        public long UnitCostCent { get; set; }\n""",
    """        public int CurrentStock { get; set; }\n        public int Quantity { get; set; }\n        public long BaseUnitCostCent { get; set; }\n        public int LineDiscountBasisPoints { get; set; }\n        public long LineDiscountedUnitCostCent { get; set; }\n        public long UnitCostCent { get; set; }\n""",
)

# Schema v10: purchase discount snapshots and legacy normalization.
db = "src/Win7BookManagement/Database/DatabaseInitializer.cs"
replace_once(
    db,
    """    supplier_name_snapshot TEXT NOT NULL DEFAULT '',\n    purchased_at TEXT NOT NULL,\n    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),\n""",
    """    supplier_name_snapshot TEXT NOT NULL DEFAULT '',\n    purchased_at TEXT NOT NULL,\n    subtotal_cent INTEGER NOT NULL DEFAULT 0 CHECK(subtotal_cent >= 0),\n    line_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discount_cent >= 0),\n    order_discount_basis_points INTEGER NOT NULL DEFAULT 10000\n        CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000),\n    order_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(order_discount_cent >= 0),\n    total_cent INTEGER NOT NULL CHECK(total_cent >= 0),\n""",
)
replace_once(
    db,
    """    title_snapshot TEXT NOT NULL,\n    quantity INTEGER NOT NULL CHECK(quantity > 0),\n    unit_cost_cent INTEGER NOT NULL CHECK(unit_cost_cent >= 0),\n""",
    """    title_snapshot TEXT NOT NULL,\n    quantity INTEGER NOT NULL CHECK(quantity > 0),\n    base_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_cost_cent >= 0),\n    line_discount_basis_points INTEGER NOT NULL DEFAULT 10000\n        CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000),\n    line_discounted_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discounted_unit_cost_cent >= 0),\n    unit_cost_cent INTEGER NOT NULL CHECK(unit_cost_cent >= 0),\n""",
)
replace_once(
    db,
    """                    EnsureBookMetadataColumns(connection, transaction);\n                    EnsurePurchaseWorkflowColumns(connection, transaction);\n                    EnsureSalesDiscountColumns(connection, transaction);\n""",
    """                    EnsureBookMetadataColumns(connection, transaction);\n                    EnsurePurchaseWorkflowColumns(connection, transaction);\n                    EnsurePurchaseDiscountColumns(connection, transaction);\n                    EnsureSalesDiscountColumns(connection, transaction);\n""",
)
replace_once(
    db,
    """INSERT INTO schema_info(version)\nSELECT 9 WHERE NOT EXISTS (SELECT 1 FROM schema_info);\n\nUPDATE schema_info\nSET version = 9\nWHERE version < 9;\n""",
    """INSERT INTO schema_info(version)\nSELECT 10 WHERE NOT EXISTS (SELECT 1 FROM schema_info);\n\nUPDATE schema_info\nSET version = 10\nWHERE version < 10;\n""",
)
replace_once(
    db,
    """        private static void EnsureSalesDiscountColumns(\n            SQLiteConnection connection,\n            SQLiteTransaction transaction)\n""",
    """        private static void EnsurePurchaseDiscountColumns(\n            SQLiteConnection connection,\n            SQLiteTransaction transaction)\n        {\n            EnsureColumn(connection, transaction, \"purchase_orders\", \"subtotal_cent\",\n                \"ALTER TABLE purchase_orders ADD COLUMN subtotal_cent INTEGER NOT NULL DEFAULT 0 CHECK(subtotal_cent >= 0);\");\n            EnsureColumn(connection, transaction, \"purchase_orders\", \"line_discount_cent\",\n                \"ALTER TABLE purchase_orders ADD COLUMN line_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discount_cent >= 0);\");\n            EnsureColumn(connection, transaction, \"purchase_orders\", \"order_discount_basis_points\",\n                \"ALTER TABLE purchase_orders ADD COLUMN order_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(order_discount_basis_points >= 0 AND order_discount_basis_points <= 10000);\");\n            EnsureColumn(connection, transaction, \"purchase_orders\", \"order_discount_cent\",\n                \"ALTER TABLE purchase_orders ADD COLUMN order_discount_cent INTEGER NOT NULL DEFAULT 0 CHECK(order_discount_cent >= 0);\");\n\n            EnsureColumn(connection, transaction, \"purchase_order_items\", \"base_unit_cost_cent\",\n                \"ALTER TABLE purchase_order_items ADD COLUMN base_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(base_unit_cost_cent >= 0);\");\n            EnsureColumn(connection, transaction, \"purchase_order_items\", \"line_discount_basis_points\",\n                \"ALTER TABLE purchase_order_items ADD COLUMN line_discount_basis_points INTEGER NOT NULL DEFAULT 10000 CHECK(line_discount_basis_points >= 0 AND line_discount_basis_points <= 10000);\");\n            EnsureColumn(connection, transaction, \"purchase_order_items\", \"line_discounted_unit_cost_cent\",\n                \"ALTER TABLE purchase_order_items ADD COLUMN line_discounted_unit_cost_cent INTEGER NOT NULL DEFAULT 0 CHECK(line_discounted_unit_cost_cent >= 0);\");\n\n            using (var normalize = connection.CreateCommand())\n            {\n                normalize.Transaction = transaction;\n                normalize.CommandText = @\"\nUPDATE purchase_order_items\nSET base_unit_cost_cent = unit_cost_cent\nWHERE base_unit_cost_cent = 0 AND unit_cost_cent > 0;\n\nUPDATE purchase_order_items\nSET line_discounted_unit_cost_cent = unit_cost_cent\nWHERE line_discounted_unit_cost_cent = 0 AND unit_cost_cent > 0;\n\nUPDATE purchase_orders\nSET subtotal_cent = total_cent\nWHERE subtotal_cent = 0 AND total_cent > 0;\";\n                normalize.ExecuteNonQuery();\n            }\n        }\n\n        private static void EnsureSalesDiscountColumns(\n            SQLiteConnection connection,\n            SQLiteTransaction transaction)\n""",
)

# Purchase service discount pipeline.
svc = "src/Win7BookManagement/Services/PurchaseService.cs"
replace_once(
    svc,
    """        public PurchaseDocument SaveDraft(\n            long? documentId,\n            string requestedOrderNo,\n            DateTime purchaseDate,\n            long? supplierId,\n            IList<TransactionLineInput> lines,\n            string note)\n        {\n            var normalizedLines = lines ?? new List<TransactionLineInput>();\n            ValidateDraftLines(normalizedLines);\n\n            var now = DateTime.Now;\n            var timestamp = Format(now);\n            var purchasedAt = Format(purchaseDate.Date);\n            var totalCent = CalculateTotal(normalizedLines);\n""",
    """        public PurchaseDocument SaveDraft(\n            long? documentId,\n            string requestedOrderNo,\n            DateTime purchaseDate,\n            long? supplierId,\n            IList<TransactionLineInput> lines,\n            string note)\n        {\n            return SaveDraft(\n                documentId, requestedOrderNo, purchaseDate, supplierId, lines, note, 10000);\n        }\n\n        public PurchaseDocument SaveDraft(\n            long? documentId,\n            string requestedOrderNo,\n            DateTime purchaseDate,\n            long? supplierId,\n            IList<TransactionLineInput> lines,\n            string note,\n            int orderDiscountBasisPoints)\n        {\n            var normalizedLines = lines ?? new List<TransactionLineInput>();\n            ValidateDraftLines(normalizedLines);\n            if (orderDiscountBasisPoints < 0 || orderDiscountBasisPoints > 10000)\n                throw new InvalidOperationException(\"整单折扣必须在 0% 到 100% 之间。\");\n\n            var now = DateTime.Now;\n            var timestamp = Format(now);\n            var purchasedAt = Format(purchaseDate.Date);\n            var totals = CalculateTotals(normalizedLines, orderDiscountBasisPoints);\n""",
)
replace_once(
    svc,
    """    purchased_at=@purchasedAt,\n    total_cent=@total,\n    note=@note,\n""",
    """    purchased_at=@purchasedAt,\n    subtotal_cent=@subtotal,\n    line_discount_cent=@lineDiscount,\n    order_discount_basis_points=@orderDiscountBasisPoints,\n    order_discount_cent=@orderDiscount,\n    total_cent=@total,\n    note=@note,\n""",
)
replace_once(
    svc,
    """                            update.Parameters.AddWithValue(\"@purchasedAt\", purchasedAt);\n                            update.Parameters.AddWithValue(\"@total\", totalCent);\n""",
    """                            update.Parameters.AddWithValue(\"@purchasedAt\", purchasedAt);\n                            update.Parameters.AddWithValue(\"@subtotal\", totals.SubtotalCent);\n                            update.Parameters.AddWithValue(\"@lineDiscount\", totals.LineDiscountCent);\n                            update.Parameters.AddWithValue(\"@orderDiscountBasisPoints\", orderDiscountBasisPoints);\n                            update.Parameters.AddWithValue(\"@orderDiscount\", totals.OrderDiscountCent);\n                            update.Parameters.AddWithValue(\"@total\", totals.TotalCent);\n""",
)
replace_once(
    svc,
    """INSERT INTO purchase_orders\n(order_no, supplier_id, supplier_name_snapshot, purchased_at, total_cent, note,\n created_at, status, reviewed_at, updated_at)\nVALUES\n(@no, @supplierId, @supplierName, @purchasedAt, @total, @note,\n @createdAt, @status, NULL, @updatedAt);\n""",
    """INSERT INTO purchase_orders\n(order_no, supplier_id, supplier_name_snapshot, purchased_at,\n subtotal_cent, line_discount_cent, order_discount_basis_points, order_discount_cent,\n total_cent, note, created_at, status, reviewed_at, updated_at)\nVALUES\n(@no, @supplierId, @supplierName, @purchasedAt,\n @subtotal, @lineDiscount, @orderDiscountBasisPoints, @orderDiscount,\n @total, @note, @createdAt, @status, NULL, @updatedAt);\n""",
)
replace_once(
    svc,
    """                            insert.Parameters.AddWithValue(\"@purchasedAt\", purchasedAt);\n                            insert.Parameters.AddWithValue(\"@total\", totalCent);\n""",
    """                            insert.Parameters.AddWithValue(\"@purchasedAt\", purchasedAt);\n                            insert.Parameters.AddWithValue(\"@subtotal\", totals.SubtotalCent);\n                            insert.Parameters.AddWithValue(\"@lineDiscount\", totals.LineDiscountCent);\n                            insert.Parameters.AddWithValue(\"@orderDiscountBasisPoints\", orderDiscountBasisPoints);\n                            insert.Parameters.AddWithValue(\"@orderDiscount\", totals.OrderDiscountCent);\n                            insert.Parameters.AddWithValue(\"@total\", totals.TotalCent);\n""",
)
replace_once(
    svc,
    """                    InsertItems(connection, transaction, id, normalizedLines);\n""",
    """                    InsertItems(connection, transaction, id, normalizedLines, orderDiscountBasisPoints);\n""",
)
replace_once(
    svc,
    """SELECT id, order_no, supplier_id, supplier_name_snapshot, purchased_at,\n       total_cent, note, status, reviewed_at, updated_at\n""",
    """SELECT id, order_no, supplier_id, supplier_name_snapshot, purchased_at,\n       subtotal_cent, line_discount_cent, order_discount_basis_points,\n       order_discount_cent, total_cent, note, status, reviewed_at, updated_at\n""",
)
replace_once(
    svc,
    """                            SupplierName = Convert.ToString(reader[\"supplier_name_snapshot\"]),\n                            PurchasedAt = ParseDate(Convert.ToString(reader[\"purchased_at\"])),\n                            TotalCent = Convert.ToInt64(reader[\"total_cent\"]),\n""",
    """                            SupplierName = Convert.ToString(reader[\"supplier_name_snapshot\"]),\n                            PurchasedAt = ParseDate(Convert.ToString(reader[\"purchased_at\"])),\n                            SubtotalCent = Convert.ToInt64(reader[\"subtotal_cent\"]),\n                            LineDiscountCent = Convert.ToInt64(reader[\"line_discount_cent\"]),\n                            OrderDiscountBasisPoints = Convert.ToInt32(reader[\"order_discount_basis_points\"]),\n                            OrderDiscountCent = Convert.ToInt64(reader[\"order_discount_cent\"]),\n                            TotalCent = Convert.ToInt64(reader[\"total_cent\"]),\n""",
)
replace_once(
    svc,
    """       b.stock_quantity,\n       pi.quantity,\n       pi.unit_cost_cent,\n""",
    """       b.stock_quantity,\n       pi.quantity,\n       pi.base_unit_cost_cent,\n       pi.line_discount_basis_points,\n       pi.line_discounted_unit_cost_cent,\n       pi.unit_cost_cent,\n""",
)
replace_once(
    svc,
    """                                CurrentStock = Convert.ToInt32(reader[\"stock_quantity\"]),\n                                Quantity = Convert.ToInt32(reader[\"quantity\"]),\n                                UnitCostCent = Convert.ToInt64(reader[\"unit_cost_cent\"]),\n""",
    """                                CurrentStock = Convert.ToInt32(reader[\"stock_quantity\"]),\n                                Quantity = Convert.ToInt32(reader[\"quantity\"]),\n                                BaseUnitCostCent = Convert.ToInt64(reader[\"base_unit_cost_cent\"]),\n                                LineDiscountBasisPoints = Convert.ToInt32(reader[\"line_discount_basis_points\"]),\n                                LineDiscountedUnitCostCent = Convert.ToInt64(reader[\"line_discounted_unit_cost_cent\"]),\n                                UnitCostCent = Convert.ToInt64(reader[\"unit_cost_cent\"]),\n""",
)
replace_once(
    svc,
    """                    BookId = line.BookId,\n                    Quantity = line.Quantity,\n                    UnitPriceCent = line.UnitCostCent\n""",
    """                    BookId = line.BookId,\n                    Quantity = line.Quantity,\n                    UnitPriceCent = line.BaseUnitCostCent,\n                    BaseUnitPriceCent = line.BaseUnitCostCent,\n                    DiscountBasisPoints = line.LineDiscountBasisPoints\n""",
)
replace_once(
    svc,
    """                source.SupplierId,\n                lines,\n                \"\");\n""",
    """                source.SupplierId,\n                lines,\n                \"\",\n                source.OrderDiscountBasisPoints);\n""",
)
replace_once(
    svc,
    """                if (line.UnitPriceCent < 0)\n                    throw new InvalidOperationException(\"进价不能为负数。\");\n                if (!seen.Add(line.BookId))\n""",
    """                if (ResolveBaseUnitCost(line) < 0)\n                    throw new InvalidOperationException(\"进价不能为负数。\");\n                if (line.DiscountBasisPoints < 0 || line.DiscountBasisPoints > 10000)\n                    throw new InvalidOperationException(\"单品折扣必须在 0% 到 100% 之间。\");\n                if (!seen.Add(line.BookId))\n""",
)
replace_once(
    svc,
    """        private static long CalculateTotal(IList<TransactionLineInput> lines)\n        {\n            long total = 0;\n            foreach (var line in lines)\n                total = checked(total + checked((long)line.Quantity * line.UnitPriceCent));\n            return total;\n        }\n""",
    """        private static PurchaseTotals CalculateTotals(\n            IList<TransactionLineInput> lines,\n            int orderDiscountBasisPoints)\n        {\n            var result = new PurchaseTotals();\n            foreach (var line in lines)\n            {\n                var baseUnitCostCent = ResolveBaseUnitCost(line);\n                var lineDiscountedUnitCostCent =\n                    ApplyBasisPoints(baseUnitCostCent, line.DiscountBasisPoints);\n                var finalUnitCostCent =\n                    ApplyBasisPoints(lineDiscountedUnitCostCent, orderDiscountBasisPoints);\n\n                result.SubtotalCent = checked(\n                    result.SubtotalCent + checked((long)line.Quantity * baseUnitCostCent));\n                result.LineDiscountCent = checked(\n                    result.LineDiscountCent + checked(\n                        (long)line.Quantity * (baseUnitCostCent - lineDiscountedUnitCostCent)));\n                result.OrderDiscountCent = checked(\n                    result.OrderDiscountCent + checked(\n                        (long)line.Quantity * (lineDiscountedUnitCostCent - finalUnitCostCent)));\n                result.TotalCent = checked(\n                    result.TotalCent + checked((long)line.Quantity * finalUnitCostCent));\n            }\n            return result;\n        }\n\n        private static long ResolveBaseUnitCost(TransactionLineInput line)\n        {\n            return line.BaseUnitPriceCent > 0 || line.UnitPriceCent == 0\n                ? line.BaseUnitPriceCent\n                : line.UnitPriceCent;\n        }\n\n        private static long ApplyBasisPoints(long amountCent, int basisPoints)\n        {\n            if (amountCent <= 0 || basisPoints <= 0) return 0;\n            if (basisPoints >= 10000) return amountCent;\n\n            return decimal.ToInt64(decimal.Round(\n                amountCent * (basisPoints / 10000m),\n                0,\n                MidpointRounding.AwayFromZero));\n        }\n""",
)
replace_once(
    svc,
    """        private static void InsertItems(\n            SQLiteConnection connection,\n            SQLiteTransaction transaction,\n            long orderId,\n            IList<TransactionLineInput> lines)\n""",
    """        private static void InsertItems(\n            SQLiteConnection connection,\n            SQLiteTransaction transaction,\n            long orderId,\n            IList<TransactionLineInput> lines,\n            int orderDiscountBasisPoints)\n""",
)
replace_once(
    svc,
    """                var lineTotal = checked((long)line.Quantity * line.UnitPriceCent);\n                using (var item = connection.CreateCommand())\n                {\n                    item.Transaction = transaction;\n                    item.CommandText = @\"\nINSERT INTO purchase_order_items\n(purchase_order_id, book_id, isbn_snapshot, title_snapshot, quantity, unit_cost_cent, line_total_cent)\nVALUES(@orderId, @bookId, @isbn, @title, @qty, @unit, @total);\";\n""",
    """                var baseUnitCostCent = ResolveBaseUnitCost(line);\n                var lineDiscountedUnitCostCent =\n                    ApplyBasisPoints(baseUnitCostCent, line.DiscountBasisPoints);\n                var finalUnitCostCent =\n                    ApplyBasisPoints(lineDiscountedUnitCostCent, orderDiscountBasisPoints);\n                var lineTotal = checked((long)line.Quantity * finalUnitCostCent);\n                using (var item = connection.CreateCommand())\n                {\n                    item.Transaction = transaction;\n                    item.CommandText = @\"\nINSERT INTO purchase_order_items\n(purchase_order_id, book_id, isbn_snapshot, title_snapshot, quantity,\n base_unit_cost_cent, line_discount_basis_points, line_discounted_unit_cost_cent,\n unit_cost_cent, line_total_cent)\nVALUES(@orderId, @bookId, @isbn, @title, @qty,\n @baseUnit, @lineDiscountBasisPoints, @lineDiscountedUnit, @unit, @total);\";\n""",
)
replace_once(
    svc,
    """                    item.Parameters.AddWithValue(\"@title\", title);\n                    item.Parameters.AddWithValue(\"@qty\", line.Quantity);\n                    item.Parameters.AddWithValue(\"@unit\", line.UnitPriceCent);\n                    item.Parameters.AddWithValue(\"@total\", lineTotal);\n""",
    """                    item.Parameters.AddWithValue(\"@title\", title);\n                    item.Parameters.AddWithValue(\"@qty\", line.Quantity);\n                    item.Parameters.AddWithValue(\"@baseUnit\", baseUnitCostCent);\n                    item.Parameters.AddWithValue(\"@lineDiscountBasisPoints\", line.DiscountBasisPoints);\n                    item.Parameters.AddWithValue(\"@lineDiscountedUnit\", lineDiscountedUnitCostCent);\n                    item.Parameters.AddWithValue(\"@unit\", finalUnitCostCent);\n                    item.Parameters.AddWithValue(\"@total\", lineTotal);\n""",
)
replace_once(
    svc,
    """        private sealed class PostingLine\n""",
    """        private sealed class PurchaseTotals\n        {\n            public long SubtotalCent { get; set; }\n            public long LineDiscountCent { get; set; }\n            public long OrderDiscountCent { get; set; }\n            public long TotalCent { get; set; }\n        }\n\n        private sealed class PostingLine\n""",
)

# Purchase form UI / editing / export.
form = "src/Win7BookManagement/Forms/PurchaseForm.cs"
replace_once(form, "        private readonly AntdUI.Select _supplier = new AntdUI.Select();\n", "        private readonly AntdUI.Select _supplier = new AntdUI.Select();\n        private readonly AntdUI.InputNumber _orderDiscount = new AntdUI.InputNumber();\n")
replace_once(
    form,
    """        private readonly Label _lineCount = new Label();\n        private readonly Label _quantityTotal = new Label();\n        private readonly Label _total = new Label();\n""",
    """        private readonly Label _lineCount = new Label();\n        private readonly Label _quantityTotal = new Label();\n        private readonly Label _originalTotal = new Label();\n        private readonly Label _discountTotal = new Label();\n        private readonly Label _orderDiscountLabel = new Label();\n        private readonly Label _total = new Label();\n""",
)
replace_once(
    form,
    """        private readonly AntdUI.Column _quantityColumn;\n        private readonly AntdUI.Column _unitCostColumn;\n        private readonly AntdUI.Column _lineTotalColumn;\n""",
    """        private readonly AntdUI.Column _quantityColumn;\n        private readonly AntdUI.Column _unitCostColumn;\n        private readonly AntdUI.Column _discountColumn;\n        private readonly AntdUI.Column _discountedUnitCostColumn;\n        private readonly AntdUI.Column _lineTotalColumn;\n""",
)
replace_once(form, "        private string _baselineNote = \"\";\n", "        private string _baselineNote = \"\";\n        private int _baselineOrderDiscountBasisPoints = 10000;\n")
replace_once(
    form,
    """            _unitCostColumn = new AntdUI.Column(\"UnitCostYuan\", \"进价（元）\")\n            {\n                Width = \"108\",\n                MinWidth = \"92\",\n                ReadOnly = false,\n                DisplayFormat = \"0.00\",\n                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }\n            };\n            _lineTotalColumn = new AntdUI.Column(\"LineTotalYuan\", \"小计（元）\")\n""",
    """            _unitCostColumn = new AntdUI.Column(\"BaseUnitCostYuan\", \"原进价（元）\")\n            {\n                Width = \"108\",\n                MinWidth = \"92\",\n                ReadOnly = false,\n                DisplayFormat = \"0.00\",\n                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }\n            };\n            _discountColumn = new AntdUI.Column(\"DiscountPercent\", \"单品折扣%\")\n            {\n                Width = \"96\",\n                MinWidth = \"86\",\n                ReadOnly = false,\n                DisplayFormat = \"0.00\",\n                Style = new AntdUI.Table.CellStyleInfo { BackColor = UiTheme.AccentSoft }\n            };\n            _discountedUnitCostColumn = new AntdUI.Column(\"DiscountedUnitCostYuan\", \"折后进价\")\n            {\n                Width = \"96\",\n                MinWidth = \"86\",\n                ReadOnly = true,\n                DisplayFormat = \"0.00\"\n            };\n            _lineTotalColumn = new AntdUI.Column(\"LineTotalYuan\", \"小计（元）\")\n""",
)
replace_once(form, """            };\n\n            var root = new TableLayoutPanel\n""", """            };\n\n            ConfigureOrderDiscount();\n\n            var root = new TableLayoutPanel\n""",)
replace_once(
    form,
    """        private Control CreateReceivingSection()\n""",
    """        private void ConfigureOrderDiscount()\n        {\n            _orderDiscount.Minimum = 0m;\n            _orderDiscount.Maximum = 100m;\n            _orderDiscount.DecimalPlaces = 2;\n            _orderDiscount.Value = 100m;\n            _orderDiscount.Width = 112;\n            _orderDiscount.MinimumSize = new Size(96, UiTheme.InputHeight);\n            _orderDiscount.ValueChanged += delegate(object sender, AntdUI.DecimalEventArgs e)\n            {\n                foreach (var row in _rows)\n                    row.OrderDiscountPercent = _orderDiscount.Value;\n                if (!_loadingDocument) MarkDirty();\n                _grid.Refresh();\n                UpdateTotals();\n            };\n        }\n\n        private Control CreateReceivingSection()\n""",
)
replace_once(
    form,
    """                _quantityColumn,\n                _unitCostColumn,\n                _lineTotalColumn\n            };\n            _grid.ConfigureColumnPersistence(_services.Settings, \"purchase-lines-ui-spec-v5\");\n""",
    """                _quantityColumn,\n                _unitCostColumn,\n                _discountColumn,\n                _discountedUnitCostColumn,\n                _lineTotalColumn\n            };\n            _grid.ConfigureColumnPersistence(_services.Settings, \"purchase-lines-ui-spec-v6\");\n""",
)
replace_once(
    form,
    """                    !string.Equals(key, \"Quantity\", StringComparison.Ordinal) &&\n                    !string.Equals(key, \"UnitCostYuan\", StringComparison.Ordinal);\n""",
    """                    !string.Equals(key, \"Quantity\", StringComparison.Ordinal) &&\n                    !string.Equals(key, \"BaseUnitCostYuan\", StringComparison.Ordinal) &&\n                    !string.Equals(key, \"DiscountPercent\", StringComparison.Ordinal);\n""",
)
replace_once(
    form,
    """            else if (string.Equals(e.Column.Key, \"UnitCostYuan\", StringComparison.Ordinal))\n            {\n                decimal price;\n                if (!decimal.TryParse(e.Value, out price) || price < 0)\n                {\n                    MessageBox.Show(this, \"本次进价必须是有效的非负金额。\", \"金额格式不正确\", MessageBoxButtons.OK, MessageBoxIcon.Information);\n                    return false;\n                }\n                row.UnitCostYuan = price;\n            }\n""",
    """            else if (string.Equals(e.Column.Key, \"BaseUnitCostYuan\", StringComparison.Ordinal))\n            {\n                decimal price;\n                if (!decimal.TryParse(e.Value, out price) || price < 0)\n                {\n                    MessageBox.Show(this, \"本次进价必须是有效的非负金额。\", \"金额格式不正确\", MessageBoxButtons.OK, MessageBoxIcon.Information);\n                    return false;\n                }\n                row.BaseUnitCostYuan = price;\n            }\n            else if (string.Equals(e.Column.Key, \"DiscountPercent\", StringComparison.Ordinal))\n            {\n                decimal discount;\n                if (!decimal.TryParse(e.Value, out discount) || discount < 0m || discount > 100m)\n                {\n                    MessageBox.Show(this, \"单品折扣必须在 0 到 100 之间，可保留两位小数。\", \"折扣格式不正确\", MessageBoxButtons.OK, MessageBoxIcon.Information);\n                    return false;\n                }\n                row.DiscountPercent = discount;\n            }\n""",
)
replace_once(
    form,
    """            ConfigureSummaryLabel(_lineCount, false);\n            ConfigureSummaryLabel(_quantityTotal, false);\n            ConfigureSummaryLabel(_total, true);\n            _metrics.Controls.Add(_lineCount);\n            _metrics.Controls.Add(_quantityTotal);\n            _metrics.Controls.Add(_total);\n""",
    """            ConfigureSummaryLabel(_lineCount, false);\n            ConfigureSummaryLabel(_quantityTotal, false);\n            ConfigureSummaryLabel(_originalTotal, false);\n            ConfigureSummaryLabel(_discountTotal, false);\n            ConfigureSummaryLabel(_total, true);\n            _orderDiscountLabel.AutoSize = true;\n            _orderDiscountLabel.Text = \"整单折扣 %\";\n            _orderDiscountLabel.ForeColor = UiTheme.TextSecondary;\n            _orderDiscountLabel.Font = UiTheme.Font(8.2F, FontStyle.Bold);\n            _orderDiscountLabel.Margin = new Padding(0, 9, 6, 0);\n            _metrics.Controls.Add(_lineCount);\n            _metrics.Controls.Add(_quantityTotal);\n            _metrics.Controls.Add(_originalTotal);\n            _metrics.Controls.Add(_discountTotal);\n            _metrics.Controls.Add(_orderDiscountLabel);\n            _metrics.Controls.Add(_orderDiscount);\n            _metrics.Controls.Add(_total);\n""",
)
replace_once(
    form,
    """            _supplier.Height = fieldHeight;\n            _supplier.MinimumSize = new Size(0, fieldHeight);\n            _note.Height = profileControlHeight;\n""",
    """            _supplier.Height = fieldHeight;\n            _supplier.MinimumSize = new Size(0, fieldHeight);\n            _orderDiscount.Width = compact ? 96 : 112;\n            _orderDiscount.Height = profileControlHeight;\n            _orderDiscount.MinimumSize = new Size(compact ? 96 : 112, profileControlHeight);\n            _orderDiscount.Font = UiTheme.Font(profile.BodyFontPoints);\n            _note.Height = profileControlHeight;\n""",
)
replace_once(form, """            if (_metrics != null)\n                _metrics.WrapContents = false;\n""", """            if (_metrics != null)\n                _metrics.WrapContents = true;\n""")
replace_once(
    form,
    """            ResizeSummaryLabel(_lineCount, compact ? 84 : 110, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_quantityTotal, compact ? 94 : 126, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_total, compact ? 146 : 190, compact ? 34 : 40, profile, true);\n""",
    """            ResizeSummaryLabel(_lineCount, compact ? 84 : 110, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_quantityTotal, compact ? 94 : 126, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_originalTotal, compact ? 118 : 150, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_discountTotal, compact ? 112 : 142, compact ? 34 : 40, profile, false);\n            ResizeSummaryLabel(_total, compact ? 146 : 190, compact ? 34 : 40, profile, true);\n""",
)
replace_once(
    form,
    """            _quantityColumn.Visible = true;\n            _unitCostColumn.Visible = true;\n            _lineTotalColumn.Visible = true;\n""",
    """            _quantityColumn.Visible = true;\n            _unitCostColumn.Visible = true;\n            _discountColumn.Visible = true;\n            _discountedUnitCostColumn.Visible = !compact;\n            _lineTotalColumn.Visible = true;\n""",
)
replace_once(
    form,
    """            _quantityColumn.Width = compact ? \"70\" : \"82\";\n            _unitCostColumn.Width = compact ? \"92\" : \"108\";\n            _lineTotalColumn.Width = compact ? \"94\" : \"110\";\n""",
    """            _quantityColumn.Width = compact ? \"70\" : \"82\";\n            _unitCostColumn.Width = compact ? \"92\" : \"108\";\n            _discountColumn.Width = compact ? \"86\" : \"96\";\n            _discountedUnitCostColumn.Width = compact ? \"86\" : \"96\";\n            _lineTotalColumn.Width = compact ? \"94\" : \"110\";\n""",
)
replace_once(
    form,
    """                CurrentStock = book.StockQuantity,\n                Quantity = 1,\n                UnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent)\n""",
    """                CurrentStock = book.StockQuantity,\n                Quantity = 1,\n                BaseUnitCostYuan = Money.ToYuan(book.DefaultPurchasePriceCent),\n                DiscountPercent = 100m,\n                OrderDiscountPercent = _orderDiscount.Value\n""",
)
replace_once(form, """                _purchaseDate.Value = DateTime.Today;\n                _note.Text = \"\";\n                _rows.Clear();\n""", """                _purchaseDate.Value = DateTime.Today;\n                _note.Text = \"\";\n                _orderDiscount.Value = 100m;\n                _rows.Clear();\n""")
replace_once(form, "供应商、图书、数量和进价已复制。", "供应商、图书、数量、进价和折扣已复制。")
replace_once(
    form,
    """                _purchaseDate.Value = document.PurchasedAt.Date;\n                _note.Text = document.Note;\n                SelectSupplier(document.SupplierId, document.SupplierName);\n""",
    """                _purchaseDate.Value = document.PurchasedAt.Date;\n                _note.Text = document.Note;\n                _orderDiscount.Value = document.OrderDiscountBasisPoints / 100m;\n                SelectSupplier(document.SupplierId, document.SupplierName);\n""",
)
replace_once(
    form,
    """                        CurrentStock = line.CurrentStock,\n                        Quantity = line.Quantity,\n                        UnitCostYuan = Money.ToYuan(line.UnitCostCent)\n""",
    """                        CurrentStock = line.CurrentStock,\n                        Quantity = line.Quantity,\n                        BaseUnitCostYuan = Money.ToYuan(line.BaseUnitCostCent),\n                        DiscountPercent = line.LineDiscountBasisPoints / 100m,\n                        OrderDiscountPercent = document.OrderDiscountBasisPoints / 100m\n""",
)
replace_once(
    form,
    """                    SelectedSupplierId,\n                    BuildLineInputs(),\n                    _note.Text);\n""",
    """                    SelectedSupplierId,\n                    BuildLineInputs(),\n                    _note.Text,\n                    PercentToBasisPoints(_orderDiscount.Value));\n""",
)
replace_once(
    form,
    """            table.Columns.Add(\"数量\", typeof(int));\n            table.Columns.Add(\"进价（元）\", typeof(decimal));\n            table.Columns.Add(\"小计（元）\", typeof(decimal));\n""",
    """            table.Columns.Add(\"数量\", typeof(int));\n            table.Columns.Add(\"原进价（元）\", typeof(decimal));\n            table.Columns.Add(\"单品折扣%\", typeof(decimal));\n            table.Columns.Add(\"折后进价（元）\", typeof(decimal));\n            table.Columns.Add(\"整单折扣%\", typeof(decimal));\n            table.Columns.Add(\"实际进价（元）\", typeof(decimal));\n            table.Columns.Add(\"小计（元）\", typeof(decimal));\n""",
)
replace_once(
    form,
    """                    document.SupplierName,\n                    \"\", \"\", \"\", \"\", \"\", 0, 0m, 0m, document.Note);\n""",
    """                    document.SupplierName,\n                    \"\", \"\", \"\", \"\", \"\", 0,\n                    0m, 100m, 0m, document.OrderDiscountBasisPoints / 100m,\n                    0m, 0m, document.Note);\n""",
)
replace_once(
    form,
    """                    line.Publisher,\n                    line.Quantity,\n                    Money.ToYuan(line.UnitCostCent),\n                    Money.ToYuan(line.LineTotalCent),\n""",
    """                    line.Publisher,\n                    line.Quantity,\n                    Money.ToYuan(line.BaseUnitCostCent),\n                    line.LineDiscountBasisPoints / 100m,\n                    Money.ToYuan(line.LineDiscountedUnitCostCent),\n                    document.OrderDiscountBasisPoints / 100m,\n                    Money.ToYuan(line.UnitCostCent),\n                    Money.ToYuan(line.LineTotalCent),\n""",
)
replace_once(
    form,
    """                if (row.UnitCostYuan < 0)\n                    throw new InvalidOperationException(\"《\" + row.Title + \"》的进价不能为负数。\");\n\n                result.Add(new TransactionLineInput\n                {\n                    BookId = row.BookId,\n                    Quantity = row.Quantity,\n                    UnitPriceCent = Money.FromYuan(row.UnitCostYuan)\n                });\n""",
    """                if (row.BaseUnitCostYuan < 0)\n                    throw new InvalidOperationException(\"《\" + row.Title + \"》的进价不能为负数。\");\n                if (row.DiscountPercent < 0m || row.DiscountPercent > 100m)\n                    throw new InvalidOperationException(\"《\" + row.Title + \"》的单品折扣必须在 0 到 100 之间。\");\n\n                var baseCent = Money.FromYuan(row.BaseUnitCostYuan);\n                result.Add(new TransactionLineInput\n                {\n                    BookId = row.BookId,\n                    Quantity = row.Quantity,\n                    UnitPriceCent = baseCent,\n                    BaseUnitPriceCent = baseCent,\n                    DiscountBasisPoints = PercentToBasisPoints(row.DiscountPercent)\n                });\n""",
)
replace_once(form, """            return SelectedPurchaseDate != _baselinePurchaseDate ||\n                   SelectedSupplierId != _baselineSupplierId ||\n""", """            return SelectedPurchaseDate != _baselinePurchaseDate ||\n                   SelectedSupplierId != _baselineSupplierId ||\n                   PercentToBasisPoints(_orderDiscount.Value) != _baselineOrderDiscountBasisPoints ||\n""")
replace_once(form, """            _baselineSupplierId = SelectedSupplierId;\n            _baselineNote = (_note.Text ?? \"\").Trim();\n""", """            _baselineSupplierId = SelectedSupplierId;\n            _baselineNote = (_note.Text ?? \"\").Trim();\n            _baselineOrderDiscountBasisPoints = PercentToBasisPoints(_orderDiscount.Value);\n""")
replace_once(form, """            _orderNo.Enabled = editable;\n            _supplier.Enabled = editable;\n            _note.Enabled = editable;\n""", """            _orderNo.Enabled = editable;\n            _supplier.Enabled = editable;\n            _orderDiscount.Enabled = editable;\n            _note.Enabled = editable;\n""")
replace_once(form, """            _quantityColumn.ReadOnly = !editable;\n            _unitCostColumn.ReadOnly = !editable;\n""", """            _quantityColumn.ReadOnly = !editable;\n            _unitCostColumn.ReadOnly = !editable;\n            _discountColumn.ReadOnly = !editable;\n""")
replace_once(
    form,
    """        private void UpdateTotals()\n        {\n            decimal total = 0m;\n            var quantity = 0;\n            var index = 1;\n            foreach (var row in _rows)\n            {\n                row.Index = index++;\n                total += row.Quantity * row.UnitCostYuan;\n                quantity += row.Quantity;\n            }\n\n            _lineCount.Text = \"图书项  \" + _rows.Count;\n            _quantityTotal.Text = \"入库册数  \" + quantity;\n            _total.Text = \"采购金额  ¥\" + total.ToString(\"0.00\");\n""",
    """        private void UpdateTotals()\n        {\n            long originalCent = 0;\n            long totalCent = 0;\n            var quantity = 0;\n            var index = 1;\n            foreach (var row in _rows)\n            {\n                row.Index = index++;\n                row.OrderDiscountPercent = _orderDiscount.Value;\n                var baseCent = Money.FromYuan(row.BaseUnitCostYuan);\n                var lineCent = ApplyBasisPoints(baseCent, PercentToBasisPoints(row.DiscountPercent));\n                var finalCent = ApplyBasisPoints(lineCent, PercentToBasisPoints(_orderDiscount.Value));\n                originalCent = checked(originalCent + checked((long)row.Quantity * baseCent));\n                totalCent = checked(totalCent + checked((long)row.Quantity * finalCent));\n                quantity += row.Quantity;\n            }\n\n            _lineCount.Text = \"图书项  \" + _rows.Count;\n            _quantityTotal.Text = \"入库册数  \" + quantity;\n            _originalTotal.Text = \"原金额  ¥\" + Money.ToYuan(originalCent).ToString(\"0.00\");\n            _discountTotal.Text = \"优惠  ¥\" + Money.ToYuan(originalCent - totalCent).ToString(\"0.00\");\n            _total.Text = \"采购金额  ¥\" + Money.ToYuan(totalCent).ToString(\"0.00\");\n""",
)
replace_once(
    form,
    """            public int CurrentStock { get; set; }\n            public int Quantity { get; set; }\n            public decimal UnitCostYuan { get; set; }\n            public decimal LineTotalYuan { get { return Quantity * UnitCostYuan; } }\n        }\n    }\n}\n""",
    """            public int CurrentStock { get; set; }\n            public int Quantity { get; set; }\n            public decimal BaseUnitCostYuan { get; set; }\n            public decimal DiscountPercent { get; set; }\n            public decimal OrderDiscountPercent { get; set; }\n\n            public decimal DiscountedUnitCostYuan\n            {\n                get\n                {\n                    var baseCent = Money.FromYuan(BaseUnitCostYuan);\n                    return Money.ToYuan(\n                        ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent)));\n                }\n            }\n\n            public decimal LineTotalYuan\n            {\n                get\n                {\n                    var baseCent = Money.FromYuan(BaseUnitCostYuan);\n                    var lineCent = ApplyBasisPoints(baseCent, PercentToBasisPoints(DiscountPercent));\n                    var finalCent = ApplyBasisPoints(lineCent, PercentToBasisPoints(OrderDiscountPercent));\n                    return Money.ToYuan(checked((long)Quantity * finalCent));\n                }\n            }\n        }\n\n        private static int PercentToBasisPoints(decimal percent)\n        {\n            var clamped = Math.Max(0m, Math.Min(100m, percent));\n            return decimal.ToInt32(decimal.Round(\n                clamped * 100m, 0, MidpointRounding.AwayFromZero));\n        }\n\n        private static long ApplyBasisPoints(long amountCent, int basisPoints)\n        {\n            if (amountCent <= 0 || basisPoints <= 0) return 0;\n            if (basisPoints >= 10000) return amountCent;\n            return decimal.ToInt64(decimal.Round(\n                amountCent * (basisPoints / 10000m),\n                0, MidpointRounding.AwayFromZero));\n        }\n    }\n}\n""",
)

# Reports and document center expose purchase discount snapshots.
report = "src/Win7BookManagement/Repositories/ReportRepository.cs"
replace_once(
    report,
    """       pi.quantity AS 数量,\n       ROUND(pi.unit_cost_cent / 100.0, 2) AS 进价,\n       ROUND(pi.line_total_cent / 100.0, 2) AS 金额\n""",
    """       pi.quantity AS 数量,\n       ROUND(pi.base_unit_cost_cent / 100.0, 2) AS 原进价,\n       ROUND(pi.line_discount_basis_points / 100.0, 2) AS [单品折扣%],\n       ROUND(pi.line_discounted_unit_cost_cent / 100.0, 2) AS 折后进价,\n       ROUND(po.order_discount_basis_points / 100.0, 2) AS [整单折扣%],\n       ROUND(pi.unit_cost_cent / 100.0, 2) AS 实际进价,\n       ROUND(pi.line_total_cent / 100.0, 2) AS 金额\n""",
)
replace_once(
    report,
    """       pi.quantity AS quantity,\n       pi.unit_cost_cent AS unit_cost_cent,\n       pi.line_total_cent AS line_total_cent,\n       COALESCE((\n""",
    """       pi.quantity AS quantity,\n       pi.base_unit_cost_cent AS base_unit_cost_cent,\n       pi.line_discount_basis_points AS line_discount_basis_points,\n       pi.line_discounted_unit_cost_cent AS line_discounted_unit_cost_cent,\n       pi.unit_cost_cent AS unit_cost_cent,\n       pi.line_total_cent AS line_total_cent,\n       po.subtotal_cent AS order_subtotal_cent,\n       po.line_discount_cent AS order_line_discount_cent,\n       po.order_discount_basis_points AS order_discount_basis_points,\n       po.order_discount_cent AS order_discount_cent,\n       COALESCE((\n""",
)

doc = "src/Win7BookManagement/Repositories/DocumentRepository.cs"
replace_once(
    doc,
    """       pi.quantity-COALESCE((SELECT SUM(pri.quantity) FROM purchase_return_items pri WHERE pri.source_purchase_order_item_id=pi.id),0) AS 可退,\n       ROUND(pi.unit_cost_cent/100.0,2) AS 进价,\n       ROUND(pi.line_total_cent/100.0,2) AS 金额\nFROM purchase_order_items pi\nWHERE pi.purchase_order_id=@id\n""",
    """       pi.quantity-COALESCE((SELECT SUM(pri.quantity) FROM purchase_return_items pri WHERE pri.source_purchase_order_item_id=pi.id),0) AS 可退,\n       ROUND(pi.base_unit_cost_cent/100.0,2) AS 原进价,\n       ROUND(pi.line_discount_basis_points/100.0,2) AS [单品折扣%],\n       ROUND(pi.line_discounted_unit_cost_cent/100.0,2) AS 折后进价,\n       ROUND(po.order_discount_basis_points/100.0,2) AS [整单折扣%],\n       ROUND(pi.unit_cost_cent/100.0,2) AS 实际进价,\n       ROUND(pi.line_total_cent/100.0,2) AS 金额\nFROM purchase_order_items pi\nJOIN purchase_orders po ON po.id=pi.purchase_order_id\nWHERE pi.purchase_order_id=@id\n""",
)

# Purchase monthly workbook: retain existing columns, append discount audit columns.
monthly = "src/Win7BookManagement/Reporting/PurchaseMonthlyExcelExporter.cs"
replace_once(
    monthly,
    """            \"数量\", \"进价\", \"金额\", \"当月退货数量\", \"月末累计已退\",\n            \"净入库数量\", \"复核时间\", \"单据备注\"\n""",
    """            \"数量\", \"进价\", \"金额\", \"当月退货数量\", \"月末累计已退\",\n            \"净入库数量\", \"复核时间\", \"单据备注\",\n            \"原进价\", \"单品折扣%\", \"折后进价\", \"整单折扣%\",\n            \"单据原金额\", \"单品优惠\", \"整单优惠\"\n""",
)
replace_once(
    monthly,
    """            7, 13, 22, 18, 18, 30, 9, 11, 12, 13, 13, 12, 20, 28\n""",
    """            7, 13, 22, 18, 18, 30, 9, 11, 12, 13, 13, 12, 20, 28,\n            11, 12, 11, 12, 13, 12, 12\n""",
)
replace_once(
    monthly,
    """                    WriteDetail(\n                        sheet, styles, excelRow, 13,\n                        first ? source.OrderNote : null,\n                        \"@\", top, bottom, HorizontalAlignment.Left, true);\n                    excelRow++;\n""",
    """                    WriteDetail(\n                        sheet, styles, excelRow, 13,\n                        first ? source.OrderNote : null,\n                        \"@\", top, bottom, HorizontalAlignment.Left, true);\n                    WriteDetail(sheet, styles, excelRow, 14,\n                        source.BaseUnitCostCent / 100.0, \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 15,\n                        source.LineDiscountBasisPoints / 100.0, \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 16,\n                        source.LineDiscountedUnitCostCent / 100.0, \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 17,\n                        first ? (object)(source.OrderDiscountBasisPoints / 100.0) : null,\n                        \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 18,\n                        first ? (object)(source.OrderSubtotalCent / 100.0) : null,\n                        \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 19,\n                        first ? (object)(source.OrderLineDiscountCent / 100.0) : null,\n                        \"0.00\", top, bottom);\n                    WriteDetail(sheet, styles, excelRow, 20,\n                        first ? (object)(source.OrderDiscountCent / 100.0) : null,\n                        \"0.00\", top, bottom);\n                    excelRow++;\n""",
)
replace_once(monthly, """                    foreach (var column in new[] { 0, 1, 2, 3, 12, 13 })\n""", """                    foreach (var column in new[] { 0, 1, 2, 3, 12, 13, 17, 18, 19, 20 })\n""")
replace_once(
    monthly,
    """                    Quantity = ToInt(row[\"quantity\"]),\n                    UnitCostCent = ToLong(row[\"unit_cost_cent\"]),\n                    LineTotalCent = ToLong(row[\"line_total_cent\"]),\n""",
    """                    Quantity = ToInt(row[\"quantity\"]),\n                    BaseUnitCostCent = ToLong(row[\"base_unit_cost_cent\"]),\n                    LineDiscountBasisPoints = ToInt(row[\"line_discount_basis_points\"]),\n                    LineDiscountedUnitCostCent = ToLong(row[\"line_discounted_unit_cost_cent\"]),\n                    UnitCostCent = ToLong(row[\"unit_cost_cent\"]),\n                    LineTotalCent = ToLong(row[\"line_total_cent\"]),\n                    OrderSubtotalCent = ToLong(row[\"order_subtotal_cent\"]),\n                    OrderLineDiscountCent = ToLong(row[\"order_line_discount_cent\"]),\n                    OrderDiscountBasisPoints = ToInt(row[\"order_discount_basis_points\"]),\n                    OrderDiscountCent = ToLong(row[\"order_discount_cent\"]),\n""",
)
replace_once(
    monthly,
    """            public int Quantity { get; set; }\n            public long UnitCostCent { get; set; }\n            public long LineTotalCent { get; set; }\n""",
    """            public int Quantity { get; set; }\n            public long BaseUnitCostCent { get; set; }\n            public int LineDiscountBasisPoints { get; set; }\n            public long LineDiscountedUnitCostCent { get; set; }\n            public long UnitCostCent { get; set; }\n            public long LineTotalCent { get; set; }\n            public long OrderSubtotalCent { get; set; }\n            public long OrderLineDiscountCent { get; set; }\n            public int OrderDiscountBasisPoints { get; set; }\n            public long OrderDiscountCent { get; set; }\n""",
)

# Self-test uses a discounted purchase that has the same final cost as the old fixture.
test = "src/Win7BookManagement/Infrastructure/SelfTest.cs"
replace_once(
    test,
    """                    new List<TransactionLineInput>\n                    {\n                        new TransactionLineInput { BookId = bookId, Quantity = 4, UnitPriceCent = 1000 }\n                    },\n                    \"purchase\");\n                if (services.Books.GetById(bookId).StockQuantity != 5 ||\n                    purchaseDraft.IsReviewed ||\n                    !purchaseDraft.OrderNo.StartsWith(DateTime.Today.ToString(\"yyyyMMdd\"), StringComparison.Ordinal))\n                    throw new InvalidOperationException(\"采购草稿保存或自动单号自检失败。\");\n""",
    """                    new List<TransactionLineInput>\n                    {\n                        new TransactionLineInput\n                        {\n                            BookId = bookId,\n                            Quantity = 4,\n                            UnitPriceCent = 2000,\n                            BaseUnitPriceCent = 2000,\n                            DiscountBasisPoints = 8000\n                        }\n                    },\n                    \"purchase\",\n                    6250);\n                if (services.Books.GetById(bookId).StockQuantity != 5 ||\n                    purchaseDraft.IsReviewed ||\n                    purchaseDraft.SubtotalCent != 8000 ||\n                    purchaseDraft.LineDiscountCent != 1600 ||\n                    purchaseDraft.OrderDiscountBasisPoints != 6250 ||\n                    purchaseDraft.OrderDiscountCent != 2400 ||\n                    purchaseDraft.TotalCent != 4000 ||\n                    !purchaseDraft.OrderNo.StartsWith(DateTime.Today.ToString(\"yyyyMMdd\"), StringComparison.Ordinal))\n                    throw new InvalidOperationException(\"采购折扣草稿保存或自动单号自检失败。\");\n""",
)
replace_once(
    test,
    """                    copiedPurchaseDraft.Lines[0].BookId != bookId ||\n                    copiedPurchaseDraft.Lines[0].Quantity != 4 ||\n                    copiedPurchaseDraft.Lines[0].UnitCostCent != 1000 ||\n""",
    """                    copiedPurchaseDraft.Lines[0].BookId != bookId ||\n                    copiedPurchaseDraft.Lines[0].Quantity != 4 ||\n                    copiedPurchaseDraft.OrderDiscountBasisPoints != 6250 ||\n                    copiedPurchaseDraft.Lines[0].BaseUnitCostCent != 2000 ||\n                    copiedPurchaseDraft.Lines[0].LineDiscountBasisPoints != 8000 ||\n                    copiedPurchaseDraft.Lines[0].LineDiscountedUnitCostCent != 1600 ||\n                    copiedPurchaseDraft.Lines[0].UnitCostCent != 1000 ||\n""",
)
replace_once(
    test,
    """                    throw new InvalidOperationException(\"原销售单退货状态 / 折扣快照自检失败。\");\n\n                try\n""",
    """                    throw new InvalidOperationException(\"原销售单退货状态 / 折扣快照自检失败。\");\n\n                var purchaseItems = services.Documents.GetItems(\"purchase\", purchaseId);\n                if (purchaseItems.Rows.Count != 1 ||\n                    Convert.ToDecimal(purchaseItems.Rows[0][\"原进价\"]) != 20.00m ||\n                    Convert.ToDecimal(purchaseItems.Rows[0][\"单品折扣%\"]) != 80.00m ||\n                    Convert.ToDecimal(purchaseItems.Rows[0][\"折后进价\"]) != 16.00m ||\n                    Convert.ToDecimal(purchaseItems.Rows[0][\"整单折扣%\"]) != 62.50m ||\n                    Convert.ToDecimal(purchaseItems.Rows[0][\"实际进价\"]) != 10.00m)\n                    throw new InvalidOperationException(\"采购折扣快照 / 单据中心展示自检失败。\");\n\n                try\n""",
)
replace_once(
    test,
    """                            Math.Abs(purchaseDay.GetRow(3).GetCell(8).NumericCellValue - 40.00) > 0.001 ||\n                            purchaseDay.GetRow(5).GetCell(0).StringCellValue != \"采购退货明细\" ||\n""",
    """                            Math.Abs(purchaseDay.GetRow(3).GetCell(8).NumericCellValue - 40.00) > 0.001 ||\n                            purchaseDay.GetRow(2).GetCell(14).StringCellValue != \"原进价\" ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(14).NumericCellValue - 20.00) > 0.001 ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(15).NumericCellValue - 80.00) > 0.001 ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(16).NumericCellValue - 16.00) > 0.001 ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(17).NumericCellValue - 62.50) > 0.001 ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(18).NumericCellValue - 80.00) > 0.001 ||\n                            Math.Abs(purchaseDay.GetRow(3).GetCell(20).NumericCellValue - 24.00) > 0.001 ||\n                            purchaseDay.GetRow(5).GetCell(0).StringCellValue != \"采购退货明细\" ||\n""",
)

# Installer: launch immediately after a successful first-time .NET install unless restart is required.
iss = "installer/win7-book-management.iss"
replace_once(iss, '#define AppVersion "0.4.0"', '#define AppVersion "0.5.0"')
replace_once(iss, """var\n  DotNetWasMissing: Boolean;\n""", """var\n  DotNetWasMissing: Boolean;\n  DotNetInstallSucceeded: Boolean;\n  DotNetRestartRequired: Boolean;\n""")
replace_once(iss, """  DotNetWasMissing := not IsDotNet48Installed;\n  Result := True;\n""", """  DotNetWasMissing := not IsDotNet48Installed;\n  DotNetInstallSucceeded := not DotNetWasMissing;\n  DotNetRestartRequired := False;\n  Result := True;\n""")
replace_once(iss, """  if ResultCode = 0 then\n  begin\n    Result := '';\n  end\n  else if (ResultCode = 3010) or (ResultCode = 1641) then\n  begin\n    NeedsRestart := True;\n    Result := '';\n""", """  if ResultCode = 0 then\n  begin\n    DotNetInstallSucceeded := True;\n    Result := '';\n  end\n  else if (ResultCode = 3010) or (ResultCode = 1641) then\n  begin\n    DotNetInstallSucceeded := True;\n    DotNetRestartRequired := True;\n    NeedsRestart := True;\n    Result := '';\n""")
replace_once(iss, """  Result := not DotNetWasMissing;\n""", """  Result := DotNetInstallSucceeded and (not DotNetRestartRequired);\n""")

# Documentation invariants / behavior.
readme = "README.md"
replace_once(
    readme,
    "采购入库（供应商可选且默认“不区分”；采购日期/采购单号；草稿保存；复核后才正式入库；",
    "采购入库（供应商可选且默认“不区分”；采购日期/采购单号；支持单品折扣%与整单折扣%，保存原进价、折后进价、实际进价和优惠快照；草稿保存；复核后才正式入库；",
)
replace_once(
    readme,
    "按草稿/已复核筛选、复制任意历史单据为今天的新草稿，并可删除从未产生库存流水的废弃草稿，",
    "按草稿/已复核筛选、复制任意历史单据为今天的新草稿并保留折扣，并可删除从未产生库存流水的废弃草稿，",
)
replace_once(
    readme,
    "曾复核/反复核过的单据强制保留；Ctrl+S 保存草稿",
    "曾复核/反复核过的单据强制保留；采购退货始终按原采购实际进价退款；Ctrl+S 保存草稿",
)
replace_once(
    readme,
    "采购每日明细按单号分组，同一单号的日期、采购单号、供应商、复核时间和备注自动合并，并展示当月退货数量、月末累计已退和净入库数量。",
    "采购每日明细按单号分组，同一单号的日期、采购单号、供应商、复核时间和备注自动合并，并展示当月退货数量、月末累计已退和净入库数量；附加原进价、单品折扣、折后进价、整单折扣及优惠快照用于对账。",
)

agents = "AGENTS.md"
replace_once(
    agents,
    """- `sales_order_items.unit_price_cent` is the actual final unit price paid after all discounts. Sales returns must continue to refund from that immutable snapshot.\n""",
    """- `sales_order_items.unit_price_cent` is the actual final unit price paid after all discounts. Sales returns must continue to refund from that immutable snapshot.\n- Purchase discounts follow the same basis-point and rounding rules as sales: pre-discount unit cost → line discount → whole-order discount.\n- `purchase_order_items.unit_cost_cent` is the final actual unit cost after all purchase discounts. Purchase returns and sale-time recent-purchase reference cost must use this immutable final-cost snapshot.\n- Legacy purchase rows without discount metadata migrate as 100% line / 100% order discount without changing their historical final amounts.\n""",
)

print("Purchase discount + installer source transformation completed.")
