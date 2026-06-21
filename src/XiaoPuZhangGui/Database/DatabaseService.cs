using System.Data.SQLite;
using System.IO;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Database
{
    internal static class DatabaseService
    {
        public static void Initialize(string databasePath)
        {
            string databaseDirectory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(databaseDirectory) && !Directory.Exists(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            bool isNewDatabase = !File.Exists(databasePath);
            if (isNewDatabase)
            {
                SQLiteConnection.CreateFile(databasePath);
            }

            string connectionString = BuildConnectionString(databasePath);
            ExecuteSchema(connectionString);

            CategoryRepository categoryRepository = new CategoryRepository(connectionString);
            categoryRepository.EnsureDefaultCategories();
        }

        public static string BuildConnectionString(string databasePath)
        {
            SQLiteConnectionStringBuilder builder = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3,
                ForeignKeys = true
            };

            return builder.ToString();
        }

        private static void ExecuteSchema(string connectionString)
        {
            string schema = File.ReadAllText(GetSchemaPath());

            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = schema;
                command.ExecuteNonQuery();
            }
        }

        private static string GetSchemaPath()
        {
            string outputPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Database", "schema.sql");
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            return Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "schema.sql");
        }
    }
}
