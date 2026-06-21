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
                    for (int i = 0; i < DefaultCategories.Length; i++)
                    {
                        using (SQLiteCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = @"
INSERT OR IGNORE INTO categories (name, is_active, sort_order, created_at)
VALUES (@name, 1, @sort_order, @created_at);";
                            command.Parameters.AddWithValue("@name", DefaultCategories[i]);
                            command.Parameters.AddWithValue("@sort_order", i + 1);
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
            return GetCategories(false);
        }

        public IList<Category> GetAllCategories()
        {
            return GetCategories(true);
        }

        public Category GetById(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT id, name, is_active, sort_order, created_at, updated_at
FROM categories
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadCategory(reader) : null;
                }
            }
        }

        public bool ExistsByName(string name, long? excludedId)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT COUNT(1)
FROM categories
WHERE name = @name AND (@excluded_id IS NULL OR id <> @excluded_id);";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@excluded_id", excludedId.HasValue ? (object)excludedId.Value : DBNull.Value);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public void Add(string name)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
INSERT INTO categories (name, is_active, sort_order, created_at)
VALUES (@name, 1, (SELECT IFNULL(MAX(sort_order), 0) + 1 FROM categories), @created_at);";
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        public void Rename(long id, string name)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
UPDATE categories
SET name = @name,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        public void SetActive(long id, bool isActive)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
UPDATE categories
SET is_active = @is_active,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@is_active", isActive ? 1 : 0);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private IList<Category> GetCategories(bool includeInactive)
        {
            List<Category> categories = new List<Category>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT id, name, is_active, sort_order, created_at, updated_at
FROM categories
WHERE @include_inactive = 1 OR is_active = 1
ORDER BY sort_order ASC, id ASC;";
                command.Parameters.AddWithValue("@include_inactive", includeInactive ? 1 : 0);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(ReadCategory(reader));
                    }
                }
            }

            return categories;
        }

        private static Category ReadCategory(SQLiteDataReader reader)
        {
            return new Category
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                IsActive = reader.GetInt32(2) == 1,
                SortOrder = reader.GetInt32(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = reader.IsDBNull(5) ? (DateTime?)null : DateTime.Parse(reader.GetString(5))
            };
        }
    }
}
