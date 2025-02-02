namespace WeddingShare.Models.Migrator
{
    public class KeyMigrator
    {
        public KeyMigrator(string key, Func<string, string>? action = null)
        {
            Key = key;
            MigrationAction = action;
        }

        public string Key { get; set; }
        public Func<string, string>? MigrationAction { get; set; }
    }
}