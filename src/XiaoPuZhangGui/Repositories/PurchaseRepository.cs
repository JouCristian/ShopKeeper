using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class PurchaseRepository
    {
        private readonly string _connectionString;

        public PurchaseRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<PurchaseRecord> Search(DateTime startDate, DateTime endDate, string productKeyword)
        {
            List<PurchaseRecord> records = new List<PurchaseRecord>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT r.id, r.purchase_no, r.purchase_date, r.total_amount, r.remark, r.created_at, r.updated_at,
       COUNT(i.id) AS item_count,
       IFNULL(SUM(i.quantity), 0) AS total_quantity
FROM purchase_records r
LEFT JOIN purchase_items i ON i.purchase_record_id = r.id
WHERE date(r.purchase_date) >= date(@start_date)
  AND date(r.purchase_date) <= date(@end_date)
  AND (
      @keyword = ''
      OR EXISTS (
          SELECT 1
          FROM purchase_items pi
          WHERE pi.purchase_record_id = r.id
            AND pi.product_name_snapshot LIKE @keyword_like
      )
  )
GROUP BY r.id, r.purchase_no, r.purchase_date, r.total_amount, r.remark, r.created_at, r.updated_at
ORDER BY r.purchase_date DESC, r.id DESC;";
                command.Parameters.AddWithValue("@start_date", startDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@end_date", endDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@keyword", productKeyword ?? string.Empty);
                command.Parameters.AddWithValue("@keyword_like", "%" + (productKeyword ?? string.Empty) + "%");

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(ReadRecordSummary(reader));
                    }
                }
            }

            return records;
        }

        public PurchaseRecord GetById(long id)
        {
            PurchaseRecord record = null;

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT id, purchase_no, purchase_date, total_amount, remark, created_at, updated_at
FROM purchase_records
WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new PurchaseRecord
                            {
                                Id = reader.GetInt64(0),
                                PurchaseNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                PurchaseDate = DateTime.Parse(reader.GetString(2)),
                                TotalAmount = Convert.ToDecimal(reader.GetValue(3)),
                                Remark = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                CreatedAt = DateTime.Parse(reader.GetString(5)),
                                UpdatedAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6))
                            };
                        }
                    }
                }

                if (record == null)
                {
                    return null;
                }

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT id, purchase_record_id, product_id, product_name_snapshot, quantity, purchase_price,
       line_total, production_date, expiry_date, remark, created_at, updated_at
FROM purchase_items
WHERE purchase_record_id = @purchase_record_id
ORDER BY id ASC;";
                    command.Parameters.AddWithValue("@purchase_record_id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            record.Items.Add(ReadItem(reader));
                        }
                    }
                }
            }

            record.ProductKindCount = record.Items.Count;
            foreach (PurchaseItem item in record.Items)
            {
                record.TotalQuantity += item.Quantity;
            }

            return record;
        }

        public long Save(PurchaseRecord record)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        record.PurchaseNo = GeneratePurchaseNo();
                        record.TotalAmount = CalculateTotal(record);
                        long recordId = InsertRecord(connection, transaction, record);

                        foreach (PurchaseItem item in record.Items)
                        {
                            ProductStockSnapshot stock = GetProductStock(connection, transaction, item.ProductId);
                            item.PurchaseRecordId = recordId;
                            item.ProductNameSnapshot = stock.ProductName;
                            item.LineTotal = item.Quantity * item.PurchasePrice;

                            long itemId = InsertItem(connection, transaction, item);
                            StockBatch batch = new StockBatch
                            {
                                ProductId = item.ProductId,
                                BatchCode = GenerateBatchCode(itemId),
                                SourceType = "Purchase",
                                SourceId = itemId,
                                QuantityIn = item.Quantity,
                                QuantityRemaining = item.Quantity,
                                PurchasePrice = item.PurchasePrice,
                                ProductionDate = item.ProductionDate,
                                ExpiryDate = item.ExpiryDate
                            };
                            StockBatchRepository.Insert(connection, transaction, batch);

                            decimal newStock = stock.CurrentStock + item.Quantity;
                            decimal newAverageCost = stock.CurrentStock <= 0
                                ? item.PurchasePrice
                                : ((stock.CurrentStock * stock.AverageCost) + (item.Quantity * item.PurchasePrice)) / newStock;

                            UpdateProductStock(connection, transaction, item.ProductId, newStock, newAverageCost);
                        }

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

        private static long InsertRecord(SQLiteConnection connection, SQLiteTransaction transaction, PurchaseRecord record)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO purchase_records
    (purchase_no, purchase_date, purchased_at, total_amount, remark, created_at)
VALUES
    (@purchase_no, @purchase_date, @purchased_at, @total_amount, @remark, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@purchase_no", record.PurchaseNo);
                command.Parameters.AddWithValue("@purchase_date", record.PurchaseDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@purchased_at", record.PurchaseDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@total_amount", record.TotalAmount);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(record.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static long InsertItem(SQLiteConnection connection, SQLiteTransaction transaction, PurchaseItem item)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO purchase_items
    (purchase_record_id, product_id, product_name_snapshot, quantity, purchase_price, line_total,
     production_date, unit_cost, expiry_date, remark, created_at)
VALUES
    (@purchase_record_id, @product_id, @product_name_snapshot, @quantity, @purchase_price, @line_total,
     @production_date, @unit_cost, @expiry_date, @remark, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@purchase_record_id", item.PurchaseRecordId);
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@product_name_snapshot", item.ProductNameSnapshot);
                command.Parameters.AddWithValue("@quantity", item.Quantity);
                command.Parameters.AddWithValue("@purchase_price", item.PurchasePrice);
                command.Parameters.AddWithValue("@line_total", item.LineTotal);
                command.Parameters.AddWithValue("@production_date", item.ProductionDate.HasValue ? (object)item.ProductionDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                command.Parameters.AddWithValue("@unit_cost", item.PurchasePrice);
                command.Parameters.AddWithValue("@expiry_date", item.ExpiryDate.HasValue ? (object)item.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(item.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static ProductStockSnapshot GetProductStock(SQLiteConnection connection, SQLiteTransaction transaction, long productId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT name, current_stock, average_cost, requires_expiry, status
FROM products
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("商品不存在，无法入库。");
                    }

                    string status = reader.IsDBNull(4) ? "在售" : reader.GetString(4);
                    if (status != "在售")
                    {
                        throw new InvalidOperationException("停用商品不能入库。");
                    }

                    return new ProductStockSnapshot
                    {
                        ProductName = reader.GetString(0),
                        CurrentStock = Convert.ToDecimal(reader.GetValue(1)),
                        AverageCost = Convert.ToDecimal(reader.GetValue(2)),
                        RequiresExpiry = reader.GetInt32(3) == 1
                    };
                }
            }
        }

        private static void UpdateProductStock(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal currentStock, decimal averageCost)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE products
SET current_stock = @current_stock,
    average_cost = @average_cost,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);
                command.Parameters.AddWithValue("@current_stock", currentStock);
                command.Parameters.AddWithValue("@average_cost", averageCost);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static PurchaseRecord ReadRecordSummary(SQLiteDataReader reader)
        {
            return new PurchaseRecord
            {
                Id = reader.GetInt64(0),
                PurchaseNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                PurchaseDate = DateTime.Parse(reader.GetString(2)),
                TotalAmount = Convert.ToDecimal(reader.GetValue(3)),
                Remark = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                UpdatedAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6)),
                ProductKindCount = Convert.ToInt32(reader.GetValue(7)),
                TotalQuantity = Convert.ToDecimal(reader.GetValue(8))
            };
        }

        private static PurchaseItem ReadItem(SQLiteDataReader reader)
        {
            return new PurchaseItem
            {
                Id = reader.GetInt64(0),
                PurchaseRecordId = reader.GetInt64(1),
                ProductId = reader.GetInt64(2),
                ProductNameSnapshot = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Quantity = Convert.ToDecimal(reader.GetValue(4)),
                PurchasePrice = Convert.ToDecimal(reader.GetValue(5)),
                LineTotal = Convert.ToDecimal(reader.GetValue(6)),
                ProductionDate = reader.IsDBNull(7) ? (DateTime?)null : DateTime.Parse(reader.GetString(7)),
                ExpiryDate = reader.IsDBNull(8) ? (DateTime?)null : DateTime.Parse(reader.GetString(8)),
                Remark = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                UpdatedAt = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11))
            };
        }

        private static decimal CalculateTotal(PurchaseRecord record)
        {
            decimal total = 0;
            foreach (PurchaseItem item in record.Items)
            {
                total += item.Quantity * item.PurchasePrice;
            }

            return total;
        }

        private static string GeneratePurchaseNo()
        {
            return "PUR-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        private static string GenerateBatchCode(long purchaseItemId)
        {
            return "BATCH-" + DateTime.Now.ToString("yyyyMMdd") + "-" + purchaseItemId.ToString("0000");
        }

        private static object EmptyToDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private sealed class ProductStockSnapshot
        {
            public string ProductName { get; set; }

            public decimal CurrentStock { get; set; }

            public decimal AverageCost { get; set; }

            public bool RequiresExpiry { get; set; }
        }
    }
}
