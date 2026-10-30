using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed partial class PurchaseForm
    {
private Control CreateReceivingSection()
        {
            _receivingSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Background,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            _receivingSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _receivingSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _actionRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseTopToolbarStandardHeight),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.Background,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _newOrderButton = CreateToolbarButton("新入库单", 118, true, delegate { StartNewOrder(); });
            _newOrderButton.IconSvg = "FileAddOutlined";
            _historyButton = CreateToolbarButton("历史单据", 112, false, delegate { OpenHistory(); });
            _previousButton = CreateToolbarButton("上一张", 96, false, delegate { NavigateAdjacent(false); });
            _nextButton = CreateToolbarButton("下一张", 96, false, delegate { NavigateAdjacent(true); });
            _exportButton = CreateToolbarButton("导出 Excel", 118, false, delegate { ExportCurrent(); });
            _lookupButton = CreateToolbarButton("查找图书", 112, false, delegate { OpenAdvancedBookLookup(""); });
            _lookupButton.IconSvg = "SearchOutlined";
            _clearButton = CreateToolbarButton("清空明细", 118, false, delegate { ClearCartWithConfirmation(); });

            _actionRow.Controls.Add(_newOrderButton);
            _actionRow.Controls.Add(_historyButton);
            _actionRow.Controls.Add(_previousButton);
            _actionRow.Controls.Add(_nextButton);
            _actionRow.Controls.Add(_exportButton);
            _actionRow.Controls.Add(_lookupButton);
            _actionRow.Controls.Add(_clearButton);

            _documentSection = new UiSpecSectionPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseDocumentStandardHeight),
                ColumnCount = 1,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 28, 14, 28),
                Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0)
            };
            _documentSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _documentSection.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _headerFields = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 5,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _headerFields.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _purchaseDate.Tag = "toolbar-input";
            _orderNo.Tag = "toolbar-input";
            _supplier.DropDownArrow = true;
            _supplier.Tag = "toolbar-input";

            var dateField = CreateField(
                "采购日期",
                _purchaseDate,
                BookDeskUiSpec.PurchaseDateLabelWidth,
                206);
            var orderField = CreateField(
                "采购单号",
                _orderNo,
                BookDeskUiSpec.PurchaseOrderNoLabelWidth,
                258);
            var supplierField = CreateField(
                "供应商",
                _supplier,
                BookDeskUiSpec.PurchaseSupplierLabelWidth,
                BookDeskUiSpec.PurchaseSupplierStandardWidth);

            _headerFields.Controls.Add(dateField, 0, 0);
            _headerFields.Controls.Add(orderField, 1, 0);
            _headerFields.Controls.Add(supplierField, 2, 0);

            _statusLabel.AutoSize = false;
            _statusLabel.Size = new Size(118, BookDeskUiSpec.PurchaseFieldStandardHeight);
            _statusLabel.MinimumSize = new Size(118, BookDeskUiSpec.PurchaseFieldStandardHeight);
            _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            _statusLabel.Font = UiTheme.Font(8.5F, FontStyle.Bold);
            _statusLabel.Margin = Padding.Empty;
            _headerFields.Controls.Add(_statusLabel, 4, 0);
            _documentSection.Controls.Add(_headerFields, 0, 0);

            _receivingSection.Controls.Add(_actionRow, 0, 0);
            _receivingSection.Controls.Add(_documentSection, 0, 1);
            return _receivingSection;
        }

private AntdUI.Button CreateToolbarButton(string text, int width, bool primary, Action action)
        {
            var button = UiTheme.CreateAntdButton(text, primary);
            button.Width = width;
            button.Tag = "toolbar-action";
            button.Margin = new Padding(0, 0, BookDeskUiSpec.Standard.ControlGap, 0);
            button.Click += delegate { action(); };
            return button;
        }

private static Control CreateField(string labelText, Control control, int labelWidth, int controlWidth)
        {
            var host = new TableLayoutPanel
            {
                Width = labelWidth + controlWidth,
                Height = BookDeskUiSpec.PurchaseFieldStandardHeight,
                MinimumSize = new Size(
                    labelWidth + controlWidth,
                    BookDeskUiSpec.PurchaseFieldStandardHeight),
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 0, 10, 0),
                Padding = Padding.Empty
            };
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            host.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            host.Controls.Add(new Label
            {
                Text = labelText,
                AutoSize = false,
                Width = labelWidth,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.4F, FontStyle.Bold),
                Margin = Padding.Empty
            }, 0, 0);

            control.Dock = DockStyle.Fill;
            control.Margin = Padding.Empty;
            host.Controls.Add(control, 1, 0);
            return host;
        }

private Control CreateCartSection()
        {
            ConfigureGrid();

            _cartHost = new UiSpecSectionPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = UiTheme.Surface,
                Margin = new Padding(0, BookDeskUiSpec.Standard.SectionGap, 0, 0)
            };
            _cartHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cartHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _cartHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _cartHeader = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseCartHeaderHeight),
                ColumnCount = 3,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(12, 5, 12, 5),
                Margin = Padding.Empty
            };
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _cartHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _cartHeader.Controls.Add(new Label
            {
                Text = "入库明细",
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UiTheme.Font(10F, FontStyle.Bold),
                ForeColor = UiTheme.TextPrimary
            }, 0, 0);

            _cartHeader.Controls.Add(new Label
            {
                Text = "点击明细表后可直接扫码；未匹配会自动打开高级查找",
                AutoSize = true,
                Anchor = AnchorStyles.Right,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8F),
                Margin = new Padding(8, 6, 12, 0)
            }, 1, 0);

            _removeButton = UiTheme.CreateAntdButton("移除选中", false);
            _removeButton.Width = 106;
            _removeButton.Click += delegate { RemoveSelected(); };
            _cartHeader.Controls.Add(_removeButton, 2, 0);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _emptyState.Dock = DockStyle.None;
            _emptyState.TextAlign = ContentAlignment.MiddleCenter;
            _emptyState.Text = "当前入库单为空\r\n点击明细表后可直接扫码；也可点击上方“查找图书”";
            _emptyState.ForeColor = UiTheme.TextSecondary;
            _emptyState.BackColor = UiTheme.Surface;
            _emptyState.Font = UiTheme.Font(BookDeskUiSpec.Standard.SecondaryFontPoints);
            _emptyState.Margin = Padding.Empty;
            _emptyState.Padding = Padding.Empty;
            _emptyState.Cursor = Cursors.IBeam;
            _emptyState.MouseDown += delegate { FocusGridForScanner(); };

            content.Controls.Add(_grid);
            _grid.Controls.Add(_emptyState);
            _emptyState.BringToFront();
            _grid.Resize += delegate { LayoutEmptyCartSurface(); };

            _cartHost.Controls.Add(_cartHeader, 0, 0);
            _cartHost.Controls.Add(content, 0, 1);
            return _cartHost;
        }

private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.RowHeight = BookDeskUiSpec.Standard.TableRowHeight;
            _grid.RowHeightHeader = BookDeskUiSpec.Standard.TableHeaderHeight;
            _grid.EnableHeaderResizing = true;
            _grid.ColumnDragSort = false;
            _grid.EditMode = AntdUI.TEditMode.Click;
            _grid.ShowTip = true;
            _grid.EmptyText = "当前入库单为空\r\n点击表格后可直接扫码；未匹配时打开高级查找";

            _grid.Columns = new AntdUI.ColumnCollection
            {
                _indexColumn,
                _selfCodeColumn,
                _isbnColumn,
                _titleColumn,
                _authorColumn,
                _publisherColumn,
                _quantityColumn,
                _unitCostColumn,
                _discountColumn,
                _discountedUnitCostColumn,
                _lineTotalColumn
            };
            _grid.ConfigureColumnPersistence(_services.Settings, "purchase-lines-ui-spec-v6");

            _grid.MouseDown += delegate
            {
                if (!IsReviewed)
                {
                    _scannerCaptureEnabled = true;
                    _scanBuffer.Clear();
                    _grid.Focus();
                }
            };
            _grid.Enter += delegate
            {
                if (!IsReviewed)
                    _scannerCaptureEnabled = true;
            };
            _grid.CellClick += delegate(object sender, AntdUI.TableClickEventArgs e)
            {
                _selectedRow = e.Record as PurchaseCartRow;

                var key = e.Column == null ? "" : e.Column.Key;
                _scannerCaptureEnabled =
                    !string.Equals(key, "Quantity", StringComparison.Ordinal) &&
                    !string.Equals(key, "BaseUnitCostYuan", StringComparison.Ordinal) &&
                    !string.Equals(key, "DiscountPercent", StringComparison.Ordinal);
                if (!_scannerCaptureEnabled)
                    _scanBuffer.Clear();
            };
            _grid.CellEndEdit += HandleCellEndEdit;
        }

private Control CreateNoteSection()
        {
            _noteSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseNoteStandardHeight),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.Surface,
                Padding = new Padding(14, 7, 14, 7),
                Margin = new Padding(0, 6, 0, 0)
            };
            _noteSection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _noteSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _noteSection.Controls.Add(new Label
            {
                Text = "备注",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = UiTheme.TextSecondary,
                Font = UiTheme.Font(8.5F),
                Margin = new Padding(0, 0, 12, 0)
            }, 0, 0);

            _note.Dock = DockStyle.Fill;
            _note.Margin = Padding.Empty;
            _noteSection.Controls.Add(_note, 1, 0);
            return _noteSection;
        }

private Control CreateTotalsSection()
        {
            _totalsSection = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MinimumSize = new Size(0, BookDeskUiSpec.PurchaseSummaryStandardHeight),
                ColumnCount = 2,
                RowCount = 1,
                BackColor = UiTheme.SurfaceMuted,
                Padding = new Padding(14, 9, 14, 9),
                Margin = new Padding(0, 8, 0, 0)
            };
            _totalsSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _totalsSection.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _metrics = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = UiTheme.SurfaceMuted,
                Margin = Padding.Empty
            };
            ConfigureSummaryLabel(_lineCount, false);
            ConfigureSummaryLabel(_quantityTotal, false);
            ConfigureSummaryLabel(_originalTotal, false);
            ConfigureSummaryLabel(_discountTotal, false);
            ConfigureSummaryLabel(_total, true);
            _orderDiscountLabel.AutoSize = true;
            _orderDiscountLabel.Text = "整单折扣 %";
            _orderDiscountLabel.ForeColor = UiTheme.TextSecondary;
            _orderDiscountLabel.Font = UiTheme.Font(8.2F, FontStyle.Bold);
            _orderDiscountLabel.Margin = new Padding(0, 9, 6, 0);
            _metrics.Controls.Add(_lineCount);
            _metrics.Controls.Add(_quantityTotal);
            _metrics.Controls.Add(_originalTotal);
            _metrics.Controls.Add(_discountTotal);
            _metrics.Controls.Add(_orderDiscountLabel);
            _metrics.Controls.Add(_orderDiscount);
            _metrics.Controls.Add(_total);

            _bottomActions = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = Padding.Empty
            };

            _saveDraftButton = UiTheme.CreateAntdButton("保存草稿", false);
            _saveDraftButton.Width = 104;
            _saveDraftButton.Click += delegate { SaveDraftInternal(true); };

            _unreviewButton = UiTheme.CreateAntdButton("反复核", false);
            _unreviewButton.Width = 92;
            _unreviewButton.Click += delegate { UnreviewCurrent(); };

            _reviewButton = UiTheme.CreateAntdButton("复核入库", true);
            _reviewButton.Width = 112;
            _reviewButton.Click += delegate { ReviewCurrent(); };

            _bottomActions.Controls.Add(_saveDraftButton);
            _bottomActions.Controls.Add(_unreviewButton);
            _bottomActions.Controls.Add(_reviewButton);

            _totalsSection.Controls.Add(_metrics, 0, 0);
            _totalsSection.Controls.Add(_bottomActions, 1, 0);
            return _totalsSection;
        }

private static void ConfigureSummaryLabel(Label label, bool primary)
        {
            label.AutoSize = true;
            label.ForeColor = primary ? UiTheme.Accent : UiTheme.TextSecondary;
            label.Font = UiTheme.Font(primary ? 12F : 8.2F, FontStyle.Bold);
            label.BackColor = primary ? UiTheme.AccentSoft : UiTheme.Surface;
            label.Padding = primary ? new Padding(10, 6, 10, 6) : new Padding(8, 6, 8, 6);
            label.Margin = new Padding(0, 0, 8, 0);
        }

private void ApplyResponsiveColumns()
        {
            var profile = _profile ?? BookDeskUiSpec.Standard;
            var compact = profile.IsCompact;
            var expanded = string.Equals(profile.Name, "expanded", StringComparison.Ordinal);
            var profileControlHeight = profile.ControlHeight;
            var fieldHeight = compact
                ? BookDeskUiSpec.PurchaseFieldCompactHeight
                : BookDeskUiSpec.PurchaseFieldStandardHeight;
            var topButtonHeight = compact
                ? BookDeskUiSpec.PurchaseTopButtonCompactHeight
                : BookDeskUiSpec.PurchaseTopButtonStandardHeight;

            if (_receivingSection != null)
                _receivingSection.Padding = Padding.Empty;

            if (_actionRow != null)
            {
                _actionRow.Margin = Padding.Empty;
                _actionRow.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseTopToolbarCompactHeight
                        : BookDeskUiSpec.PurchaseTopToolbarStandardHeight);
                _actionRow.WrapContents = false;
            }

            SetTopToolbarButton(_newOrderButton, 118, topButtonHeight, profile);
            SetTopToolbarButton(_historyButton, 112, topButtonHeight, profile);
            SetTopToolbarButton(_previousButton, 96, topButtonHeight, profile);
            SetTopToolbarButton(_nextButton, 96, topButtonHeight, profile);
            SetTopToolbarButton(_exportButton, 118, topButtonHeight, profile);
            SetTopToolbarButton(_lookupButton, 112, topButtonHeight, profile);
            SetTopToolbarButton(_clearButton, 118, topButtonHeight, profile);
            if (_clearButton != null) _clearButton.Margin = Padding.Empty;

            SetToolbarButton(_removeButton, 106, profile);
            SetToolbarButton(_saveDraftButton, compact ? 96 : 104, profile);
            SetToolbarButton(_unreviewButton, compact ? 80 : 88, profile);
            SetToolbarButton(_reviewButton, compact ? 104 : 112, profile);

            _purchaseDate.Height = fieldHeight;
            _purchaseDate.MinimumSize = new Size(0, fieldHeight);
            _orderNo.Height = fieldHeight;
            _orderNo.MinimumSize = new Size(0, fieldHeight);
            _supplier.Height = fieldHeight;
            _supplier.MinimumSize = new Size(0, fieldHeight);
            _orderDiscount.Width = compact ? 96 : 112;
            _orderDiscount.Height = profileControlHeight;
            _orderDiscount.MinimumSize = new Size(compact ? 96 : 112, profileControlHeight);
            _orderDiscount.Font = UiTheme.Font(profile.BodyFontPoints);
            _note.Height = profileControlHeight;
            _note.MinimumSize = new Size(0, profileControlHeight);

            _purchaseDate.Font = UiTheme.Font(profile.BodyFontPoints);
            _orderNo.Font = UiTheme.Font(profile.BodyFontPoints);
            _supplier.Font = UiTheme.Font(profile.BodyFontPoints);
            _note.Font = UiTheme.Font(profile.BodyFontPoints);

            ResizeFieldHost(
                _purchaseDate,
                BookDeskUiSpec.PurchaseDateLabelWidth,
                compact ? 176 : 206,
                fieldHeight);
            ResizeFieldHost(
                _orderNo,
                BookDeskUiSpec.PurchaseOrderNoLabelWidth,
                compact ? 210 : 258,
                fieldHeight);
            ResizeFieldHost(
                _supplier,
                BookDeskUiSpec.PurchaseSupplierLabelWidth,
                compact
                    ? BookDeskUiSpec.PurchaseSupplierCompactWidth
                    : expanded
                        ? BookDeskUiSpec.PurchaseSupplierExpandedWidth
                        : BookDeskUiSpec.PurchaseSupplierStandardWidth,
                fieldHeight);

            if (_statusLabel != null)
            {
                _statusLabel.Size = new Size(118, fieldHeight);
                _statusLabel.MinimumSize = new Size(118, fieldHeight);
                _statusLabel.Font = UiTheme.Font(profile.BodyFontPoints, FontStyle.Bold);
            }

            if (_documentSection != null)
            {
                _documentSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseDocumentCompactHeight
                        : BookDeskUiSpec.PurchaseDocumentStandardHeight);
                var horizontalPadding = compact ? 10 : 14;
                var targetHeight = compact
                    ? BookDeskUiSpec.PurchaseDocumentCompactHeight
                    : BookDeskUiSpec.PurchaseDocumentStandardHeight;
                var verticalPadding = Math.Max(0, (targetHeight - fieldHeight) / 2);
                _documentSection.Padding = new Padding(
                    horizontalPadding,
                    verticalPadding,
                    horizontalPadding,
                    verticalPadding);
                _documentSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_cartHost != null)
                _cartHost.Margin = new Padding(0, profile.SectionGap, 0, 0);

            if (_cartHeader != null)
            {
                _cartHeader.MinimumSize = new Size(
                    0,
                    compact ? 44 : BookDeskUiSpec.PurchaseCartHeaderHeight);
                _cartHeader.Padding = compact
                    ? new Padding(10, 4, 10, 4)
                    : new Padding(12, 5, 12, 5);
            }

            _grid.RowHeightHeader = profile.TableHeaderHeight;
            _grid.RowHeight = profile.TableRowHeight;
            _grid.Font = UiTheme.Font(profile.TableFontPoints);
            ApplyColumnWidths(profile);
            _emptyState.Font = UiTheme.Font(profile.SecondaryFontPoints);
            LayoutEmptyCartSurface();

            if (_noteSection != null)
            {
                _noteSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseNoteCompactHeight
                        : BookDeskUiSpec.PurchaseNoteStandardHeight);
                _noteSection.Padding = compact
                    ? new Padding(10, 4, 10, 4)
                    : new Padding(14, 6, 14, 6);
                _noteSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_totalsSection != null)
            {
                _totalsSection.MinimumSize = new Size(
                    0,
                    compact
                        ? BookDeskUiSpec.PurchaseSummaryCompactHeight
                        : BookDeskUiSpec.PurchaseSummaryStandardHeight);
                _totalsSection.Padding = compact
                    ? new Padding(8, 7, 8, 7)
                    : new Padding(12, 9, 12, 9);
                _totalsSection.Margin = new Padding(0, profile.SectionGap, 0, 0);
            }

            if (_metrics != null)
                _metrics.WrapContents = true;
            if (_bottomActions != null)
                _bottomActions.WrapContents = false;

            ResizeSummaryLabel(_lineCount, compact ? 84 : 110, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_quantityTotal, compact ? 94 : 126, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_originalTotal, compact ? 118 : 150, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_discountTotal, compact ? 112 : 142, compact ? 34 : 40, profile, false);
            ResizeSummaryLabel(_total, compact ? 146 : 190, compact ? 34 : 40, profile, true);

            _indexColumn.Visible = true;
            _selfCodeColumn.Visible = true;
            _isbnColumn.Visible = true;
            _titleColumn.Visible = true;
            _authorColumn.Visible = true;
            _publisherColumn.Visible = true;
            _quantityColumn.Visible = true;
            _unitCostColumn.Visible = true;
            _discountColumn.Visible = true;
            _discountedUnitCostColumn.Visible = !compact;
            _lineTotalColumn.Visible = true;
            _shelfColumn.Visible = false;
            _stockColumn.Visible = false;

            _grid.LoadLayout();
            _grid.Refresh();
            if (_rows.Count == 0)
            {
                LayoutEmptyCartSurface();
                _emptyState.BringToFront();
            }
        }

private static void ResizeFieldHost(
            Control control,
            int labelWidth,
            int controlWidth,
            int height)
        {
            if (control == null || control.Parent == null)
                return;

            var host = control.Parent as TableLayoutPanel;
            if (host == null)
                return;

            host.Width = labelWidth + controlWidth;
            host.Height = height;
            host.MinimumSize = new Size(labelWidth + controlWidth, height);
            if (host.ColumnStyles.Count >= 2)
            {
                host.ColumnStyles[0].SizeType = SizeType.Absolute;
                host.ColumnStyles[0].Width = labelWidth;
            }
        }

private static void SetTopToolbarButton(
            AntdUI.Button button,
            int width,
            int height,
            UiSpecProfile profile)
        {
            if (button == null) return;

            var toolbarHeight = profile.IsCompact
                ? BookDeskUiSpec.PurchaseTopToolbarCompactHeight
                : BookDeskUiSpec.PurchaseTopToolbarStandardHeight;
            var verticalMargin = Math.Max(0, (toolbarHeight - height) / 2);
            var horizontalPadding = profile.IsCompact
                ? BookDeskUiSpec.PurchaseTopButtonCompactPaddingX
                : BookDeskUiSpec.PurchaseTopButtonStandardPaddingX;

            button.Width = width;
            button.Height = height;
            button.MinimumSize = new Size(width, height);
            button.Padding = new Padding(horizontalPadding, 0, horizontalPadding, 0);
            button.Font = UiTheme.Font(profile.BodyFontPoints);
            button.Margin = new Padding(0, verticalMargin, profile.ControlGap, verticalMargin);
        }

private static void SetToolbarButton(AntdUI.Button button, int width, UiSpecProfile profile)
        {
            if (button == null) return;
            button.Width = width;
            button.Height = profile.ControlHeight;
            button.MinimumSize = new Size(width, profile.ControlHeight);
            button.Font = UiTheme.Font(profile.BodyFontPoints);
            button.Margin = new Padding(0, 0, profile.ControlGap, 0);
        }

private static void ResizeSummaryLabel(
            Label label,
            int width,
            int height,
            UiSpecProfile profile,
            bool primary)
        {
            if (label == null) return;
            label.AutoSize = false;
            label.Size = new Size(width, height);
            label.MinimumSize = new Size(width, height);
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.Padding = new Padding(8, 0, 8, 0);
            label.Font = UiTheme.Font(
                primary ? BookDeskUiSpec.PixelFontToPoints(15) : profile.BodyFontPoints,
                FontStyle.Bold);
        }

private void ApplyColumnWidths(UiSpecProfile profile)
        {
            var compact = profile != null && profile.IsCompact;
            _indexColumn.Width = compact ? "52" : "58";
            _selfCodeColumn.Width = compact ? "86" : "112";
            _isbnColumn.Width = compact ? "94" : "122";
            _titleColumn.Width = "fill";
            _titleColumn.MinWidth = "150";
            _titleColumn.MaxWidth = "420";
            _authorColumn.Width = compact ? "84" : "118";
            _publisherColumn.Width = compact ? "84" : "118";
            _quantityColumn.Width = compact ? "70" : "82";
            _unitCostColumn.Width = compact ? "92" : "108";
            _discountColumn.Width = compact ? "86" : "96";
            _discountedUnitCostColumn.Width = compact ? "86" : "96";
            _lineTotalColumn.Width = compact ? "94" : "110";
            _shelfColumn.Width = "88";
            _stockColumn.Width = "92";
        }

private void LayoutEmptyCartSurface()
        {
            if (_grid == null) return;
            var profile = _profile ?? BookDeskUiSpec.Standard;
            var headerHeight = Math.Min(profile.TableHeaderHeight, Math.Max(0, _grid.ClientSize.Height));
            _emptyState.SetBounds(
                0,
                headerHeight,
                Math.Max(0, _grid.ClientSize.Width),
                Math.Max(0, _grid.ClientSize.Height - headerHeight));
        }
    }
}
