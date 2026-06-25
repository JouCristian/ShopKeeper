using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class ScrapRepository
    {
        private readonly string _connectionString;

        public ScrapRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<ScrapRecord> Search()
        {
            List<ScrapRecord> records = new List<ScrapRecord>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT id, scrap_no, scrap_date, product_id, product_name_snapshot, quantity,
       cost_price_snapshot, loss_amount, reason, remark, created_at, updated_at
FROM scrap_records
ORDER BY date(scrap_date) DESC, id DESC
LIMIT 200;";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(ReadRecord(reader));
                    }
                }
            }

            return records;
        }

        public long Save(ScrapRecord record)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ProductSnapshot product = GetProductSnapshot(connection, transaction, record.ProductId);
                        record.ScrapNo = GenerateScrapNo();
                        record.ProductNameSnapshot = product.Name;
                        record.CostPriceSnapshot = product.AverageCost;
                        record.LossAmount = record.Quantity * record.CostPriceSnapshot;

                        long recordId = InsertRecord(connection, transaction, record);
                        UpdateProductStock(connection, transaction, record.ProductId, product.CurrentStock - record.Quantity);
                        DeductStockBatches(connection, transaction, record.ProductId, record.Quantity);

                        transaction.Commit();
                        return recordId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void Delete(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        ScrapRecord record = GetById(connection, transaction, id);
                        if (record == null)
                        {
                            throw new InvalidOperationException("报废记录不存在或已被删除。");
                        }

                        IncreaseProductStock(connection, transaction, record.ProductId, record.Quantity);
                        InsertReversalBatch(connection, transaction, record);
                        DeleteRecord(connection, transaction, id);

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static long InsertRecord(SQLiteConnection connection, SQLiteTransaction transaction, ScrapRecord record)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO scrap_records
    (scrap_no, scrap_date, product_id, product_name_snapshot, quantity,
     cost_price_snapshot, loss_amount, reason, remark, created_at)
VALUES
    (@scrap_no, @scrap_date, @product_id, @product_name_snapshot, @quantity,
     @cost_price_snapshot, @loss_amount, @reason, @remark, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@scrap_no", record.ScrapNo);
                command.Parameters.AddWithValue("@scrap_date", record.ScrapDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@product_id", record.ProductId);
                command.Parameters.AddWithValue("@product_name_snapshot", record.ProductNameSnapshot);
                command.Parameters.AddWithValue("@quantity", record.Quantity);
                command.Parameters.AddWithValue("@cost_price_snapshot", record.CostPriceSnapshot);
                command.Parameters.AddWithValue("@loss_amount", record.LossAmount);
                command.Parameters.AddWithValue("@reason", record.Reason);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(record.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static ScrapRecord GetById(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT id, scrap_no, scrap_date, product_id, product_name_snapshot, quantity,
       cost_price_snapshot, loss_amount, reason, remark, created_at, updated_at
FROM scrap_records
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadRecord(reader) : null;
                }
            }
        }

        private static void IncreaseProductStock(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal quantity)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE products
SET current_stock = current_stock + @quantity,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);
                command.Parameters.AddWithValue("@quantity", quantity);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void InsertReversalBatch(SQLiteConnection connection, SQLiteTransaction transaction, ScrapRecord record)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO stock_batches
    (product_id, purchase_item_id, batch_code, source_type, source_id, quantity_in,
     quantity_remaining, purchase_price, quantity, remaining_quantity, unit_cost,
     expiry_date, created_at)
VALUES
    (@product_id, NULL, @batch_code, 'DeleteScrap', @source_id, @quantity,
     @quantity, @unit_cost, @quantity, @quantity, @unit_cost, NULL, @created_at);";
                command.Parameters.AddWithValue("@product_id", record.ProductId);
                command.Parameters.AddWithValue("@batch_code", "DEL-SCR-" + record.Id.ToString("000000"));
                command.Parameters.AddWithValue("@source_id", record.Id);
                command.Parameters.AddWithValue("@quantity", record.Quantity);
                command.Parameters.AddWithValue("@unit_cost", record.CostPriceSnapshot);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteRecord(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM scrap_records WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private static ProductSnapshot GetProductSnapshot(SQLiteConnection connection, SQLiteTransaction transaction, long productId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT name, current_stock, average_cost, status
FROM products
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("商品不存在，无法报废。");
                    }

                    string status = reader.IsDBNull(3) ? "在售" : reader.GetString(3);
                    if (status != "在售")
                    {
                        throw new InvalidOperationException("停用商品不能报废。");
                    }

                    return new ProductSnapshot
                    {
                        Name = reader.GetString(0),
                        CurrentStock = Convert.ToDecimal(reader.GetValue(1)),
                        AverageCost = Convert.ToDecimal(reader.GetValue(2))
                    };
                }
            }
        }

        private static void UpdateProductStock(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal currentStock)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE products
SET current_stock = @current_stock,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);
                command.Parameters.AddWithValue("@current_stock", currentStock);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void DeductStockBatches(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal quantity)
        {
            decimal remaining = quantity;
            foreach (BatchQuantity batch in GetDeductibleBatches(connection, transaction, productId))
            {
                if (remaining <= 0)
                {
                    break;
                }

                decimal deduct = batch.QuantityRemaining >= remaining ? remaining : batch.QuantityRemaining;
                UpdateBatchRemaining(connection, transaction, batch.Id, batch.QuantityRemaining - deduct);
                remaining -= deduct;
            }
        }

        private static IList<BatchQuantity> GetDeductibleBatches(SQLiteConnection connection, SQLiteTransaction transaction, long productId)
        {
            List<BatchQuantity> batches = new List<BatchQuantity>();

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT id, quantity_remaining
FROM stock_batches
WHERE product_id = @product_id
  AND quantity_remaining > 0
ORDER BY CASE WHEN expiry_date IS NULL OR expiry_date = '' THEN 1 ELSE 0 END ASC,
         date(expiry_date) ASC,
         id ASC;";
                command.Parameters.AddWithValue("@product_id", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        batches.Add(new BatchQuantity
                        {
                            Id = reader.GetInt64(0),
                            QuantityRemaining = Convert.ToDecimal(reader.GetValue(1))
                        });
                    }
                }
            }

            return batches;
        }

        private static void UpdateBatchRemaining(SQLiteConnection connection, SQLiteTransaction transaction, long batchId, decimal quantityRemaining)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE stock_batches
SET quantity_remaining = @quantity_remaining,
    remaining_quantity = @quantity_remaining,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", batchId);
                command.Parameters.AddWithValue("@quantity_remaining", quantityRemaining);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static ScrapRecord ReadRecord(SQLiteDataReader reader)
        {
            return new ScrapRecord
            {
                Id = reader.GetInt64(0),
                ScrapNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                ScrapDate = ParseDateTime(reader, 2),
                ProductId = reader.GetInt64(3),
                ProductNameSnapshot = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Quantity = Convert.ToDecimal(reader.GetValue(5)),
                CostPriceSnapshot = Convert.ToDecimal(reader.GetValue(6)),
                LossAmount = Convert.ToDecimal(reader.GetValue(7)),
                Reason = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Remark = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                CreatedAt = ParseDateTime(reader, 10),
                UpdatedAt = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11))
            };
        }

        private static DateTime ParseDateTime(SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return DateTime.Now;
            }

            DateTime result;
            return DateTime.TryParse(reader.GetString(index), out result) ? result : DateTime.Now;
        }

        private static string GenerateScrapNo()
        {
            return "SCR-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        private static object EmptyToDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private sealed class ProductSnapshot
        {
            public string Name { get; set; }

            public decimal CurrentStock { get; set; }

            public decimal AverageCost { get; set; }
        }

        private sealed class BatchQuantity
        {
            public long Id { get; set; }

            public decimal QuantityRemaining { get; set; }
        }
    }
}
