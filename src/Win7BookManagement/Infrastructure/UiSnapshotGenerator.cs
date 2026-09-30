using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Win7BookManagement.Forms;
using Win7BookManagement.Models;

namespace Win7BookManagement.Infrastructure
{
    public static class UiSnapshotGenerator
    {
        public static int Run(string outputDirectory)
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "Win7BookManagement-UiSnapshot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                if (string.IsNullOrWhiteSpace(outputDirectory))
                    outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-snapshots");

                outputDirectory = Path.GetFullPath(outputDirectory);
                Directory.CreateDirectory(outputDirectory);

                var services = new ApplicationServices(Path.Combine(root, "snapshot.db"));
                services.Settings.SetOnboardingCompleted(true);
                services.Settings.SetHomeGuideExpanded(false);
                services.Settings.SetLowStockThreshold(3);

                var books = SeedData(services);

                Capture(
                    outputDirectory,
                    "01-main-dashboard-1366x768.png",
                    delegate { return new MainForm(services); },
                    new Size(1366, 768),
                    false,
                    null);

                Capture(
                    outputDirectory,
                    "02-sales-1366x768.png",
                    delegate { return new SalesForm(services); },
                    new Size(1366, 768),
                    true,
                    delegate(Form form) { PopulateByIsbn(form, books, 3); });

                Capture(
                    outputDirectory,
                    "03-sales-1024x768.png",
                    delegate { return new SalesForm(services); },
                    new Size(1024, 768),
                    true,
                    delegate(Form form) { PopulateByIsbn(form, books, 2); });

                Capture(
                    outputDirectory,
                    "08-sales-wide-1800x900.png",
                    delegate { return new SalesForm(services); },
                    new Size(1800, 900),
                    true,
                    delegate(Form form) { PopulateByIsbn(form, books, 3); });

                Capture(
                    outputDirectory,
                    "04-book-master-1366x768.png",
                    delegate { return new BookListForm(services); },
                    new Size(1366, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "05-book-master-1024x768.png",
                    delegate { return new BookListForm(services); },
                    new Size(1024, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "09-book-master-wide-1800x900.png",
                    delegate { return new BookListForm(services); },
                    new Size(1800, 900),
                    true,
                    null);

                var firstBook = services.Books.GetById(books[0].Id);
                Capture(
                    outputDirectory,
                    "06-book-editor.png",
                    delegate { return new BookEditForm(services, firstBook); },
                    new Size(1000, 720),
                    false,
                    null);

                Capture(
                    outputDirectory,
                    "07-purchase-1366x768.png",
                    delegate { return new PurchaseForm(services); },
                    new Size(1366, 768),
                    true,
                    delegate(Form form) { PopulateByIsbn(form, books, 3); });

                Capture(
                    outputDirectory,
                    "10-inventory-1366x768.png",
                    delegate { return new InventoryForm(services); },
                    new Size(1366, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "11-suppliers-1366x768.png",
                    delegate { return new SupplierForm(services); },
                    new Size(1366, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "12-reports-1366x768.png",
                    delegate { return new ReportsForm(services); },
                    new Size(1366, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "13-documents-1366x768.png",
                    delegate { return new DocumentCenterForm(services); },
                    new Size(1366, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "14-settings-1024x768.png",
                    delegate { return new SettingsForm(services); },
                    new Size(1024, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "15-backup-1024x768.png",
                    delegate { return new BackupForm(services); },
                    new Size(1024, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "16-help-1024x768.png",
                    delegate { return new HelpForm(services, delegate { }, delegate { }); },
                    new Size(1024, 768),
                    true,
                    null);

                Capture(
                    outputDirectory,
                    "17-book-lookup.png",
                    delegate { return new BookLookupDialog(services); },
                    new Size(920, 620),
                    false,
                    null);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                try
                {
                    File.WriteAllText(
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-snapshot-error.txt"),
                        ex.ToString());
                }
                catch
                {
                }

                return 1;
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                try { Directory.Delete(root, true); } catch { }
            }
        }

        private static IList<Book> SeedData(ApplicationServices services)
        {
            var supplierId = services.Suppliers.Insert(new Supplier
            {
                Name = "纸上航线图书供应",
                ContactName = "林晓",
                Phone = "138-0000-2026",
                Note = "视觉快照示例供应商"
            });

            var samples = new[]
            {
                new Book
                {
                    SelfCode = "BK-0001",
                    Isbn = "9780000000101",
                    Title = "卡夫卡短篇集",
                    Author = "弗兰茨·卡夫卡",
                    Publisher = "示例出版社",
                    Category = "外国文学",
                    PublicationYear = "2025",
                    Edition = "1版2印",
                    Binding = "精装",
                    ShelfCode = "A-01-1",
                    ListPriceCent = 6800,
                    DefaultPurchasePriceCent = 3600,
                    SalePriceCent = 6200,
                    Note = "重点陈列"
                },
                new Book
                {
                    SelfCode = "BK-0002",
                    Isbn = "9780000000102",
                    Title = "博尔赫斯小说选",
                    Author = "豪尔赫·路易斯·博尔赫斯",
                    Publisher = "示例文艺出版社",
                    Category = "外国文学",
                    PublicationYear = "2024",
                    Edition = "2版1印",
                    Binding = "平装",
                    ShelfCode = "A-01-2",
                    ListPriceCent = 5800,
                    DefaultPurchasePriceCent = 3100,
                    SalePriceCent = 5200,
                    Note = ""
                },
                new Book
                {
                    SelfCode = "BK-0003",
                    Isbn = "9780000000103",
                    Title = "现代主义文学导论",
                    Author = "示例作者",
                    Publisher = "北方学术出版社",
                    Category = "文学理论",
                    PublicationYear = "2026",
                    Edition = "1版1印",
                    Binding = "平装",
                    ShelfCode = "B-03-1",
                    ListPriceCent = 7600,
                    DefaultPurchasePriceCent = 4200,
                    SalePriceCent = 7000,
                    Note = "新书"
                },
                new Book
                {
                    SelfCode = "BK-0004",
                    Isbn = "9780000000104",
                    Title = "独立书店经营手册",
                    Author = "编辑部",
                    Publisher = "城市文化出版社",
                    Category = "经营管理",
                    PublicationYear = "2026",
                    Edition = "1版1印",
                    Binding = "平装",
                    ShelfCode = "C-02-3",
                    ListPriceCent = 4900,
                    DefaultPurchasePriceCent = 2600,
                    SalePriceCent = 4500,
                    Note = ""
                },
                new Book
                {
                    SelfCode = "BK-0005",
                    Isbn = "9780000000105",
                    Title = "摄影与城市漫游",
                    Author = "周屿",
                    Publisher = "光影出版社",
                    Category = "艺术",
                    PublicationYear = "2023",
                    Edition = "1版3印",
                    Binding = "精装",
                    ShelfCode = "D-01-1",
                    ListPriceCent = 9800,
                    DefaultPurchasePriceCent = 5400,
                    SalePriceCent = 9200,
                    Note = "封面易磨损"
                },
                new Book
                {
                    SelfCode = "BK-0006",
                    Isbn = "9780000000106",
                    Title = "诗歌的声音",
                    Author = "陈野",
                    Publisher = "远岸出版社",
                    Category = "诗歌",
                    PublicationYear = "2022",
                    Edition = "1版1印",
                    Binding = "平装",
                    ShelfCode = "A-05-2",
                    ListPriceCent = 4200,
                    DefaultPurchasePriceCent = 2200,
                    SalePriceCent = 3900,
                    Note = ""
                },
                new Book
                {
                    SelfCode = "BK-0007",
                    Isbn = "9780000000107",
                    Title = "设计中的秩序与留白",
                    Author = "宋一",
                    Publisher = "设计文化出版社",
                    Category = "设计",
                    PublicationYear = "2025",
                    Edition = "1版1印",
                    Binding = "精装",
                    ShelfCode = "D-02-4",
                    ListPriceCent = 11800,
                    DefaultPurchasePriceCent = 6500,
                    SalePriceCent = 10800,
                    Note = "陈列样书另存"
                },
                new Book
                {
                    SelfCode = "BK-0008",
                    Isbn = "9780000000108",
                    Title = "城市咖啡馆观察笔记",
                    Author = "林桥",
                    Publisher = "日常出版",
                    Category = "生活",
                    PublicationYear = "2026",
                    Edition = "1版1印",
                    Binding = "平装",
                    ShelfCode = "E-01-2",
                    ListPriceCent = 5200,
                    DefaultPurchasePriceCent = 2800,
                    SalePriceCent = 4800,
                    Note = ""
                }
            };

            var stored = new List<Book>();
            for (var i = 0; i < samples.Length; i++)
            {
                var id = services.Books.Insert(samples[i]);
                services.Inventory.Adjust(id, i == 5 ? 2 : 6 + i, "视觉快照初始库存");
                stored.Add(services.Books.GetById(id));
            }

            services.Purchases.Receive(
                supplierId,
                new List<TransactionLineInput>
                {
                    new TransactionLineInput
                    {
                        BookId = stored[0].Id,
                        Quantity = 3,
                        UnitPriceCent = 3500
                    },
                    new TransactionLineInput
                    {
                        BookId = stored[1].Id,
                        Quantity = 2,
                        UnitPriceCent = 3000
                    }
                },
                "视觉快照采购");

            services.Sales.Checkout(
                new List<TransactionLineInput>
                {
                    new TransactionLineInput
                    {
                        BookId = stored[0].Id,
                        Quantity = 1,
                        UnitPriceCent = stored[0].SalePriceCent
                    },
                    new TransactionLineInput
                    {
                        BookId = stored[2].Id,
                        Quantity = 1,
                        UnitPriceCent = stored[2].SalePriceCent
                    },
                    new TransactionLineInput
                    {
                        BookId = stored[4].Id,
                        Quantity = 1,
                        UnitPriceCent = stored[4].SalePriceCent
                    }
                },
                "视觉快照销售");

            return stored;
        }

        private static void PopulateByIsbn(Form form, IList<Book> books, int count)
        {
            if (form == null || books == null)
                return;

            var type = form.GetType();
            var isbnField = type.GetField("_isbn", BindingFlags.Instance | BindingFlags.NonPublic);
            var addMethod = type.GetMethod("AddByIsbn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (isbnField == null || addMethod == null)
                return;

            var textBox = isbnField.GetValue(form) as TextBox;
            if (textBox == null)
                return;

            var maximum = Math.Min(count, books.Count);
            for (var i = 0; i < maximum; i++)
            {
                textBox.Text = books[i].Isbn;
                addMethod.Invoke(form, null);
            }
        }

        private static void Capture(
            string outputDirectory,
            string fileName,
            Func<Form> factory,
            Size size,
            bool embeddedPage,
            Action<Form> afterShown)
        {
            using (var form = factory())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = Point.Empty;
                form.ShowInTaskbar = false;

                var workingArea = Screen.PrimaryScreen == null
                    ? Size.Empty
                    : Screen.PrimaryScreen.WorkingArea.Size;
                var useOffscreenHost =
                    embeddedPage ||
                    (workingArea.Width > 0 && size.Width > workingArea.Width) ||
                    (workingArea.Height > 0 && size.Height > workingArea.Height);

                if (useOffscreenHost)
                {
                    using (var host = new Panel())
                    {
                        host.Size = size;
                        host.BackColor = UiTheme.Background;
                        host.Padding = Padding.Empty;
                        host.Margin = Padding.Empty;
                        host.CreateControl();

                        form.TopLevel = false;
                        form.FormBorderStyle = FormBorderStyle.None;
                        form.Dock = DockStyle.Fill;
                        form.Margin = Padding.Empty;

                        host.Controls.Add(form);
                        UiTheme.Apply(form);
                        form.Show();
                        Application.DoEvents();

                        if (afterShown != null)
                        {
                            afterShown(form);
                            Application.DoEvents();
                        }

                        form.PerformLayout();
                        host.PerformLayout();
                        Application.DoEvents();

                        using (var bitmap = new Bitmap(
                            Math.Max(1, size.Width),
                            Math.Max(1, size.Height)))
                        {
                            host.DrawToBitmap(
                                bitmap,
                                new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                            bitmap.Save(
                                Path.Combine(outputDirectory, fileName),
                                ImageFormat.Png);
                        }

                        form.Hide();
                    }

                    return;
                }

                form.Size = size;
                UiTheme.Apply(form);
                form.Show();
                Application.DoEvents();

                if (afterShown != null)
                {
                    afterShown(form);
                    Application.DoEvents();
                }

                form.PerformLayout();
                Application.DoEvents();

                using (var bitmap = new Bitmap(
                    Math.Max(1, form.Width),
                    Math.Max(1, form.Height)))
                {
                    form.DrawToBitmap(
                        bitmap,
                        new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(
                        Path.Combine(outputDirectory, fileName),
                        ImageFormat.Png);
                }

                form.Hide();
            }
        }
    }
}
