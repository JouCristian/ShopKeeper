using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class ProductRepository
    {
        private readonly string _connectionString;

        public ProductRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<Product> Search(string keyword, long? categoryId, string status)
        {
            List<Product> products = new List<Product>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT p.id, p.name, p.category_id, c.name AS category_name, p.barcode, p.specification,
       p.default_price, p.current_stock, p.average_cost, p.min_stock_alert,
       p.requires_expiry, p.expiry_date, p.status, p.remark, p.created_at, p.updated_at
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
WHERE (@keyword = '' OR p.name LIKE @keyword_like OR IFNULL(p.barcode, '') LIKE @keyword_like)
  AND (@category_id IS NULL OR p.category_id = @category_id)
  AND ((@status = '全部' AND IFNULL(p.status, '在售') <> '已删除') OR p.status = @status)
ORDER BY p.status ASC, p.id DESC;";
                command.Parameters.AddWithValue("@keyword", keyword ?? string.Empty);
                command.Parameters.AddWithValue("@keyword_like", "%" + (keyword ?? string.Empty) + "%");
                command.Parameters.AddWithValue("@category_id", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? "全部" : status);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(ReadProduct(reader));
                    }
                }
            }

            return products;
        }

        public Product GetById(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT p.id, p.name, p.category_id, c.name AS category_name, p.barcode, p.specification,
       p.default_price, p.current_stock, p.average_cost, p.min_stock_alert,
       p.requires_expiry, p.expiry_date, p.status, p.remark, p.created_at, p.updated_at
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
WHERE p.id = @id;";
                command.Parameters.AddWithValue("@id", id);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadProduct(reader) : null;
                }
            }
        }

        public long Insert(Product product)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
INSERT INTO products
    (name, category_id, barcode, specification, default_price, current_stock, average_cost,
     min_stock_alert, requires_expiry, expiry_date, status, remark, created_at)
VALUES
    (@name, @category_id, @barcode, @specification, @default_price, @current_stock, @average_cost,
     @min_stock_alert, @requires_expiry, @expiry_date, @status, @remark, @created_at);
SELECT last_insert_rowid();";
                AddParameters(command, product);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        public void Update(Product product)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
UPDATE products
SET name = @name,
    category_id = @category_id,
    barcode = @barcode,
    specification = @specification,
    default_price = @default_price,
    current_stock = @current_stock,
    average_cost = @average_cost,
    min_stock_alert = @min_stock_alert,
    requires_expiry = @requires_expiry,
    expiry_date = @expiry_date,
    status = @status,
    remark = @remark,
    updated_at = @updated_at
WHERE id = @id;";
                AddParameters(command, product);
                command.Parameters.AddWithValue("@id", product.Id);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        public void SetStatus(long id, string status)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
UPDATE products
SET status = @status,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        public void Delete(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                if (HasBusinessRecords(connection, id))
                {
                    SetStatus(connection, id, "已删除");
                    return;
                }

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM products WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    if (command.ExecuteNonQuery() == 0)
                    {
                        throw new InvalidOperationException("商品不存在或已被删除。");
                    }
                }
            }
        }

        private static void SetStatus(SQLiteConnection connection, long id, string status)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE products
SET status = @status,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                if (command.ExecuteNonQuery() == 0)
                {
                    throw new InvalidOperationException("商品不存在或已被删除。");
                }
            }
        }

        public bool HasProductsInCategory(long categoryId)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = "SELECT COUNT(1) FROM products WHERE category_id = @category_id;";
                command.Parameters.AddWithValue("@category_id", categoryId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static bool HasBusinessRecords(SQLiteConnection connection, long productId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT
    (SELECT COUNT(1) FROM purchase_items WHERE product_id = @product_id) +
    (SELECT COUNT(1) FROM sales_items WHERE product_id = @product_id) +
    (SELECT COUNT(1) FROM inventory_check_items WHERE product_id = @product_id) +
    (SELECT COUNT(1) FROM scrap_records WHERE product_id = @product_id) +
    (SELECT COUNT(1) FROM stock_batches WHERE product_id = @product_id);";
                command.Parameters.AddWithValue("@product_id", productId);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void AddParameters(SQLiteCommand command, Product product)
        {
            command.Parameters.AddWithValue("@name", product.Name);
            command.Parameters.AddWithValue("@category_id", product.CategoryId);
            command.Parameters.AddWithValue("@barcode", EmptyToDbNull(product.Barcode));
            command.Parameters.AddWithValue("@specification", EmptyToDbNull(product.Specification));
            command.Parameters.AddWithValue("@default_price", product.DefaultPrice);
            command.Parameters.AddWithValue("@current_stock", product.CurrentStock);
            command.Parameters.AddWithValue("@average_cost", product.AverageCost);
            command.Parameters.AddWithValue("@min_stock_alert", product.MinStockAlert);
            command.Parameters.AddWithValue("@requires_expiry", product.RequiresExpiry ? 1 : 0);
            command.Parameters.AddWithValue("@expiry_date", product.ExpiryDate.HasValue ? (object)product.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
            command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(product.Status) ? "在售" : product.Status);
            command.Parameters.AddWithValue("@remark", EmptyToDbNull(product.Remark));
        }

        private static object EmptyToDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static Product ReadProduct(SQLiteDataReader reader)
        {
            return new Product
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                CategoryId = reader.GetInt64(2),
                CategoryName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Barcode = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Specification = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                DefaultPrice = Convert.ToDecimal(reader.GetValue(6)),
                CurrentStock = Convert.ToDecimal(reader.GetValue(7)),
                AverageCost = Convert.ToDecimal(reader.GetValue(8)),
                MinStockAlert = Convert.ToDecimal(reader.GetValue(9)),
                RequiresExpiry = reader.GetInt32(10) == 1,
                ExpiryDate = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11)),
                Status = reader.IsDBNull(12) ? "在售" : reader.GetString(12),
                Remark = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                CreatedAt = DateTime.Parse(reader.GetString(14)),
                UpdatedAt = reader.IsDBNull(15) ? (DateTime?)null : DateTime.Parse(reader.GetString(15))
            };
        }
    }
}
