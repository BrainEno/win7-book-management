using System;
using Win7BookManagement.Database;

namespace Win7BookManagement.Repositories
{
    public sealed class SettingsRepository
    {
        public const string LowStockThresholdKey = "low_stock_threshold";
        private readonly DatabaseConnectionFactory _factory;

        public SettingsRepository(DatabaseConnectionFactory factory)
        {
            _factory = factory;
        }

        public int GetInt(string key, int defaultValue)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT value FROM app_settings WHERE key=@key LIMIT 1;";
                command.Parameters.AddWithValue("@key", key);
                var value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value) return defaultValue;

                int parsed;
                return int.TryParse(Convert.ToString(value), out parsed) ? parsed : defaultValue;
            }
        }

        public void SetInt(string key, int value)
        {
            using (var connection = _factory.Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
INSERT OR REPLACE INTO app_settings(key, value, updated_at)
VALUES(@key, @value, @updatedAt);";
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value.ToString());
                command.Parameters.AddWithValue("@updatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
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
    }
}
