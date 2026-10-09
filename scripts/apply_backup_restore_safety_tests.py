from pathlib import Path


def replace_once(path, old, new):
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"Expected exactly one source block in {path}, found {count}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")


backup_service = "src/Win7BookManagement/Services/BackupService.cs"
replace_once(
    backup_service,
    "AND name IN ('schema_info','books','inventory_transactions','settings');",
    "AND name IN ('schema_info','books','inventory_transactions','app_settings');"
)

self_test = "src/Win7BookManagement/Infrastructure/SelfTest.cs"
replace_once(
    self_test,
    '''                var backupPath = Path.Combine(root, "backup.db");
                services.Backup.CreateBackup(backupPath);
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
                    throw new InvalidOperationException("数据库备份自检失败。");

                VerifyLegacyBookSchemaUpgrade(root);
''',
    '''                var backupSnapshotStock = services.Books.GetById(bookId).StockQuantity;
                var backupPath = Path.Combine(root, "backup.db");
                services.Backup.CreateBackup(backupPath);
                if (!File.Exists(backupPath) || new FileInfo(backupPath).Length == 0)
                    throw new InvalidOperationException("数据库备份自检失败。");

                var liveOverwriteRejected = false;
                try
                {
                    services.Backup.CreateBackup(dbPath);
                }
                catch (InvalidOperationException)
                {
                    liveOverwriteRejected = true;
                }
                if (!liveOverwriteRejected)
                    throw new InvalidOperationException("备份不应允许覆盖当前数据库文件。");

                services.Inventory.Adjust(bookId, 2, "backup restore mutation");
                if (services.Books.GetById(bookId).StockQuantity != backupSnapshotStock + 2)
                    throw new InvalidOperationException("恢复往返测试准备数据失败。");

                var safetyBackupPath = services.Backup.Restore(backupPath);
                var restoredServices = new ApplicationServices(dbPath);
                if (restoredServices.Books.GetById(bookId).StockQuantity != backupSnapshotStock)
                    throw new InvalidOperationException("数据库恢复没有还原到备份时的数据。");

                if (string.IsNullOrWhiteSpace(safetyBackupPath) || !File.Exists(safetyBackupPath))
                    throw new InvalidOperationException("恢复前安全备份没有生成。");

                using (var safetyConnection = new SQLiteConnection(
                    "Data Source=" + safetyBackupPath + ";Version=3;Read Only=True;Pooling=False;"))
                {
                    safetyConnection.Open();
                    using (var command = safetyConnection.CreateCommand())
                    {
                        command.CommandText = "SELECT stock_quantity FROM books WHERE id=@id;";
                        command.Parameters.AddWithValue("@id", bookId);
                        if (Convert.ToInt32(command.ExecuteScalar()) != backupSnapshotStock + 2)
                            throw new InvalidOperationException("恢复前安全备份没有保存恢复操作前的最新数据。");
                    }
                }

                var corruptBackupPath = Path.Combine(root, "corrupt-backup.db");
                File.WriteAllText(corruptBackupPath, "not a sqlite database");
                var corruptBackupRejected = false;
                try
                {
                    restoredServices.Backup.Restore(corruptBackupPath);
                }
                catch (InvalidOperationException)
                {
                    corruptBackupRejected = true;
                }
                if (!corruptBackupRejected ||
                    new ApplicationServices(dbPath).Books.GetById(bookId).StockQuantity != backupSnapshotStock)
                    throw new InvalidOperationException("损坏备份拒绝或原数据库保护自检失败。");

                VerifyLegacyBookSchemaUpgrade(root);
'''
)

README = "README.md"
replace_once(
    README,
    "- SQLite 数据库备份与恢复",
    "- SQLite 安全备份与恢复：一致性快照、完整性/外键校验、恢复前时间戳安全备份、暂存切换与失败自动回滚"
)
replace_once(
    README,
    "## 销售月报 Excel\n",
    '''## 数据库备份与恢复安全机制

- **创建备份**不再直接复制正在运行的数据库文件。系统先执行 WAL 一致性检查点，再使用 SQLite Backup API 写入临时数据库，执行 `integrity_check`、核心表检查和 `foreign_key_check`；只有全部通过后才把备份发布到用户选择的位置。
- 若用户选择的备份文件名已经存在，新备份验证完成前不会破坏旧备份；发布失败会尽量恢复原文件。系统也明确禁止把备份目标选成当前正在运行的数据库文件。
- **执行恢复**时，系统首先只读校验用户选择的备份，在此之前不会修改当前数据库。随后使用同一套 SQLite 一致性备份机制，在当前数据库目录的 `restore-safety` 子目录生成 `before_restore_时间戳.db` 安全备份。
- 所选备份不会直接覆盖正式库，而是先通过 SQLite Backup API 重新物化成独立暂存库并再次校验；通过后才切换为正式数据库。旧版本有效备份会在切换后按现有 forward-only migration 自动升级。
- 切换完成后系统再次执行完整性和外键检查。如果恢复后的数据库或升级过程失败，会自动使用 `before_restore_*.db` 回滚到恢复操作前的数据；即使自动回滚也失败，安全备份文件仍会保留并在错误信息中给出位置。
- 恢复成功后建议重新启动 BOOK DESK，使所有页面缓存和查询状态重新从恢复后的数据库加载。

## 销售月报 Excel
'''
)

AGENTS = "AGENTS.md"
replace_once(
    AGENTS,
    "- Backups must use a consistent SQLite backup/copy procedure.\n",
    '''- Backups must use SQLite's backup API to create a consistent standalone snapshot; never treat a raw copy of the WAL-mode main database file as a complete backup.
- A backup artifact must be written to a temporary file, pass SQLite integrity/core-table/foreign-key validation, and only then replace or publish the requested destination.
- Restore must validate the selected backup before touching the live database, create a timestamped pre-restore safety snapshot through the same SQLite backup API, restore through a validated staging database, and validate again after forward migrations.
- Any failure after the live database is replaced must automatically attempt rollback from the pre-restore safety snapshot; the safety snapshot must be preserved for manual recovery if automatic rollback fails.
'''
)
