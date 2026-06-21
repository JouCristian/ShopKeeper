using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class StockBatchRepository
    {
        private readonly string _connectionString;

        public StockBatchRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<StockBatch> GetByProductId(long productId)
        {
            List<StockBatch> batches = new List<StockBatch>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT id, product_id, batch_code, source_type, source_id, quantity_in, quantity_remaining,
       purchase_price, production_date, expiry_date, created_at, updated_at
FROM stock_batches
WHERE product_id = @product_id
ORDER BY id DESC;";
                command.Parameters.AddWithValue("@product_id", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        batches.Add(ReadBatch(reader));
                    }
                }
            }

            return batches;
        }

        internal static long Insert(SQLiteConnection connection, SQLiteTransaction transaction, StockBatch batch)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO stock_batches
    (product_id, purchase_item_id, batch_code, source_type, source_id, quantity_in, quantity_remaining,
     purchase_price, production_date, quantity, remaining_quantity, unit_cost, expiry_date, created_at)
VALUES
    (@product_id, @purchase_item_id, @batch_code, @source_type, @source_id, @quantity_in, @quantity_remaining,
     @purchase_price, @production_date, @quantity, @remaining_quantity, @unit_cost, @expiry_date, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@product_id", batch.ProductId);
                command.Parameters.AddWithValue("@purchase_item_id", batch.SourceId);
                command.Parameters.AddWithValue("@batch_code", batch.BatchCode);
                command.Parameters.AddWithValue("@source_type", batch.SourceType);
                command.Parameters.AddWithValue("@source_id", batch.SourceId);
                command.Parameters.AddWithValue("@quantity_in", batch.QuantityIn);
                command.Parameters.AddWithValue("@quantity_remaining", batch.QuantityRemaining);
                command.Parameters.AddWithValue("@purchase_price", batch.PurchasePrice);
                command.Parameters.AddWithValue("@production_date", batch.ProductionDate.HasValue ? (object)batch.ProductionDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                command.Parameters.AddWithValue("@quantity", batch.QuantityIn);
                command.Parameters.AddWithValue("@remaining_quantity", batch.QuantityRemaining);
                command.Parameters.AddWithValue("@unit_cost", batch.PurchasePrice);
                command.Parameters.AddWithValue("@expiry_date", batch.ExpiryDate.HasValue ? (object)batch.ExpiryDate.Value.ToString("yyyy-MM-dd") : DBNull.Value);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static StockBatch ReadBatch(SQLiteDataReader reader)
        {
            return new StockBatch
            {
                Id = reader.GetInt64(0),
                ProductId = reader.GetInt64(1),
                BatchCode = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                SourceType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                SourceId = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                QuantityIn = Convert.ToDecimal(reader.GetValue(5)),
                QuantityRemaining = Convert.ToDecimal(reader.GetValue(6)),
                PurchasePrice = Convert.ToDecimal(reader.GetValue(7)),
                ProductionDate = reader.IsDBNull(8) ? (DateTime?)null : DateTime.Parse(reader.GetString(8)),
                ExpiryDate = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9)),
                CreatedAt = DateTime.Parse(reader.GetString(10)),
                UpdatedAt = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11))
            };
        }
    }
}
