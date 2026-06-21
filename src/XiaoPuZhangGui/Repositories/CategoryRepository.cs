using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class CategoryRepository
    {
        private static readonly string[] DefaultCategories =
        {
            "烟酒",
            "饮料",
            "零食",
            "方便食品",
            "日用品",
            "调味品",
            "冷冻食品",
            "其他"
        };

        private readonly string _connectionString;

        public CategoryRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void EnsureDefaultCategories()
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    foreach (string categoryName in DefaultCategories)
                    {
                        using (SQLiteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"
INSERT OR IGNORE INTO categories (name, is_active, created_at)
VALUES (@name, 1, @created_at);";
                            command.Parameters.AddWithValue("@name", categoryName);
                            command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public IList<Category> GetActiveCategories()
        {
            List<Category> categories = new List<Category>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT id, name, is_active, created_at
FROM categories
WHERE is_active = 1
ORDER BY sort_order ASC, id ASC;";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new Category
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            IsActive = reader.GetInt32(2) == 1,
                            CreatedAt = DateTime.Parse(reader.GetString(3))
                        });
                    }
                }
            }

            return categories;
        }
    }
}
