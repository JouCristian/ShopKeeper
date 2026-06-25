using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class InventoryCheckRepository
    {
        private readonly string _connectionString;

        public InventoryCheckRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<InventoryCheck> Search(string keyword, long? categoryId)
        {
            List<InventoryCheck> records = new List<InventoryCheck>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT c.id, c.check_no, c.check_date, c.total_profit_quantity, c.total_loss_quantity,
       c.total_profit_amount, c.total_loss_amount, c.remark, c.created_at, c.updated_at,
       COUNT(i.id) AS item_count
FROM inventory_checks c
LEFT JOIN inventory_check_items i ON i.inventory_check_id = c.id
WHERE (
    @keyword = ''
    OR EXISTS (
        SELECT 1 FROM inventory_check_items si
        WHERE si.inventory_check_id = c.id
          AND si.product_name_snapshot LIKE @keyword_like
    )
)
AND (
    @category_id IS NULL
    OR EXISTS (
        SELECT 1
        FROM inventory_check_items ci
        INNER JOIN products p ON p.id = ci.product_id
        WHERE ci.inventory_check_id = c.id
          AND p.category_id = @category_id
    )
)
GROUP BY c.id, c.check_no, c.check_date, c.total_profit_quantity, c.total_loss_quantity,
         c.total_profit_amount, c.total_loss_amount, c.remark, c.created_at, c.updated_at
ORDER BY date(c.check_date) DESC, c.id DESC;";
                command.Parameters.AddWithValue("@keyword", keyword ?? string.Empty);
                command.Parameters.AddWithValue("@keyword_like", "%" + (keyword ?? string.Empty) + "%");
                command.Parameters.AddWithValue("@category_id", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(ReadSummary(reader));
                    }
                }
            }

            return records;
        }

        public InventoryCheck GetById(long id)
        {
            InventoryCheck record = null;

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT id, check_no, check_date, total_profit_quantity, total_loss_quantity,
       total_profit_amount, total_loss_amount, remark, created_at, updated_at
FROM inventory_checks
WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = new InventoryCheck
                            {
                                Id = reader.GetInt64(0),
                                CheckNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                CheckDate = ParseDateTime(reader, 2),
                                TotalProfitQuantity = Convert.ToDecimal(reader.GetValue(3)),
                                TotalLossQuantity = Convert.ToDecimal(reader.GetValue(4)),
                                TotalProfitAmount = Convert.ToDecimal(reader.GetValue(5)),
                                TotalLossAmount = Convert.ToDecimal(reader.GetValue(6)),
                                Remark = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                CreatedAt = ParseDateTime(reader, 8),
                                UpdatedAt = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9))
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
SELECT i.id, i.inventory_check_id, i.product_id, i.product_name_snapshot,
       IFNULL(cat.name, '') AS category_name, i.system_stock, i.actual_stock,
       i.difference_quantity, i.cost_price_snapshot, i.difference_amount,
       i.reason, i.remark, i.created_at, i.updated_at
FROM inventory_check_items i
LEFT JOIN products p ON p.id = i.product_id
LEFT JOIN categories cat ON cat.id = p.category_id
WHERE i.inventory_check_id = @inventory_check_id
ORDER BY i.id ASC;";
                    command.Parameters.AddWithValue("@inventory_check_id", id);

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
            return record;
        }

        public long Save(InventoryCheck record)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        record.CheckNo = GenerateCheckNo();
                        PrepareItems(connection, transaction, record);
                        long recordId = InsertRecord(connection, transaction, record);

                        foreach (InventoryCheckItem item in record.Items)
                        {
                            item.InventoryCheckId = recordId;
                            long itemId = InsertItem(connection, transaction, item);
                            UpdateProductStock(connection, transaction, item.ProductId, item.ActualStock);

                            if (item.DifferenceQuantity < 0)
                            {
                                DeductStockBatches(connection, transaction, item.ProductId, Math.Abs(item.DifferenceQuantity));
                            }
                            else if (item.DifferenceQuantity > 0)
                            {
                                InsertAdjustmentBatch(connection, transaction, item, itemId);
                            }
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

        public void Delete(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        IList<InventoryCheckItem> items = GetItems(connection, transaction, id);
                        if (items.Count == 0 && !Exists(connection, transaction, id))
                        {
                            throw new InvalidOperationException("盘点单不存在或已被删除。");
                        }

                        foreach (InventoryCheckItem item in items)
                        {
                            if (item.DifferenceQuantity > 0)
                            {
                                EnsureAdjustmentBatchUnused(connection, transaction, item.Id);
                                EnsureEnoughStockToReverse(connection, transaction, item.ProductId, item.DifferenceQuantity);
                            }
                        }

                        foreach (InventoryCheckItem item in items)
                        {
                            if (item.DifferenceQuantity > 0)
                            {
                                DeleteAdjustmentBatch(connection, transaction, item.Id);
                            }
                            else if (item.DifferenceQuantity < 0)
                            {
                                InsertReversalBatch(connection, transaction, item);
                            }

                            ReverseProductStock(connection, transaction, item.ProductId, item.DifferenceQuantity);
                        }

                        DeleteItems(connection, transaction, id);
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

        private static void PrepareItems(SQLiteConnection connection, SQLiteTransaction transaction, InventoryCheck record)
        {
            record.TotalProfitQuantity = 0;
            record.TotalLossQuantity = 0;
            record.TotalProfitAmount = 0;
            record.TotalLossAmount = 0;

            foreach (InventoryCheckItem item in record.Items)
            {
                ProductSnapshot product = GetProductSnapshot(connection, transaction, item.ProductId);
                item.ProductNameSnapshot = product.Name;
                item.SystemStock = product.CurrentStock;
                item.CostPriceSnapshot = product.AverageCost;
                item.DifferenceQuantity = item.ActualStock - item.SystemStock;
                item.DifferenceAmount = item.DifferenceQuantity * item.CostPriceSnapshot;

                if (item.DifferenceQuantity > 0)
                {
                    record.TotalProfitQuantity += item.DifferenceQuantity;
                    record.TotalProfitAmount += item.DifferenceAmount;
                }
                else if (item.DifferenceQuantity < 0)
                {
                    record.TotalLossQuantity += Math.Abs(item.DifferenceQuantity);
                    record.TotalLossAmount += Math.Abs(item.DifferenceAmount);
                }
            }
        }

        private static bool Exists(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(1) FROM inventory_checks WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static IList<InventoryCheckItem> GetItems(SQLiteConnection connection, SQLiteTransaction transaction, long inventoryCheckId)
        {
            List<InventoryCheckItem> items = new List<InventoryCheckItem>();

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT i.id, i.inventory_check_id, i.product_id, i.product_name_snapshot,
       IFNULL(cat.name, '') AS category_name, i.system_stock, i.actual_stock,
       i.difference_quantity, i.cost_price_snapshot, i.difference_amount,
       i.reason, i.remark, i.created_at, i.updated_at
FROM inventory_check_items i
LEFT JOIN products p ON p.id = i.product_id
LEFT JOIN categories cat ON cat.id = p.category_id
WHERE i.inventory_check_id = @inventory_check_id
ORDER BY i.id ASC;";
                command.Parameters.AddWithValue("@inventory_check_id", inventoryCheckId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(ReadItem(reader));
                    }
                }
            }

            return items;
        }

        private static void EnsureAdjustmentBatchUnused(SQLiteConnection connection, SQLiteTransaction transaction, long inventoryCheckItemId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT COUNT(1)
FROM stock_batches
WHERE source_type = 'InventoryCheck'
  AND source_id = @source_id
  AND quantity_remaining < quantity_in;";
                command.Parameters.AddWithValue("@source_id", inventoryCheckItemId);

                if (Convert.ToInt32(command.ExecuteScalar()) > 0)
                {
                    throw new InvalidOperationException("该盘点单的盘盈库存已经被后续业务消耗，不能直接删除。");
                }
            }
        }

        private static void EnsureEnoughStockToReverse(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal quantity)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT current_stock FROM products WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);

                object value = command.ExecuteScalar();
                if (value == null || value == DBNull.Value)
                {
                    throw new InvalidOperationException("盘点单关联的商品不存在，不能删除。");
                }

                if (Convert.ToDecimal(value) < quantity)
                {
                    throw new InvalidOperationException("当前库存少于本次盘盈数量，不能删除该盘点单。");
                }
            }
        }

        private static void DeleteAdjustmentBatch(SQLiteConnection connection, SQLiteTransaction transaction, long inventoryCheckItemId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM stock_batches WHERE source_type = 'InventoryCheck' AND source_id = @source_id;";
                command.Parameters.AddWithValue("@source_id", inventoryCheckItemId);
                command.ExecuteNonQuery();
            }
        }

        private static void InsertReversalBatch(SQLiteConnection connection, SQLiteTransaction transaction, InventoryCheckItem item)
        {
            decimal quantity = Math.Abs(item.DifferenceQuantity);

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO stock_batches
    (product_id, purchase_item_id, batch_code, source_type, source_id, quantity_in,
     quantity_remaining, purchase_price, quantity, remaining_quantity, unit_cost,
     expiry_date, created_at)
VALUES
    (@product_id, NULL, @batch_code, 'DeleteInventoryCheck', @source_id, @quantity,
     @quantity, @unit_cost, @quantity, @quantity, @unit_cost, NULL, @created_at);";
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@batch_code", "DEL-CHK-" + item.Id.ToString("000000"));
                command.Parameters.AddWithValue("@source_id", item.Id);
                command.Parameters.AddWithValue("@quantity", quantity);
                command.Parameters.AddWithValue("@unit_cost", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void ReverseProductStock(SQLiteConnection connection, SQLiteTransaction transaction, long productId, decimal differenceQuantity)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE products
SET current_stock = current_stock - @difference_quantity,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", productId);
                command.Parameters.AddWithValue("@difference_quantity", differenceQuantity);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteItems(SQLiteConnection connection, SQLiteTransaction transaction, long inventoryCheckId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM inventory_check_items WHERE inventory_check_id = @inventory_check_id;";
                command.Parameters.AddWithValue("@inventory_check_id", inventoryCheckId);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteRecord(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM inventory_checks WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private static long InsertRecord(SQLiteConnection connection, SQLiteTransaction transaction, InventoryCheck record)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO inventory_checks
    (check_no, check_date, total_profit_quantity, total_loss_quantity,
     total_profit_amount, total_loss_amount, remark, created_at)
VALUES
    (@check_no, @check_date, @total_profit_quantity, @total_loss_quantity,
     @total_profit_amount, @total_loss_amount, @remark, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@check_no", record.CheckNo);
                command.Parameters.AddWithValue("@check_date", record.CheckDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@total_profit_quantity", record.TotalProfitQuantity);
                command.Parameters.AddWithValue("@total_loss_quantity", record.TotalLossQuantity);
                command.Parameters.AddWithValue("@total_profit_amount", record.TotalProfitAmount);
                command.Parameters.AddWithValue("@total_loss_amount", record.TotalLossAmount);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(record.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static long InsertItem(SQLiteConnection connection, SQLiteTransaction transaction, InventoryCheckItem item)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO inventory_check_items
    (inventory_check_id, product_id, product_name_snapshot, system_stock, actual_stock,
     difference_quantity, cost_price_snapshot, difference_amount, reason, remark, created_at)
VALUES
    (@inventory_check_id, @product_id, @product_name_snapshot, @system_stock, @actual_stock,
     @difference_quantity, @cost_price_snapshot, @difference_amount, @reason, @remark, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@inventory_check_id", item.InventoryCheckId);
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@product_name_snapshot", item.ProductNameSnapshot);
                command.Parameters.AddWithValue("@system_stock", item.SystemStock);
                command.Parameters.AddWithValue("@actual_stock", item.ActualStock);
                command.Parameters.AddWithValue("@difference_quantity", item.DifferenceQuantity);
                command.Parameters.AddWithValue("@cost_price_snapshot", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@difference_amount", item.DifferenceAmount);
                command.Parameters.AddWithValue("@reason", EmptyToDbNull(item.Reason));
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(item.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static void InsertAdjustmentBatch(SQLiteConnection connection, SQLiteTransaction transaction, InventoryCheckItem item, long itemId)
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
    (@product_id, NULL, @batch_code, 'InventoryCheck', @source_id, @quantity_in,
     @quantity_remaining, @purchase_price, @quantity, @remaining_quantity, @unit_cost,
     NULL, @created_at);";
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@batch_code", GenerateBatchCode(itemId));
                command.Parameters.AddWithValue("@source_id", itemId);
                command.Parameters.AddWithValue("@quantity_in", item.DifferenceQuantity);
                command.Parameters.AddWithValue("@quantity_remaining", item.DifferenceQuantity);
                command.Parameters.AddWithValue("@purchase_price", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@quantity", item.DifferenceQuantity);
                command.Parameters.AddWithValue("@remaining_quantity", item.DifferenceQuantity);
                command.Parameters.AddWithValue("@unit_cost", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static ProductSnapshot GetProductSnapshot(SQLiteConnection connection, SQLiteTransaction transaction, long productId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT p.name, p.current_stock, p.average_cost, p.status, IFNULL(c.name, '') AS category_name
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
WHERE p.id = @id;";
                command.Parameters.AddWithValue("@id", productId);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        throw new InvalidOperationException("商品不存在，无法盘点。");
                    }

                    string status = reader.IsDBNull(3) ? "在售" : reader.GetString(3);
                    if (status != "在售")
                    {
                        throw new InvalidOperationException("停用商品不能盘点。");
                    }

                    return new ProductSnapshot
                    {
                        Name = reader.GetString(0),
                        CurrentStock = Convert.ToDecimal(reader.GetValue(1)),
                        AverageCost = Convert.ToDecimal(reader.GetValue(2)),
                        CategoryName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
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

        private static InventoryCheck ReadSummary(SQLiteDataReader reader)
        {
            return new InventoryCheck
            {
                Id = reader.GetInt64(0),
                CheckNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                CheckDate = ParseDateTime(reader, 2),
                TotalProfitQuantity = Convert.ToDecimal(reader.GetValue(3)),
                TotalLossQuantity = Convert.ToDecimal(reader.GetValue(4)),
                TotalProfitAmount = Convert.ToDecimal(reader.GetValue(5)),
                TotalLossAmount = Convert.ToDecimal(reader.GetValue(6)),
                Remark = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                CreatedAt = ParseDateTime(reader, 8),
                UpdatedAt = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9)),
                ProductKindCount = Convert.ToInt32(reader.GetValue(10))
            };
        }

        private static InventoryCheckItem ReadItem(SQLiteDataReader reader)
        {
            return new InventoryCheckItem
            {
                Id = reader.GetInt64(0),
                InventoryCheckId = reader.GetInt64(1),
                ProductId = reader.GetInt64(2),
                ProductNameSnapshot = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                CategoryName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                SystemStock = Convert.ToDecimal(reader.GetValue(5)),
                ActualStock = Convert.ToDecimal(reader.GetValue(6)),
                DifferenceQuantity = Convert.ToDecimal(reader.GetValue(7)),
                CostPriceSnapshot = Convert.ToDecimal(reader.GetValue(8)),
                DifferenceAmount = Convert.ToDecimal(reader.GetValue(9)),
                Reason = reader.IsDBNull(10) ? string.Empty : reader.GetString(10),
                Remark = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                CreatedAt = ParseDateTime(reader, 12),
                UpdatedAt = reader.IsDBNull(13) ? (DateTime?)null : DateTime.Parse(reader.GetString(13))
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

        private static string GenerateCheckNo()
        {
            return "CHK-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        private static string GenerateBatchCode(long itemId)
        {
            return "CHK-BATCH-" + DateTime.Now.ToString("yyyyMMdd") + "-" + itemId.ToString("0000");
        }

        private static object EmptyToDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private sealed class ProductSnapshot
        {
            public string Name { get; set; }

            public string CategoryName { get; set; }

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
