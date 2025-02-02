using WeddingShare.Helpers.Migrators;

namespace WeddingShare.Helpers
{
    public interface IConfigHelper
    {
        string? GetEnvironmentVariable(string key);
        string? GetConfigValue(string key);
        string? Get(string key);
        string GetOrDefault(string key, string defaultValue);
        int GetOrDefault(string key, int defaultValue);
        long GetOrDefault(string key, long defaultValue);
        decimal GetOrDefault(string key, decimal defaultValue);
        double GetOrDefault(string key, double defaultValue);
        bool GetOrDefault(string key, bool defaultValue);
        DateTime? GetOrDefault(string key, DateTime? defaultValue);
    }

    public class ConfigHelper : IConfigHelper
    {
        private readonly IEnvironmentWrapper _environment;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public ConfigHelper(IEnvironmentWrapper environment, IConfiguration config, ILogger<ConfigHelper> logger)
        {
            _environment = environment;
            _configuration = config;
            _logger = logger;
        }

        public string? GetEnvironmentVariable(string key)
        {
            try
            {
                foreach (var envKey in KeyHelper.GetAlternateVersions(key))
                { 
                    if (!this.IsProtectedVariable(envKey))
                    {
                        var keyName = string.Join('_', envKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1)).Trim('_').ToUpper();

                        var value = _environment.GetEnvironmentVariable(keyName);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get environment variable '{key}'");
            }

            return null;
        }

        public string? GetConfigValue(string key)
        {
            try
            {
                var value = _configuration.GetValue<string>(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get config value '{key}'");
            }

            return null;
        }

        public string? Get(string key)
        {
            try
            {
                var alertnateKeys = GetAlternateKeys(key);

                foreach (var alertnateKey in alertnateKeys)
                { 
                    var value = this.GetEnvironmentVariable(alertnateKey);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                foreach (var alertnateKey in alertnateKeys)
                {
                    var value = this.GetConfigValue(alertnateKey);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to find key '{key}' in either environment variables or appsettings");
            }

            return null;
        }

        public string GetOrDefault(string key, string defaultValue)
        {
            try
            {
                var value = this.Get(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch { }

            return defaultValue;
        }

        public int GetOrDefault(string key, int defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToInt32(value);
                }
            }
            catch { }

            return defaultValue;
        }

        public long GetOrDefault(string key, long defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToInt64(value);
                }
            }
            catch { }

            return defaultValue;
        }

        public decimal GetOrDefault(string key, decimal defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToDecimal(value);
                }
            }
            catch { }

            return defaultValue;
        }

        public double GetOrDefault(string key, double defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToDouble(value);
                }
            }
            catch { }

            return defaultValue;
        }

        public bool GetOrDefault(string key, bool defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToBoolean(value);
                }
            }
            catch { }

            return defaultValue;
        }

        public DateTime? GetOrDefault(string key, DateTime? defaultValue)
        {
            try
            {
                var value = this.GetOrDefault(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return Convert.ToDateTime(value);
                }
            }
            catch { }

            return defaultValue;
        }

        private bool IsProtectedVariable(string key)
        {
            switch (key.Replace(":", "_").Trim('_').ToUpper())
            {
                case "RELEASE_VERSION":
                    return true;
                default:
                    return false;
            }
        }

        private List<string> GetAlternateKeys(string key)
        {
            var keys = new List<string>();

            try
            {
                key = key.Trim();
                keys.Add(key);

                if (string.Equals(key, "Settings:Identity_Check:Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add("Settings:Show_Identity_Request");
                }
                else if (string.Equals(key, "Settings:Show_Identity_Request", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add("Settings:Identity_Check:Enabled");
                }
            }
            catch { }
                
            return keys.Distinct().ToList();
        }
    }
}