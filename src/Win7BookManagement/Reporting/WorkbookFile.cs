using System;
using System.IO;
using System.Threading;
using NPOI.SS.UserModel;

namespace Win7BookManagement.Reporting
{
    internal static class WorkbookFile
    {
        public static string ValidateExtension(string path, bool allowCsv)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("导出路径不能为空。", "path");
            var extension = Path.GetExtension(path);
            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase) ||
                (allowCsv && string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)))
                return extension;
            throw new InvalidOperationException(allowCsv
                ? "文件扩展名不受支持。请选择 .xlsx、.xls 或 .csv 格式。"
                : "月报只支持 .xlsx 或 .xls，请检查文件扩展名。");
        }

        public static void Publish(IWorkbook workbook, string path, CancellationToken cancellationToken)
        {
            if (workbook == null) throw new ArgumentNullException("workbook");
            PublishCore(path, cancellationToken, stream => workbook.Write(stream));
        }

        private static void PublishCore(string path, CancellationToken cancellationToken, Action<Stream> write)
        {
            var full = Path.GetFullPath(path);
            var temporary = full + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var file = File.Create(temporary))
                using (var stream = new CancellableWriteStream(file, cancellationToken))
                    write(stream);

                // Publication is the point of no return: once the target is
                // replaced, report success rather than a misleading cancellation.
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private sealed class CancellableWriteStream : Stream
        {
            private readonly Stream _inner;
            private readonly CancellationToken _token;
            public CancellableWriteStream(Stream inner, CancellationToken token) { _inner = inner; _token = token; }
            public override bool CanRead { get { return _inner.CanRead; } }
            public override bool CanSeek { get { return _inner.CanSeek; } }
            public override bool CanWrite { get { return _inner.CanWrite; } }
            public override long Length { get { return _inner.Length; } }
            public override long Position { get { return _inner.Position; } set { _inner.Position = value; } }
            public override void Flush() { _token.ThrowIfCancellationRequested(); _inner.Flush(); }
            public override int Read(byte[] buffer, int offset, int count) { return _inner.Read(buffer, offset, count); }
            public override long Seek(long offset, SeekOrigin origin) { return _inner.Seek(offset, origin); }
            public override void SetLength(long value) { _token.ThrowIfCancellationRequested(); _inner.SetLength(value); }
            public override void Write(byte[] buffer, int offset, int count)
            {
                _token.ThrowIfCancellationRequested();
                _inner.Write(buffer, offset, count);
                _token.ThrowIfCancellationRequested();
            }
            public override void WriteByte(byte value)
            {
                _token.ThrowIfCancellationRequested();
                _inner.WriteByte(value);
                _token.ThrowIfCancellationRequested();
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
