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
            RunMigrations(connectionString);

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

        private static void RunMigrations(string connectionString)
        {
            using (SQLiteConnection connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                bool addedMinStockAlert = EnsureColumn(connection, "products", "min_stock_alert", "NUMERIC NOT NULL DEFAULT 0");
                bool addedRequiresExpiry = EnsureColumn(connection, "products", "requires_expiry", "INTEGER NOT NULL DEFAULT 1");
                EnsureColumn(connection, "products", "expiry_date", "TEXT NULL");
                bool addedStatus = EnsureColumn(connection, "products", "status", "TEXT NOT NULL DEFAULT '在售'");

                if (addedMinStockAlert)
                {
                    CopyColumnValue(connection, "products", "min_stock", "min_stock_alert");
                }

                if (addedRequiresExpiry)
                {
                    CopyColumnValue(connection, "products", "enable_shelf_life", "requires_expiry");
                }

                if (addedStatus && HasColumn(connection, "products", "is_active"))
                {
                    using (SQLiteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = @"
UPDATE products
SET status = CASE WHEN is_active = 1 THEN '在售' ELSE '停用' END
WHERE 1 = 1;";
                        command.ExecuteNonQuery();
                    }
                }

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE INDEX IF NOT EXISTS idx_products_status ON products(status);";
                    command.ExecuteNonQuery();
                }
            }
        }

        private static bool EnsureColumn(SQLiteConnection connection, string tableName, string columnName, string columnDefinition)
        {
            if (HasColumn(connection, tableName, columnName))
            {
                return false;
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = string.Format("ALTER TABLE {0} ADD COLUMN {1} {2};", tableName, columnName, columnDefinition);
                command.ExecuteNonQuery();
            }

            return true;
        }

        private static bool HasColumn(SQLiteConnection connection, string tableName, string columnName)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(" + tableName + ");";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == columnName)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static void CopyColumnValue(SQLiteConnection connection, string tableName, string sourceColumn, string targetColumn)
        {
            if (!HasColumn(connection, tableName, sourceColumn) || !HasColumn(connection, tableName, targetColumn))
            {
                return;
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = string.Format("UPDATE {0} SET {1} = {2};", tableName, targetColumn, sourceColumn);
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
