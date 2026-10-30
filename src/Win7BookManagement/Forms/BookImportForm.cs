using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Services;

namespace Win7BookManagement.Forms
{
    public sealed class BookImportForm : Form
    {
        private readonly ApplicationServices _services;
        private readonly BookImportService _import;
        private readonly AntdUI.Table _grid = new AntdUI.Table();
        private readonly Label _summary = new Label { AutoSize = true, MaximumSize = new Size(860, 0) };
        private readonly AntdUI.Button _commit = UiTheme.CreateAntdButton("确认导入", true);
        private readonly AntdUI.Button _errors = UiTheme.CreateAntdButton("导出错误行", false);
        private string _path;
        private IList<BookImportRow> _rows = new List<BookImportRow>();
        public bool Imported { get; private set; }

        public BookImportForm(ApplicationServices services)
        {
            _services = services;
            _import = new BookImportService(services.Database, services.Books);
            UiTheme.ConfigureForm(this);
            Text = "批量导入图书"; ClientSize = new Size(920, 600); MinimumSize = new Size(720, 480);
            StartPosition = FormStartPosition.CenterParent; BackColor = UiTheme.Background;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.Controls.Add(new Label { Text = "先下载模板，填写后选择文件预览。只有全部行通过校验才会一次性导入；更新已有图书不会修改库存。单次最多 10000 行。", AutoSize = true, MaximumSize = new Size(880, 0), Margin = new Padding(0, 0, 0, 8) }, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            var template = UiTheme.CreateAntdButton("下载模板", false); template.Width = 110; template.Click += delegate { DownloadTemplate(); };
            var choose = UiTheme.CreateAntdButton("选择并预览", true); choose.Width = 130; choose.Click += delegate { ChooseFile(); };
            _commit.Width = 110; _commit.Enabled = false; _commit.Click += delegate { Commit(); };
            _errors.Width = 120; _errors.Enabled = false; _errors.Click += delegate { ExportErrors(); };
            actions.Controls.Add(template); actions.Controls.Add(choose); actions.Controls.Add(_commit); actions.Controls.Add(_errors);
            root.Controls.Add(actions, 0, 1);
            _grid.Dock = DockStyle.Fill;
            _grid.Columns = new AntdUI.ColumnCollection
            {
                new AntdUI.Column("RowNumber", "行号") { Width = "60", ReadOnly = true },
                new AntdUI.Column("Status", "结果") { Width = "70", ReadOnly = true },
                new AntdUI.Column("SelfCode", "店内编码") { Width = "110", ReadOnly = true },
                new AntdUI.Column("Isbn", "ISBN") { Width = "130", ReadOnly = true },
                new AntdUI.Column("Title", "书名") { Width = "180", ReadOnly = true },
                new AntdUI.Column("Error", "错误 / 冲突说明") { Width = "260", ReadOnly = true }
            };
            root.Controls.Add(_grid, 0, 2); root.Controls.Add(_summary, 0, 3);
            Controls.Add(root); UiTheme.Apply(this);
        }

        private void DownloadTemplate()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "Excel 模板 (*.xlsx)|*.xlsx|CSV 模板 (*.csv)|*.csv"; dialog.FileName = "图书导入模板.xlsx";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (Path.GetExtension(dialog.FileName).ToLowerInvariant() == ".csv")
                        File.WriteAllText(dialog.FileName, BookImportService.ToCsv(new[] { BookImportService.Headers }.Concat(BookImportService.CreateTemplate().Rows.Cast<DataRow>().Select(r => r.ItemArray.Select(v => Convert.ToString(v)).ToArray()))), new UTF8Encoding(true));
                    else _services.Excel.Export(BookImportService.CreateTemplate(), dialog.FileName, "图书导入模板");
                    MessageBox.Show(this, "模板已保存。示例行请在正式导入前删除或替换。", "模板已生成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { CrashReporter.Handle(this, ex, "下载导入模板"); }
            }
        }

        private void ChooseFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图书模板 (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _path = dialog.FileName;
                UiTask.Run(this, "预览图书导入", (progress, token) => _import.Preview(_path), rows =>
                {
                    _rows = rows; BindPreview();
                });
            }
        }

        private void BindPreview()
        {
            var table = new DataTable();
            table.Columns.Add("RowNumber", typeof(int)); table.Columns.Add("Status"); table.Columns.Add("SelfCode"); table.Columns.Add("Isbn"); table.Columns.Add("Title"); table.Columns.Add("Error");
            foreach (var row in _rows)
                table.Rows.Add(row.RowNumber, row.Status, row.Book.SelfCode, row.Book.Isbn, row.Book.Title, row.Error ?? "");
            _grid.DataSource = table;
            var errors = _rows.Count(r => r.Error != null);
            _summary.Text = "共 " + _rows.Count + " 行：新增 " + _rows.Count(r => r.Error == null && !r.IsUpdate) + "，更新 " + _rows.Count(r => r.Error == null && r.IsUpdate) + "，错误 " + errors + "。" + (errors == 0 ? "可以确认导入。" : "请导出错误行修正；当前不会写入任何数据。");
            _commit.Enabled = _rows.Count > 0 && errors == 0; _errors.Enabled = errors > 0;
        }

        private void Commit()
        {
            if (MessageBox.Show(this, "将按预览结果一次性导入，全部成功或全部回滚。确定继续吗？", "确认导入", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            UiTask.Run(this, "批量导入图书", (progress, token) => _import.Commit(_path), count =>
            {
                Imported = true; _commit.Enabled = false;
                MessageBox.Show(this, "已导入 " + count + " 行图书资料。库存未从模板修改。", "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private void ExportErrors()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "错误行 (*.csv)|*.csv"; dialog.FileName = "图书导入错误行.csv";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var rows = new List<string[]> { new[] { "行号", "错误" }.Concat(BookImportService.Headers).ToArray() };
                rows.AddRange(_rows.Where(r => r.Error != null).Select(r =>
                    new[] { r.RowNumber.ToString(), r.Error }.Concat(r.Raw ?? new string[0]).ToArray()));
                File.WriteAllText(dialog.FileName, BookImportService.ToCsv(rows), new UTF8Encoding(true));
            }
        }
    }
}
