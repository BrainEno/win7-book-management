using System;
using System.Data.SQLite;
using System.IO;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// P1-1：把技术异常翻译成店员能执行的中文建议。
    /// 所有 MessageBox 中原本直接使用 ex.Message 的位置，改为 UserErrorText.For(ex)。
    /// </summary>
    public static class UserErrorText
    {
        public static string For(Exception exception)
        {
            if (exception == null) return "未知错误。";

            if (exception is AggregateException && exception.InnerException != null)
                return For(exception.InnerException);

            var sqlite = exception as SQLiteException;
            if (sqlite != null)
            {
                switch (sqlite.ResultCode)
                {
                    case SQLiteErrorCode.Busy:
                    case SQLiteErrorCode.Locked:
                        return "数据库正被占用，请稍等几秒再试（不要同时打开多个 BOOK DESK 窗口）。";
                    case SQLiteErrorCode.Constraint:
                        return "数据不满足业务规则：例如库存不能为负、ISBN 重复或必填项为空。请检查后重试。";
                    case SQLiteErrorCode.ReadOnly:
                        return "数据库是只读的。常见原因是数据目录权限不足或文件被占用。";
                    case SQLiteErrorCode.Full:
                        return "磁盘空间不足，无法写入数据库。请清理磁盘或把数据目录迁到空间更大的盘。";
                    case SQLiteErrorCode.Corrupt:
                    case SQLiteErrorCode.NotADb:
                        return "数据库文件可能已损坏。请停止操作，使用“备份与恢复”恢复最近一次有效备份。";
                    default:
                        return "数据库操作失败（" + sqlite.ResultCode + "）。请重试；反复失败请联系维护人员。";
                }
            }

            if (exception is UnauthorizedAccessException)
                return "没有权限访问该文件或目录。请用管理员身份运行，或把数据/备份目录改到当前用户可写的位置。";
            if (exception is DirectoryNotFoundException)
                return "目标目录不存在，请重新选择位置。";
            if (exception is FileNotFoundException)
                return "找不到需要的文件，可能已被移动或删除，请重新选择。";
            if (exception is IOException)
                return "读写文件失败，可能是 U 盘已拔出、磁盘已满或文件被其它程序占用。";
            if (exception is OutOfMemoryException)
                return "可用内存不足。请关闭其它程序，或使用较小的日期范围再导出。";
            if (exception is ArgumentException)
                return "输入的内容有问题：" + exception.Message;
            if (exception is InvalidOperationException)
                return "当前状态不允许这个操作，请先刷新数据或重新打开该页面。";

            return "操作失败（" + exception.GetType().Name + "）。请重试；如果持续出现，请导出诊断日志。";
        }
    }
}
