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

                EnsurePurchaseColumns(connection);
                EnsurePurchaseIndexes(connection);
                EnsureSalesColumns(connection);
                EnsureSalesIndexes(connection);
                EnsureInventoryColumns(connection);
                EnsureInventoryIndexes(connection);
            }
        }

        private static void EnsurePurchaseColumns(SQLiteConnection connection)
        {
            EnsureColumn(connection, "purchase_records", "purchase_no", "TEXT NULL");
            EnsureColumn(connection, "purchase_records", "purchase_date", "TEXT NULL");
            EnsureColumn(connection, "purchase_records", "updated_at", "TEXT NULL");
            if (HasColumn(connection, "purchase_records", "purchased_at"))
            {
                CopyDateColumnWhenEmpty(connection, "purchase_records", "purchased_at", "purchase_date");
            }

            EnsureColumn(connection, "purchase_items", "product_name_snapshot", "TEXT NULL");
            EnsureColumn(connection, "purchase_items", "purchase_price", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "purchase_items", "line_total", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "purchase_items", "production_date", "TEXT NULL");
            EnsureColumn(connection, "purchase_items", "created_at", "TEXT NULL");
            EnsureColumn(connection, "purchase_items", "updated_at", "TEXT NULL");
            if (HasColumn(connection, "purchase_items", "unit_cost"))
            {
                CopyColumnValue(connection, "purchase_items", "unit_cost", "purchase_price");
            }

            EnsureColumn(connection, "stock_batches", "batch_code", "TEXT NULL");
            EnsureColumn(connection, "stock_batches", "source_type", "TEXT NULL");
            EnsureColumn(connection, "stock_batches", "source_id", "INTEGER NULL");
            EnsureColumn(connection, "stock_batches", "quantity_in", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "stock_batches", "quantity_remaining", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "stock_batches", "purchase_price", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "stock_batches", "production_date", "TEXT NULL");
            EnsureColumn(connection, "stock_batches", "updated_at", "TEXT NULL");
            if (HasColumn(connection, "stock_batches", "quantity"))
            {
                CopyColumnValue(connection, "stock_batches", "quantity", "quantity_in");
            }

            if (HasColumn(connection, "stock_batches", "remaining_quantity"))
            {
                CopyColumnValue(connection, "stock_batches", "remaining_quantity", "quantity_remaining");
            }

            if (HasColumn(connection, "stock_batches", "unit_cost"))
            {
                CopyColumnValue(connection, "stock_batches", "unit_cost", "purchase_price");
            }
        }

        private static void EnsurePurchaseIndexes(SQLiteConnection connection)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_purchase_records_purchase_date ON purchase_records(purchase_date);
CREATE INDEX IF NOT EXISTS idx_purchase_items_product_id ON purchase_items(product_id);
CREATE INDEX IF NOT EXISTS idx_stock_batches_batch_code ON stock_batches(batch_code);";
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureSalesColumns(SQLiteConnection connection)
        {
            EnsureColumn(connection, "sales_orders", "sale_time", "TEXT NULL");
            EnsureColumn(connection, "sales_orders", "total_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_orders", "total_cost", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_orders", "gross_profit", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_orders", "paid_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_orders", "remark", "TEXT NULL");
            EnsureColumn(connection, "sales_orders", "updated_at", "TEXT NULL");

            if (HasColumn(connection, "sales_orders", "sold_at"))
            {
                CopyDateColumnWhenEmpty(connection, "sales_orders", "sold_at", "sale_time");
            }

            if (HasColumn(connection, "sales_orders", "receivable_amount"))
            {
                CopyColumnValueWhenZero(connection, "sales_orders", "receivable_amount", "total_amount");
            }

            if (HasColumn(connection, "sales_orders", "cost_amount"))
            {
                CopyColumnValueWhenZero(connection, "sales_orders", "cost_amount", "total_cost");
            }

            EnsureColumn(connection, "sales_items", "line_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_items", "line_cost", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_items", "line_profit", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "sales_items", "created_at", "TEXT NULL");
            EnsureColumn(connection, "sales_items", "updated_at", "TEXT NULL");

            if (HasColumn(connection, "sales_items", "profit_snapshot"))
            {
                CopyColumnValueWhenZero(connection, "sales_items", "profit_snapshot", "line_profit");
            }
        }

        private static void EnsureSalesIndexes(SQLiteConnection connection)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_sales_orders_sale_time ON sales_orders(sale_time);
CREATE INDEX IF NOT EXISTS idx_sales_items_product_id ON sales_items(product_id);";
                command.ExecuteNonQuery();
            }
        }

        private static void EnsureInventoryColumns(SQLiteConnection connection)
        {
            EnsureColumn(connection, "inventory_checks", "check_no", "TEXT NULL");
            EnsureColumn(connection, "inventory_checks", "check_date", "TEXT NULL");
            EnsureColumn(connection, "inventory_checks", "total_profit_quantity", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_checks", "total_loss_quantity", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_checks", "total_profit_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_checks", "total_loss_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_checks", "remark", "TEXT NULL");
            EnsureColumn(connection, "inventory_checks", "created_at", "TEXT NULL");
            EnsureColumn(connection, "inventory_checks", "updated_at", "TEXT NULL");

            EnsureColumn(connection, "inventory_check_items", "inventory_check_id", "INTEGER NULL");
            EnsureColumn(connection, "inventory_check_items", "product_id", "INTEGER NULL");
            EnsureColumn(connection, "inventory_check_items", "product_name_snapshot", "TEXT NULL");
            EnsureColumn(connection, "inventory_check_items", "system_stock", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_check_items", "actual_stock", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_check_items", "difference_quantity", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_check_items", "cost_price_snapshot", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_check_items", "difference_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "inventory_check_items", "reason", "TEXT NULL");
            EnsureColumn(connection, "inventory_check_items", "remark", "TEXT NULL");
            EnsureColumn(connection, "inventory_check_items", "created_at", "TEXT NULL");
            EnsureColumn(connection, "inventory_check_items", "updated_at", "TEXT NULL");

            EnsureColumn(connection, "scrap_records", "scrap_no", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "scrap_date", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "product_id", "INTEGER NULL");
            EnsureColumn(connection, "scrap_records", "product_name_snapshot", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "quantity", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "scrap_records", "cost_price_snapshot", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "scrap_records", "loss_amount", "NUMERIC NOT NULL DEFAULT 0");
            EnsureColumn(connection, "scrap_records", "reason", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "remark", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "created_at", "TEXT NULL");
            EnsureColumn(connection, "scrap_records", "updated_at", "TEXT NULL");
        }

        private static void EnsureInventoryIndexes(SQLiteConnection connection)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
CREATE INDEX IF NOT EXISTS idx_inventory_checks_check_date ON inventory_checks(check_date);
CREATE INDEX IF NOT EXISTS idx_inventory_check_items_product_id ON inventory_check_items(product_id);
CREATE INDEX IF NOT EXISTS idx_scrap_records_scrap_date ON scrap_records(scrap_date);
CREATE INDEX IF NOT EXISTS idx_scrap_records_product_id ON scrap_records(product_id);";
                command.ExecuteNonQuery();
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

        private static void CopyColumnValueWhenZero(SQLiteConnection connection, string tableName, string sourceColumn, string targetColumn)
        {
            if (!HasColumn(connection, tableName, sourceColumn) || !HasColumn(connection, tableName, targetColumn))
            {
                return;
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = string.Format(
                    "UPDATE {0} SET {1} = {2} WHERE {1} IS NULL OR {1} = 0;",
                    tableName,
                    targetColumn,
                    sourceColumn);
                command.ExecuteNonQuery();
            }
        }

        private static void CopyDateColumnWhenEmpty(SQLiteConnection connection, string tableName, string sourceColumn, string targetColumn)
        {
            if (!HasColumn(connection, tableName, sourceColumn) || !HasColumn(connection, tableName, targetColumn))
            {
                return;
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = string.Format(
                    "UPDATE {0} SET {1} = {2} WHERE {1} IS NULL OR {1} = '';",
                    tableName,
                    targetColumn,
                    sourceColumn);
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
