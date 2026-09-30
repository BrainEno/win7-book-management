using System;
using Win7BookManagement.Database;

namespace Win7BookManagement.Repositories
{
    public sealed class SettingsRepository
    {
        public const string LowStockThresholdKey = "low_stock_threshold";
        public const string OnboardingCompletedKey = "onboarding_completed";
        public const string HomeGuideExpandedKey = "home_guide_expanded";

        private readonly DatabaseConnectionFactory _factory;

        public SettingsRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public string GetString(string key, string defaultValue)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT value FROM app_settings WHERE key=@key LIMIT 1;";
                command.Parameters.AddWithValue("@key", key);
                var value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? defaultValue : Convert.ToString(value);
            }
        }

        public void SetString(string key, string value)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT OR REPLACE INTO app_settings(key, value, updated_at)
VALUES(@key, @value, @updatedAt);";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value ?? "");
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        public int GetInt(string key, int defaultValue)
        {
            int parsed;
            return int.TryParse(GetString(key, defaultValue.ToString()), out parsed) ? parsed : defaultValue;
        }

        public void SetInt(string key, int value)
        {
            SetString(key, value.ToString());
        }

        public bool GetBool(string key, bool defaultValue)
        {
            var value = GetString(key, defaultValue ? "1" : "0");
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public void SetBool(string key, bool value)
        {
            SetString(key, value ? "1" : "0");
        }

        public int GetLowStockThreshold()
        {
            return GetInt(LowStockThresholdKey, 3);
        }

        public void SetLowStockThreshold(int threshold)
        {
            if (threshold < 0 || threshold > 9999)
                throw new InvalidOperationException("低库存阈值必须在 0 到 9999 之间。");
            SetInt(LowStockThresholdKey, threshold);
        }

        public bool IsOnboardingCompleted()
        {
            return GetBool(OnboardingCompletedKey, false);
        }

        public void SetOnboardingCompleted(bool completed)
        {
            SetBool(OnboardingCompletedKey, completed);
        }

        public bool IsHomeGuideExpanded()
        {
            // The full onboarding opens automatically on first launch. Keep the
            // dashboard helper compact by default so operational data remains
            // visible on 1024x768-era Windows 7 screens.
            return GetBool(HomeGuideExpandedKey, false);
        }

        public void SetHomeGuideExpanded(bool expanded)
        {
            SetBool(HomeGuideExpandedKey, expanded);
        }
    }
}
