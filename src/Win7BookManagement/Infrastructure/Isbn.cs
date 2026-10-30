using System;
using System.Text;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>ISBN is optional; when present it must be a valid ISBN-10 or ISBN-13.</summary>
    public static class Isbn
    {
        public static string Normalize(string value)
        {
            string normalized;
            if (!TryNormalize(value, out normalized)) throw Invalid();
            return normalized;
        }

        /// <summary>
        /// UI-friendly ISBN normalization. Empty input is valid because many
        /// self-published books do not have an ISBN. Invalid non-empty input
        /// returns false instead of throwing, so the caller can offer a
        /// non-blocking "save without ISBN" path.
        /// </summary>
        public static bool TryNormalize(string value, out string normalized)
        {
            normalized = "";
            var text = (value ?? "").Trim();
            if (text.Length == 0) return true;

            var digits = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsDigit(ch)) digits.Append(ch);
                else if ((ch == 'X' || ch == 'x') && digits.Length == 9) digits.Append('X');
                else if (ch == '-' || char.IsWhiteSpace(ch)) continue;
                else return false;
            }

            var s = digits.ToString();
            if (s.Length == 10)
            {
                var sum = 0;
                for (var i = 0; i < 9; i++) sum += (s[i] - '0') * (10 - i);
                var check = s[9] == 'X' ? 10 : s[9] - '0';
                if (s[9] != 'X' && !char.IsDigit(s[9])) return false;
                if ((sum + check) % 11 != 0) return false;

                s = "978" + s.Substring(0, 9);
                var prefixTotal = 0;
                for (var i = 0; i < 12; i++) prefixTotal += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
                s += ((10 - prefixTotal % 10) % 10).ToString();
            }

            if (s.Length != 13 || s.IndexOf('X') >= 0) return false;
            var total = 0;
            for (var i = 0; i < 12; i++) total += (s[i] - '0') * (i % 2 == 0 ? 1 : 3);
            if ((10 - total % 10) % 10 != s[12] - '0') return false;

            normalized = s;
            return true;
        }

        public static bool LooksLikeIsbn(string value)
        {
            var count = 0;
            foreach (var ch in value ?? "")
            {
                if (char.IsDigit(ch)) count++;
                else if (ch == 'X' || ch == 'x' || ch == '-' || char.IsWhiteSpace(ch)) continue;
                else return false;
            }
            return count == 10 || count == 13;
        }

        private static InvalidOperationException Invalid()
        {
            return new InvalidOperationException("ISBN 格式或校验位不正确。请核对扫码或手输内容；ISBN-10 会自动转换为 ISBN-13。无 ISBN 的自出版物请留空 ISBN，系统将使用店内编码。");
        }
    }
}
