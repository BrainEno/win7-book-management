using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Win7BookManagement.Database;
using Win7BookManagement.Infrastructure;
using Win7BookManagement.Repositories;

namespace Win7BookManagement.Services
{
    public enum BackupHealthLevel
    {
        Unknown = 0,
        Ok = 1,
        Warning = 2,
        Critical = 3
    }

    public sealed class BackupStatus
    {
        public BackupStatus(DateTime? lastBackupUtc, string directory, BackupHealthLevel level, string message)
        {
            LastBackupUtc = lastBackupUtc;
            Directory = directory;
            Level = level;
            Message = message;
        }

        public DateTime? LastBackupUtc { get; private set; }
        public string Directory { get; private set; }
        public BackupHealthLevel Level { get; private set; }
        public string Message { get; private set; }

        public double AgeDays
        {
            get
            {
                if (!LastBackupUtc.HasValue) return double.MaxValue;
                return (DateTime.UtcNow - LastBackupUtc.Value).TotalDays;
            }
        }
    }

    public sealed class BackupVerificationResult
    {
        public bool Passed { get; set; }
        public string Path { get; set; }
        public string Detail { get; set; }
    }

    /// <summary>
    /// P0-3：让备份真的发生。
    /// 启动（每日首次）与正常退出时自动生成滚动备份，按保留数裁剪，
    /// 并在可移动磁盘 / 指定共享目录保留第二份；同时提供备份年龄与验证结果。
    /// </summary>
    public sealed class AutoBackupService
    {
        public const string BackupDirectoryKey = "backup_directory";
        public const string BackupRetentionKey = "backup_retention";
        public const string SecondaryBackupPathKey = "secondary_backup_path";
        public const string SecondaryBackupEnabledKey = "secondary_backup_enabled";
        public const string LastBackupUtcKey = "last_backup_utc";
        public const string LastSecondaryErrorKey = "last_secondary_error";

        public const int DefaultRetention = 14;
        public const int MinRetention = 3;
        public const int MaxRetention = 120;

        private readonly BackupService _backup;
        private readonly SettingsRepository _settings;

        public AutoBackupService(DatabaseConnectionFactory factory)
            : this(factory, new SettingsRepository(factory))
        {
        }

        public AutoBackupService(DatabaseConnectionFactory factory, SettingsRepository settings)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            _backup = new BackupService(factory);
            _settings = settings ?? new SettingsRepository(factory);
        }

        public string BackupDirectory
        {
            get
            {
                var configured = _settings.GetString(BackupDirectoryKey, string.Empty);
                return string.IsNullOrWhiteSpace(configured)
                    ? AppPaths.BackupDirectory
                    : configured.Trim();
            }
        }

        public int Retention
        {
            get
            {
                var value = _settings.GetInt(BackupRetentionKey, DefaultRetention);
                if (value < MinRetention) value = MinRetention;
                if (value > MaxRetention) value = MaxRetention;
                return value;
            }
        }

        public string SecondaryBackupPath
        {
            get { return _settings.GetString(SecondaryBackupPathKey, string.Empty); }
        }

        public bool SecondaryBackupEnabled
        {
            get { return _settings.GetBool(SecondaryBackupEnabledKey, true); }
        }

        /// <summary>每日首次启动调用；当天已有备份时直接返回 null。</summary>
        public string RunStartupBackupIfNeeded()
        {
            try
            {
                var status = GetStatus();
                if (status.LastBackupUtc.HasValue &&
                    status.LastBackupUtc.Value.ToLocalTime().Date == DateTime.Today)
                {
                    return null;
                }

                var path = CreateNow("每日首次启动自动备份");
                AppLog.Info("backup", "启动自动备份完成：" + path);
                return path;
            }
            catch (Exception ex)
            {
                // 备份失败不能阻断营业，但必须留下可诊断记录。
                AppLog.Error("backup", "启动自动备份失败", ex);
                return null;
            }
        }

        /// <summary>正常退出时调用；失败只记录日志。</summary>
        public string RunExitBackup()
        {
            try
            {
                var path = CreateNow("退出自动备份");
                AppLog.Info("backup", "退出自动备份完成：" + path);
                return path;
            }
            catch (Exception ex)
            {
                AppLog.Error("backup", "退出自动备份失败", ex);
                return null;
            }
        }

        /// <summary>立即备份（UI 按钮 / 验证脚本使用）。失败向上抛出，由界面用中文提示。</summary>
        public string CreateNow(string reason, IProgress<int> progress = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var directory = BackupDirectory;
            Directory.CreateDirectory(directory);

            var path = _backup.CreateTimestampedBackup(directory, progress, cancellationToken);
            _settings.SetString(LastBackupUtcKey, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            cancellationToken.ThrowIfCancellationRequested();
            CopyToSecondaryLocations(path);
            cancellationToken.ThrowIfCancellationRequested();
            Prune();
            if (progress != null) progress.Report(100);

            AppLog.Info("backup", "备份完成（" + reason + "）：" + path);
            return path;
        }

        public void Prune()
        {
            var directory = BackupDirectory;
            if (!Directory.Exists(directory)) return;

            var keep = Retention;
            var files = new DirectoryInfo(directory)
                .GetFiles("bookstore_*.db")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            foreach (var stale in files.Skip(keep))
            {
                try
                {
                    stale.Delete();
                    AppLog.Info("backup", "清理过期备份：" + stale.Name);
                }
                catch (Exception ex)
                {
                    AppLog.Warn("backup", "清理过期备份失败：" + stale.Name + " / " + ex.GetType().Name);
                }
            }
        }

        public BackupStatus GetStatus()
        {
            var directory = BackupDirectory;
            DateTime? last = null;

            if (Directory.Exists(directory))
            {
                var newest = new DirectoryInfo(directory)
                    .GetFiles("bookstore_*.db")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (newest != null) last = newest.LastWriteTimeUtc;
            }

            var stored = _settings.GetString(LastBackupUtcKey, string.Empty);
            DateTime parsed;
            if (DateTime.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
            {
                if (!last.HasValue || parsed > last.Value) last = parsed;
            }

            if (!last.HasValue)
            {
                return new BackupStatus(null, directory, BackupHealthLevel.Critical,
                    "还没有任何自动备份。请立即在“备份与恢复”创建一次，并选择 U 盘作为第二备份位置。");
            }

            var age = (DateTime.UtcNow - last.Value).TotalDays;
            if (age >= 7)
            {
                return new BackupStatus(last, directory, BackupHealthLevel.Critical,
                    "最近一次备份在 " + (int)age + " 天前，数据面临丢失风险，请立即备份到 U 盘。");
            }

            if (age >= 3)
            {
                return new BackupStatus(last, directory, BackupHealthLevel.Warning,
                    "最近一次备份在 " + (int)age + " 天前，建议今天关闭前完成备份。");
            }

            var secondaryError = _settings.GetString(LastSecondaryErrorKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(secondaryError))
                return new BackupStatus(last, directory, BackupHealthLevel.Warning,
                    "主备份已保存，但第二份备份未完成：" + secondaryError + " 请检查 U 盘或第二备份位置。");

            return new BackupStatus(last, directory, BackupHealthLevel.Ok,
                "最近一次备份：" + last.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        }

        /// <summary>只读校验最近一次备份（不触碰正式库），供设置页“验证最近一次备份”使用。</summary>
        public BackupVerificationResult VerifyLatestBackup()
        {
            var result = new BackupVerificationResult();
            var directory = BackupDirectory;
            if (!Directory.Exists(directory))
            {
                result.Detail = "备份目录不存在：" + directory;
                return result;
            }

            var newest = new DirectoryInfo(directory)
                .GetFiles("bookstore_*.db")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest == null)
            {
                result.Detail = "备份目录中没有 bookstore_*.db 文件。";
                return result;
            }

            result.Path = newest.FullName;

            try
            {
                BackupService.ValidateBackup(newest.FullName);
                result.Passed = true;
                result.Detail = "完整性、核心业务表和外键检查全部通过。";
                return result;
            }
            catch (Exception ex)
            {
                AppLog.Error("backup", "最近备份验证失败", ex);
                result.Detail = UserErrorText.For(ex);
                return result;
            }
        }

        /// <summary>可测试的保留策略：按时间倒序保留最近 keep 份。</summary>
        public static IList<DateTime> SelectForRetention(IEnumerable<DateTime> timestamps, int keep)
        {
            if (keep < 1) keep = 1;
            return timestamps
                .OrderByDescending(t => t)
                .Take(keep)
                .ToList();
        }

        private void CopyToSecondaryLocations(string backupPath)
        {
            if (!SecondaryBackupEnabled)
            {
                _settings.SetString(LastSecondaryErrorKey, "第二份备份已关闭。");
                return;
            }
            var fileName = Path.GetFileName(backupPath);
            var errors = new List<string>();
            var copies = 0;
            var configured = SecondaryBackupPath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (TryCopy(backupPath, configured, fileName)) copies++;
                else errors.Add("指定目录不可写或已断开");
            }
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Removable || !drive.IsReady) continue;
                    var target = Path.Combine(drive.RootDirectory.FullName, "BookDeskBackup");
                    if (TryCopy(backupPath, target, fileName)) copies++;
                    else errors.Add(drive.Name + " 写入失败");
                }
            }
            catch (Exception ex)
            {
                AppLog.Error("backup", "枚举可移动磁盘失败", ex);
                errors.Add("无法检查 U 盘");
            }
            if (copies == 0) errors.Add("尚未检测到可用的第二备份位置");
            _settings.SetString(LastSecondaryErrorKey, string.Join("；", errors.ToArray()));
        }

        private bool TryCopy(string sourcePath, string targetDirectory, string fileName)
        {
            var temporary = (string)null;
            try
            {
                if (string.Equals(Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(BackupDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    return false;
                Directory.CreateDirectory(targetDirectory);
                var target = Path.Combine(targetDirectory, fileName);
                temporary = target + ".writing-" + Guid.NewGuid().ToString("N");
                File.Copy(sourcePath, temporary);
                BackupService.ValidateBackup(temporary);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
                foreach (var stale in new DirectoryInfo(targetDirectory).GetFiles("bookstore_*.db")
                    .OrderByDescending(f => f.LastWriteTimeUtc).Skip(Retention))
                {
                    try { stale.Delete(); }
                    catch (Exception ex) { AppLog.Warn("backup", "第二位置旧备份清理失败：" + ex.GetType().Name); }
                }
                AppLog.Info("backup", "第二份备份写入并验证完成：" + target);
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Error("backup", "第二份备份写入失败：" + targetDirectory, ex);
                return false;
            }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) { AppLog.Warn("backup", "第二位置临时文件清理失败：" + ex.GetType().Name); }
            }
        }
    }
}
