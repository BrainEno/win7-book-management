using System;
using System.IO;
using NPOI.SS.UserModel;

namespace Win7BookManagement.Reporting
{
    internal static class WorkbookFile
    {
        public static void Publish(IWorkbook workbook, string path)
        {
            var full = Path.GetFullPath(path);
            var temporary = full + ".writing-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = File.Create(temporary)) workbook.Write(stream);
                if (File.Exists(full)) File.Replace(temporary, full, null);
                else File.Move(temporary, full);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
