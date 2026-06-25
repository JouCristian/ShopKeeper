using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class SalesRepository
    {
        private readonly string _connectionString;

        public SalesRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<SalesOrder> Search(DateTime startTime, DateTime endTime)
        {
            List<SalesOrder> orders = new List<SalesOrder>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT o.id, o.order_no, IFNULL(o.sale_time, o.sold_at) AS sale_time,
       o.total_amount, o.total_cost, o.gross_profit, o.paid_amount,
       o.remark, o.created_at, o.updated_at,
       COUNT(i.id) AS item_count,
       IFNULL(SUM(i.quantity), 0) AS total_quantity
FROM sales_orders o
LEFT JOIN sales_items i ON i.sales_order_id = o.id
WHERE datetime(IFNULL(o.sale_time, o.sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(o.sale_time, o.sold_at)) <= datetime(@end_time)
GROUP BY o.id, o.order_no, o.sale_time, o.sold_at, o.total_amount, o.total_cost,
         o.gross_profit, o.paid_amount, o.remark, o.created_at, o.updated_at
ORDER BY datetime(IFNULL(o.sale_time, o.sold_at)) DESC, o.id DESC;";
                command.Parameters.AddWithValue("@start_time", startTime.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@end_time", endTime.ToString("yyyy-MM-dd HH:mm:ss"));

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        orders.Add(ReadOrderSummary(reader));
                    }
                }
            }

            return orders;
        }

        public SalesOrder GetById(long id)
        {
            SalesOrder order = null;

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT id, order_no, IFNULL(sale_time, sold_at) AS sale_time, total_amount, total_cost,
       gross_profit, paid_amount, remark, created_at, updated_at
FROM sales_orders
WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            order = new SalesOrder
                            {
                                Id = reader.GetInt64(0),
                                OrderNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                SaleTime = ParseDateTime(reader, 2),
                                TotalAmount = Convert.ToDecimal(reader.GetValue(3)),
                                TotalCost = Convert.ToDecimal(reader.GetValue(4)),
                                GrossProfit = Convert.ToDecimal(reader.GetValue(5)),
                                PaidAmount = Convert.ToDecimal(reader.GetValue(6)),
                                Remark = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                                CreatedAt = ParseDateTime(reader, 8),
                                UpdatedAt = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9))
                            };
                        }
                    }
                }

                if (order == null)
                {
                    return null;
                }

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT id, sales_order_id, product_id, product_name_snapshot, quantity,
       sale_price_snapshot, cost_price_snapshot, line_amount, line_cost,
       line_profit, created_at, updated_at
FROM sales_items
WHERE sales_order_id = @sales_order_id
ORDER BY id ASC;";
                    command.Parameters.AddWithValue("@sales_order_id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            order.Items.Add(ReadItem(reader));
                        }
                    }
                }
            }

            order.ProductKindCount = order.Items.Count;
            foreach (SalesItem item in order.Items)
            {
                order.TotalQuantity += item.Quantity;
            }

            return order;
        }

        public long Save(SalesOrder order)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (SalesItem item in order.Items)
                        {
                            ProductSnapshot product = GetProductSnapshot(connection, transaction, item.ProductId);
                            item.ProductNameSnapshot = product.Name;
                            item.CostPriceSnapshot = product.AverageCost;
                        }

                        order.OrderNo = GenerateOrderNo();
                        CalculateTotals(order);
                        long orderId = InsertOrder(connection, transaction, order);

                        if (order.CreditAmount > 0)
                        {
                            CreditRepository.InsertInitialCredit(
                                connection,
                                transaction,
                                orderId,
                                order.DebtorName,
                                order.CreditAmount,
                                order.Remark);
                        }

                        foreach (SalesItem item in order.Items)
                        {
                            ProductSnapshot product = GetProductSnapshot(connection, transaction, item.ProductId);
                            item.SalesOrderId = orderId;
                            item.ProductNameSnapshot = product.Name;
                            item.CostPriceSnapshot = product.AverageCost;
                            item.LineAmount = item.Quantity * item.SalePriceSnapshot;
                            item.LineCost = item.Quantity * item.CostPriceSnapshot;
                            item.LineProfit = item.LineAmount - item.LineCost;

                            InsertItem(connection, transaction, item);
                            UpdateProductStock(connection, transaction, item.ProductId, product.CurrentStock - item.Quantity);
                            DeductStockBatches(connection, transaction, item.ProductId, item.Quantity);
                        }

                        transaction.Commit();
                        return orderId;
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
                        IList<SalesItem> items = GetItems(connection, transaction, id);
                        if (items.Count == 0 && !Exists(connection, transaction, id))
                        {
                            throw new InvalidOperationException("销售单不存在或已被删除。");
                        }

                        foreach (SalesItem item in items)
                        {
                            if (item.ProductId <= 0)
                            {
                                continue;
                            }

                            IncreaseProductStock(connection, transaction, item.ProductId, item.Quantity);
                            InsertReversalBatch(connection, transaction, item);
                        }

                        DeleteCreditBySalesOrder(connection, transaction, id);
                        DeleteItems(connection, transaction, id);
                        DeleteOrder(connection, transaction, id);

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

        private static long InsertOrder(SQLiteConnection connection, SQLiteTransaction transaction, SalesOrder order)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO sales_orders
    (order_no, sale_time, sold_at, total_amount, total_cost, receivable_amount,
     cost_amount, gross_profit, paid_amount, credit_amount, remark, created_at)
VALUES
    (@order_no, @sale_time, @sold_at, @total_amount, @total_cost, @receivable_amount,
     @cost_amount, @gross_profit, @paid_amount, @credit_amount, @remark, @created_at);
SELECT last_insert_rowid();";
                string saleTime = order.SaleTime.ToString("yyyy-MM-dd HH:mm:ss");
                command.Parameters.AddWithValue("@order_no", order.OrderNo);
                command.Parameters.AddWithValue("@sale_time", saleTime);
                command.Parameters.AddWithValue("@sold_at", saleTime);
                command.Parameters.AddWithValue("@total_amount", order.TotalAmount);
                command.Parameters.AddWithValue("@total_cost", order.TotalCost);
                command.Parameters.AddWithValue("@receivable_amount", order.TotalAmount);
                command.Parameters.AddWithValue("@cost_amount", order.TotalCost);
                command.Parameters.AddWithValue("@gross_profit", order.GrossProfit);
                command.Parameters.AddWithValue("@paid_amount", order.PaidAmount);
                command.Parameters.AddWithValue("@credit_amount", order.CreditAmount);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(order.Remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static long InsertItem(SQLiteConnection connection, SQLiteTransaction transaction, SalesItem item)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO sales_items
    (sales_order_id, product_id, product_name_snapshot, quantity, sale_price_snapshot,
     cost_price_snapshot, line_amount, line_cost, line_profit, profit_snapshot, created_at)
VALUES
    (@sales_order_id, @product_id, @product_name_snapshot, @quantity, @sale_price_snapshot,
     @cost_price_snapshot, @line_amount, @line_cost, @line_profit, @profit_snapshot, @created_at);
SELECT last_insert_rowid();";
                command.Parameters.AddWithValue("@sales_order_id", item.SalesOrderId);
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@product_name_snapshot", item.ProductNameSnapshot);
                command.Parameters.AddWithValue("@quantity", item.Quantity);
                command.Parameters.AddWithValue("@sale_price_snapshot", item.SalePriceSnapshot);
                command.Parameters.AddWithValue("@cost_price_snapshot", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@line_amount", item.LineAmount);
                command.Parameters.AddWithValue("@line_cost", item.LineCost);
                command.Parameters.AddWithValue("@line_profit", item.LineProfit);
                command.Parameters.AddWithValue("@profit_snapshot", item.LineProfit);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                return (long)command.ExecuteScalar();
            }
        }

        private static bool Exists(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT COUNT(1) FROM sales_orders WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static IList<SalesItem> GetItems(SQLiteConnection connection, SQLiteTransaction transaction, long salesOrderId)
        {
            List<SalesItem> items = new List<SalesItem>();

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT id, sales_order_id, product_id, product_name_snapshot, quantity,
       sale_price_snapshot, cost_price_snapshot, line_amount, line_cost,
       line_profit, created_at, updated_at
FROM sales_items
WHERE sales_order_id = @sales_order_id
ORDER BY id ASC;";
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);

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

        private static void InsertReversalBatch(SQLiteConnection connection, SQLiteTransaction transaction, SalesItem item)
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
    (@product_id, NULL, @batch_code, 'DeleteSales', @source_id, @quantity,
     @quantity, @unit_cost, @quantity, @quantity, @unit_cost, NULL, @created_at);";
                command.Parameters.AddWithValue("@product_id", item.ProductId);
                command.Parameters.AddWithValue("@batch_code", "DEL-SAL-" + item.Id.ToString("000000"));
                command.Parameters.AddWithValue("@source_id", item.Id);
                command.Parameters.AddWithValue("@quantity", item.Quantity);
                command.Parameters.AddWithValue("@unit_cost", item.CostPriceSnapshot);
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteCreditBySalesOrder(SQLiteConnection connection, SQLiteTransaction transaction, long salesOrderId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
DELETE FROM credit_payments
WHERE credit_record_id IN (SELECT id FROM credit_records WHERE sales_order_id = @sales_order_id);";
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);
                command.ExecuteNonQuery();
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
DELETE FROM repayment_records
WHERE credit_record_id IN (SELECT id FROM credit_records WHERE sales_order_id = @sales_order_id);";
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);
                command.ExecuteNonQuery();
            }

            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM credit_records WHERE sales_order_id = @sales_order_id;";
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteItems(SQLiteConnection connection, SQLiteTransaction transaction, long salesOrderId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM sales_items WHERE sales_order_id = @sales_order_id;";
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteOrder(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM sales_orders WHERE id = @id;";
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
                        throw new InvalidOperationException("商品不存在，无法销售。");
                    }

                    string status = reader.IsDBNull(3) ? "在售" : reader.GetString(3);
                    if (status != "在售")
                    {
                        throw new InvalidOperationException("停用商品不能销售。");
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
            IList<BatchQuantity> batches = GetDeductibleBatches(connection, transaction, productId);

            foreach (BatchQuantity batch in batches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                decimal deduct = batch.QuantityRemaining >= remaining ? remaining : batch.QuantityRemaining;
                decimal newRemaining = batch.QuantityRemaining - deduct;
                UpdateBatchRemaining(connection, transaction, batch.Id, newRemaining);
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

        private static SalesOrder ReadOrderSummary(SQLiteDataReader reader)
        {
            return new SalesOrder
            {
                Id = reader.GetInt64(0),
                OrderNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SaleTime = ParseDateTime(reader, 2),
                TotalAmount = Convert.ToDecimal(reader.GetValue(3)),
                TotalCost = Convert.ToDecimal(reader.GetValue(4)),
                GrossProfit = Convert.ToDecimal(reader.GetValue(5)),
                PaidAmount = Convert.ToDecimal(reader.GetValue(6)),
                Remark = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                CreatedAt = ParseDateTime(reader, 8),
                UpdatedAt = reader.IsDBNull(9) ? (DateTime?)null : DateTime.Parse(reader.GetString(9)),
                ProductKindCount = Convert.ToInt32(reader.GetValue(10)),
                TotalQuantity = Convert.ToDecimal(reader.GetValue(11))
            };
        }

        private static SalesItem ReadItem(SQLiteDataReader reader)
        {
            return new SalesItem
            {
                Id = reader.GetInt64(0),
                SalesOrderId = reader.GetInt64(1),
                ProductId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                ProductNameSnapshot = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Quantity = Convert.ToDecimal(reader.GetValue(4)),
                SalePriceSnapshot = Convert.ToDecimal(reader.GetValue(5)),
                CostPriceSnapshot = Convert.ToDecimal(reader.GetValue(6)),
                LineAmount = Convert.ToDecimal(reader.GetValue(7)),
                LineCost = Convert.ToDecimal(reader.GetValue(8)),
                LineProfit = Convert.ToDecimal(reader.GetValue(9)),
                CreatedAt = ParseDateTime(reader, 10),
                UpdatedAt = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11))
            };
        }

        private static void CalculateTotals(SalesOrder order)
        {
            order.TotalAmount = 0;
            order.TotalCost = 0;
            foreach (SalesItem item in order.Items)
            {
                item.LineAmount = item.Quantity * item.SalePriceSnapshot;
                item.LineCost = item.Quantity * item.CostPriceSnapshot;
                item.LineProfit = item.LineAmount - item.LineCost;
                order.TotalAmount += item.LineAmount;
                order.TotalCost += item.LineCost;
            }

            order.GrossProfit = order.TotalAmount - order.TotalCost;
            if (!order.PaidAmountSpecified)
            {
                order.PaidAmount = order.TotalAmount;
            }

            order.CreditAmount = order.PaidAmount < order.TotalAmount ? order.TotalAmount - order.PaidAmount : 0;
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

        private static string GenerateOrderNo()
        {
            return "SAL-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
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
