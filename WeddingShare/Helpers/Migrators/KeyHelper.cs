using WeddingShare.Models.Migrator;

namespace WeddingShare.Helpers.Migrators
{
    public class KeyHelper
    {
        public static List<KeyMigrator> GetAlternateVersions(string key)
        {
            var keys = new List<KeyMigrator>();

            try
            {
                key = key.Trim();
                keys.Add(new KeyMigrator(key));

                if (string.Equals(key, "Settings:Gallery:Enable_QR_Code", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Disable_QR_Code", (v) => { return (bool.Parse(v) == false).ToString(); }));
                }
                else if (string.Equals(key, "Settings:Identity_Check:Enabled", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Show_Identity_Request"));
                }
                else if (string.Equals(key, "Settings:Show_Identity_Request", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Identity_Check:Enabled", (v) => { return (bool.Parse(v) == false).ToString(); }));
                }
                else if (string.Equals(key, "Settings:Account:Admins:Username", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Admin:Username"));
                }
                else if (string.Equals(key, "Settings:Account:Admins:Password", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Admin:Password"));
                }
                else if (string.Equals(key, "Settings:Account:Admins:Log_Password", StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(new KeyMigrator("Settings:Admin:Log_Password"));
                }
            }
            catch { }

            return keys.Distinct().ToList();
        }
    }
}