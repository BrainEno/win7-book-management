using System;
using Win7BookManagement.Infrastructure;

namespace Win7BookManagement.Services
{
    /// <summary>
    /// Result of applying the optional-ISBN rule before saving book master data.
    /// Invalid ISBN input never becomes a stored ISBN; the user may explicitly
    /// continue with an empty ISBN while the raw input is preserved in the note.
    /// </summary>
    public sealed class BookIsbnSavePlan
    {
        internal BookIsbnSavePlan(string isbn, string note, bool invalidIsbnNeedsConfirmation, string rawIsbnInput)
        {
            Isbn = isbn;
            Note = note;
            InvalidIsbnNeedsConfirmation = invalidIsbnNeedsConfirmation;
            RawIsbnInput = rawIsbnInput;
        }

        public string Isbn { get; private set; }
        public string Note { get; private set; }
        public bool InvalidIsbnNeedsConfirmation { get; private set; }
        public string RawIsbnInput { get; private set; }
    }

    /// <summary>
    /// Business rule for beginner-friendly book creation: ISBN is optional.
    /// A malformed ISBN may be cleared with confirmation, but it must never be
    /// silently stored as a valid ISBN or replaced with a fabricated ISBN.
    /// </summary>
    public static class BookIsbnSavePolicy
    {
        public const string UnverifiedIsbnNotePrefix = "未校验 ISBN 输入：";

        public static BookIsbnSavePlan CreatePlan(string isbnInput, string currentNote)
        {
            var raw = NormalizeRawInput(isbnInput);
            string normalized;
            if (Isbn.TryNormalize(raw, out normalized))
                return new BookIsbnSavePlan(normalized, currentNote ?? "", false, raw);

            var note = AppendUnverifiedIsbnNote(currentNote, raw);
            return new BookIsbnSavePlan("", note, true, raw);
        }

        public static string AppendUnverifiedIsbnNote(string currentNote, string rawIsbnInput)
        {
            var note = currentNote ?? "";
            var raw = NormalizeRawInput(rawIsbnInput);
            if (raw.Length == 0) return note;

            var line = UnverifiedIsbnNotePrefix + raw;
            if (note.IndexOf(line, StringComparison.Ordinal) >= 0) return note;
            if (note.Trim().Length == 0) return line;
            return note.TrimEnd() + Environment.NewLine + line;
        }

        private static string NormalizeRawInput(string value)
        {
            var text = (value ?? "").Trim();
            if (text.Length == 0) return "";
            return text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
