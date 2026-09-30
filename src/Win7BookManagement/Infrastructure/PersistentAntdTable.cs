using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using Win7BookManagement.Repositories;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>
    /// AntdUI Table with durable, per-column user widths.
    ///
    /// AntdUI keeps interactive header-resize widths in a private transient map.
    /// After a drag finishes we promote those physical pixel widths into the
    /// public Column.Width values and persist logical (96-DPI) pixels in
    /// app_settings. This makes the layout survive page recreation, logout /
    /// login and application restarts while remaining DPI-independent.
    /// </summary>
    public sealed class PersistentAntdTable : AntdUI.Table
    {
        private const string SettingPrefix = "ui.table.column_width.";
        private const int MinimumPersistedWidth = 48;
        private const int MaximumPersistedWidth = 1200;

        private static readonly FieldInfo TransientWidthsField =
            typeof(AntdUI.Table).GetField(
                "tmpcol_width",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private SettingsRepository _settings;
        private string _layoutKey;
        private bool _promotingWidths;

        public void ConfigureColumnPersistence(SettingsRepository settings, string layoutKey)
        {
            _settings = settings;
            _layoutKey = (layoutKey ?? "").Trim();
            RestorePersistedColumnWidths();
        }

        public void RestorePersistedColumnWidths()
        {
            if (_settings == null || string.IsNullOrWhiteSpace(_layoutKey) || Columns == null)
                return;

            foreach (AntdUI.Column column in Columns)
            {
                if (column == null || string.IsNullOrWhiteSpace(column.Key))
                    continue;

                var saved = _settings.GetInt(BuildSettingKey(column.Key), -1);
                if (saved >= MinimumPersistedWidth && saved <= MaximumPersistedWidth)
                    column.Width = saved.ToString();
            }

            ClearTransientWidths();
            LoadLayout();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_promotingWidths || _settings == null || string.IsNullOrWhiteSpace(_layoutKey))
                return;

            PromoteAndPersistInteractiveWidths();
        }

        private void PromoteAndPersistInteractiveWidths()
        {
            var transient = GetTransientWidths();
            if (transient == null || transient.Count == 0 || Columns == null)
                return;

            var visibleColumns = new List<AntdUI.Column>();
            foreach (AntdUI.Column column in Columns)
            {
                if (column != null && column.Visible)
                    visibleColumns.Add(column);
            }

            if (visibleColumns.Count == 0)
                return;

            var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            var promotedAny = false;

            try
            {
                _promotingWidths = true;

                foreach (var pair in transient)
                {
                    if (pair.Key < 0 || pair.Key >= visibleColumns.Count)
                        continue;

                    var column = visibleColumns[pair.Key];
                    if (string.IsNullOrWhiteSpace(column.Key))
                        continue;

                    var logicalWidth = (int)Math.Round(pair.Value * 96.0 / dpi);
                    if (logicalWidth < MinimumPersistedWidth)
                        logicalWidth = MinimumPersistedWidth;
                    if (logicalWidth > MaximumPersistedWidth)
                        logicalWidth = MaximumPersistedWidth;

                    column.Width = logicalWidth.ToString();
                    _settings.SetInt(BuildSettingKey(column.Key), logicalWidth);
                    promotedAny = true;
                }

                transient.Clear();

                if (promotedAny)
                {
                    LoadLayout();
                    Invalidate();
                }
            }
            catch
            {
                // Column width persistence is a convenience feature. A failure
                // here must never interrupt selling, receiving or master-data
                // work.
            }
            finally
            {
                _promotingWidths = false;
            }
        }

        private Dictionary<int, int> GetTransientWidths()
        {
            if (TransientWidthsField == null)
                return null;

            try
            {
                return TransientWidthsField.GetValue(this) as Dictionary<int, int>;
            }
            catch
            {
                return null;
            }
        }

        private void ClearTransientWidths()
        {
            var transient = GetTransientWidths();
            if (transient != null)
                transient.Clear();
        }

        private string BuildSettingKey(string columnKey)
        {
            return SettingPrefix + _layoutKey + "." + columnKey;
        }
    }
}
