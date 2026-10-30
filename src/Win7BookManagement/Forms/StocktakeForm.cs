using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Models;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class StocktakeForm : Form
    {
        private readonly StocktakeService _service;
        private readonly AntdUI.Input _shelf = UiTheme.CreateAntdInput("留空盘点全部货架");
        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label { AutoSize = true, MaximumSize = new Size(860, 0) };
        private StocktakeDocument _document;
        public bool Posted { get; private set; }

        public StocktakeService Service { get { return _service; } }
        public StocktakeForm(ApplicationServices services)
        {
            _service = new StocktakeService(services.Database);
            UiTheme.ConfigureForm(this);
            Text = "库存盘点单"; ClientSize = new Size(900, 580); MinimumSize = new Size(700, 460);
            StartPosition = FormStartPosition.CenterParent; BackColor = UiTheme.Background;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            top.Controls.Add(new Label { Text = "货架位", AutoSize = true, Margin = new Padding(0, 8, 6, 0) });
            _shelf.Width = 200; top.Controls.Add(_shelf);
            var create = UiTheme.CreateAntdButton("生成盘点单", true); create.Width = 120; create.Click += delegate { Create(); }; top.Controls.Add(create);
            var save = UiTheme.CreateAntdButton("保存实盘", false); save.Width = 110; save.Click += delegate { Save(false); }; top.Controls.Add(save);
            var post = UiTheme.CreateAntdButton("确认生成调整", true); post.Width = 140; post.Click += delegate { Post(); }; top.Controls.Add(post);
            root.Controls.Add(top, 0, 0);
            root.Controls.Add(new Label { Text = "填写实盘数量后确认。系统只对有差异的图书生成 ADJUSTMENT 流水；盘点期间若发生销售/采购，需重新盘点，不会覆盖正常业务。", AutoSize = true, MaximumSize = new Size(860, 0), ForeColor = UiTheme.TextSecondary }, 0, 1);
            _grid.Dock = DockStyle.Fill; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false; _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BookId", Visible = false });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SelfCode", HeaderText = "店内编码", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "书名", ReadOnly = true, FillWeight = 180 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Shelf", HeaderText = "货架位", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Expected", HeaderText = "计划数", ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Actual", HeaderText = "实盘数" });
            root.Controls.Add(_grid, 0, 2); root.Controls.Add(_status, 0, 3);
            Controls.Add(root); UiTheme.Apply(this);
        }

        private void Create()
        {
            try { _document = _service.Create(_shelf.Text, "库存盘点"); Bind(); }
            catch (Exception ex) { CrashReporter.Handle(this, ex, "生成盘点单"); }
        }
        private void Bind()
        {
            _grid.Rows.Clear();
            foreach (var item in _document.Items)
                _grid.Rows.Add(item.BookId, item.SelfCode, item.Title, item.ShelfCode, item.ExpectedQuantity, item.ActualQuantity ?? item.ExpectedQuantity);
            _status.Text = "盘点单 " + _document.CountNo + " · " + _document.Items.Count + " 种图书。请核对实盘数。";
        }
        private bool Save(bool silent)
        {
            if (_document == null) { MessageBox.Show(this, "请先生成盘点单。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return false; }
            try
            {
                _grid.EndEdit();
                var actual = new Dictionary<long, int>();
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    int value;
                    if (!int.TryParse(Convert.ToString(row.Cells["Actual"].Value), out value) || value < 0)
                        throw new InvalidOperationException("《" + row.Cells["Title"].Value + "》实盘数必须是非负整数。");
                    actual[Convert.ToInt64(row.Cells["BookId"].Value)] = value;
                }
                _service.SaveActual(_document.Id, actual);
                if (!silent) MessageBox.Show(this, "实盘数量已保存，尚未调整库存。", "已保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            catch (Exception ex) { CrashReporter.Handle(this, ex, "保存实盘数量"); return false; }
        }
        private void Post()
        {
            if (!Save(true)) return;
            if (MessageBox.Show(this, "确认后将一次性生成盘点调整流水并更新库存，且不能重复过账。确定继续吗？", "确认盘点调整", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                var count = _service.Post(_document.Id); Posted = true;
                MessageBox.Show(this, "盘点完成，生成 " + count + " 条库存调整流水。", "盘点完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex) { CrashReporter.Handle(this, ex, "盘点过账"); }
        }
    }
}
