using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class ReportRepository
    {
        private readonly string _connectionString;

        public ReportRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public ReportSummary GetSummary(DateTime startTime, DateTime endTime)
        {
            ReportSummary summary = new ReportSummary
            {
                StartTime = startTime,
                EndTime = endTime
            };

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                ReadSalesSummary(connection, startTime, endTime, summary);
                summary.SoldQuantity = ReadDecimal(connection, @"
SELECT IFNULL(SUM(i.quantity), 0)
FROM sales_items i
INNER JOIN sales_orders o ON o.id = i.sales_order_id
WHERE datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) < datetime(@end_time);", startTime, endTime);
                summary.NewCredit = ReadDecimal(connection, @"
SELECT IFNULL(SUM(original_amount), 0)
FROM credit_records
WHERE datetime(IFNULL(NULLIF(credit_date, ''), created_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(credit_date, ''), created_at)) < datetime(@end_time);", startTime, endTime);
                summary.CreditCollected = ReadDecimal(connection, @"
SELECT IFNULL(SUM(amount), 0)
FROM credit_payments
WHERE datetime(IFNULL(NULLIF(payment_date, ''), created_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(payment_date, ''), created_at)) < datetime(@end_time);", startTime, endTime);
                summary.OutstandingCredit = ReadDecimal(connection, @"
SELECT IFNULL(SUM(remaining_amount), 0)
FROM credit_records
WHERE IFNULL(status, '') <> 'Settled'
  AND remaining_amount > 0;", null, null);
                summary.ScrapLoss = ReadDecimal(connection, @"
SELECT IFNULL(SUM(loss_amount), 0)
FROM scrap_records
WHERE datetime(scrap_date) >= datetime(@start_time)
  AND datetime(scrap_date) < datetime(@end_time);", startTime, endTime);
                summary.PurchaseTotal = ReadDecimal(connection, @"
SELECT IFNULL(SUM(total_amount), 0)
FROM purchase_records
WHERE datetime(purchase_date) >= datetime(@start_time)
  AND datetime(purchase_date) < datetime(@end_time);", startTime, endTime);
            }

            summary.NetProfit = summary.GrossProfit - summary.ScrapLoss;
            return summary;
        }

        public IList<ProductSalesRankItem> GetProductSalesRank(DateTime startTime, DateTime endTime, int limit)
        {
            List<ProductSalesRankItem> items = new List<ProductSalesRankItem>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT IFNULL(i.product_id, 0) AS product_id,
       IFNULL(NULLIF(i.product_name_snapshot, ''), '未命名商品') AS product_name,
       IFNULL(SUM(i.quantity), 0) AS sales_quantity,
       IFNULL(SUM(i.line_amount), 0) AS sales_amount
FROM sales_items i
INNER JOIN sales_orders o ON o.id = i.sales_order_id
WHERE datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) < datetime(@end_time)
GROUP BY IFNULL(i.product_id, 0), IFNULL(NULLIF(i.product_name_snapshot, ''), '未命名商品')
ORDER BY sales_quantity DESC, sales_amount DESC
LIMIT @limit;";
                AddRangeParameters(command, startTime, endTime);
                command.Parameters.AddWithValue("@limit", limit);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new ProductSalesRankItem
                        {
                            ProductId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                            ProductName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            SalesQuantity = Convert.ToDecimal(reader.GetValue(2)),
                            SalesAmount = Convert.ToDecimal(reader.GetValue(3))
                        });
                    }
                }
            }

            return items;
        }

        public IList<ProductProfitRankItem> GetProductProfitRank(DateTime startTime, DateTime endTime, int limit)
        {
            List<ProductProfitRankItem> items = new List<ProductProfitRankItem>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT IFNULL(i.product_id, 0) AS product_id,
       IFNULL(NULLIF(i.product_name_snapshot, ''), '未命名商品') AS product_name,
       IFNULL(SUM(i.quantity), 0) AS sales_quantity,
       IFNULL(SUM(i.line_amount), 0) AS sales_amount,
       IFNULL(SUM(i.line_cost), 0) AS product_cost,
       IFNULL(SUM(i.line_profit), 0) AS gross_profit
FROM sales_items i
INNER JOIN sales_orders o ON o.id = i.sales_order_id
WHERE datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(o.sale_time, ''), o.sold_at)) < datetime(@end_time)
GROUP BY IFNULL(i.product_id, 0), IFNULL(NULLIF(i.product_name_snapshot, ''), '未命名商品')
ORDER BY gross_profit DESC, sales_amount DESC
LIMIT @limit;";
                AddRangeParameters(command, startTime, endTime);
                command.Parameters.AddWithValue("@limit", limit);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new ProductProfitRankItem
                        {
                            ProductId = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
                            ProductName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            SalesQuantity = Convert.ToDecimal(reader.GetValue(2)),
                            SalesAmount = Convert.ToDecimal(reader.GetValue(3)),
                            ProductCost = Convert.ToDecimal(reader.GetValue(4)),
                            GrossProfit = Convert.ToDecimal(reader.GetValue(5))
                        });
                    }
                }
            }

            return items;
        }

        public IList<LowStockReportItem> GetLowStockItems(int limit)
        {
            List<LowStockReportItem> items = new List<LowStockReportItem>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT p.name, IFNULL(c.name, '') AS category_name, p.current_stock, p.min_stock_alert
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
WHERE p.status = @active_status
  AND p.current_stock <= p.min_stock_alert
ORDER BY (p.current_stock - p.min_stock_alert) ASC, p.name ASC
LIMIT @limit;";
                command.Parameters.AddWithValue("@active_status", "在售");
                command.Parameters.AddWithValue("@limit", limit);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new LowStockReportItem
                        {
                            ProductName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            CategoryName = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            CurrentStock = Convert.ToDecimal(reader.GetValue(2)),
                            MinStockAlert = Convert.ToDecimal(reader.GetValue(3))
                        });
                    }
                }
            }

            return items;
        }

        public IList<ExpiringProductReportItem> GetExpiringProducts(DateTime today, int days, int limit)
        {
            List<ExpiringProductReportItem> items = new List<ExpiringProductReportItem>();
            DateTime maxDate = today.Date.AddDays(days);

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT p.name, IFNULL(NULLIF(b.batch_code, ''), '') AS batch_code,
       b.quantity_remaining, b.expiry_date
FROM stock_batches b
INNER JOIN products p ON p.id = b.product_id
WHERE b.quantity_remaining > 0
  AND b.expiry_date IS NOT NULL
  AND b.expiry_date <> ''
  AND date(b.expiry_date) <= date(@max_date)
ORDER BY date(b.expiry_date) ASC, p.name ASC
LIMIT @limit;";
                command.Parameters.AddWithValue("@max_date", maxDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@limit", limit);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime expiryDate = ParseDate(reader, 3);
                        items.Add(new ExpiringProductReportItem
                        {
                            ProductName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            BatchCode = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                            QuantityRemaining = Convert.ToDecimal(reader.GetValue(2)),
                            ExpiryDate = expiryDate,
                            DaysRemaining = (expiryDate.Date - today.Date).Days
                        });
                    }
                }
            }

            return items;
        }

        public IList<ScrapSummaryItem> GetScrapSummary(DateTime startTime, DateTime endTime, int limit)
        {
            List<ScrapSummaryItem> items = new List<ScrapSummaryItem>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT product_name_snapshot, IFNULL(SUM(quantity), 0) AS quantity,
       IFNULL(SUM(loss_amount), 0) AS loss_amount, IFNULL(reason, '') AS reason
FROM scrap_records
WHERE datetime(scrap_date) >= datetime(@start_time)
  AND datetime(scrap_date) < datetime(@end_time)
GROUP BY product_id, product_name_snapshot, IFNULL(reason, '')
ORDER BY loss_amount DESC, quantity DESC
LIMIT @limit;";
                AddRangeParameters(command, startTime, endTime);
                command.Parameters.AddWithValue("@limit", limit);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new ScrapSummaryItem
                        {
                            ProductName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                            Quantity = Convert.ToDecimal(reader.GetValue(1)),
                            LossAmount = Convert.ToDecimal(reader.GetValue(2)),
                            Reason = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                        });
                    }
                }
            }

            return items;
        }

        private static void ReadSalesSummary(SQLiteConnection connection, DateTime startTime, DateTime endTime, ReportSummary summary)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT COUNT(*) AS order_count,
       IFNULL(SUM(total_amount), 0) AS sales_receivable,
       IFNULL(SUM(paid_amount), 0) AS sales_paid,
       IFNULL(SUM(total_cost), 0) AS product_cost,
       IFNULL(SUM(gross_profit), 0) AS gross_profit
FROM sales_orders
WHERE datetime(IFNULL(NULLIF(sale_time, ''), sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(sale_time, ''), sold_at)) < datetime(@end_time);";
                AddRangeParameters(command, startTime, endTime);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return;
                    }

                    summary.SalesOrderCount = Convert.ToInt32(reader.GetValue(0));
                    summary.SalesReceivable = Convert.ToDecimal(reader.GetValue(1));
                    summary.SalesPaid = Convert.ToDecimal(reader.GetValue(2));
                    summary.ProductCost = Convert.ToDecimal(reader.GetValue(3));
                    summary.GrossProfit = Convert.ToDecimal(reader.GetValue(4));
                }
            }
        }

        private static decimal ReadDecimal(SQLiteConnection connection, string sql, DateTime? startTime, DateTime? endTime)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                if (startTime.HasValue && endTime.HasValue)
                {
                    AddRangeParameters(command, startTime.Value, endTime.Value);
                }

                object value = command.ExecuteScalar();
                return value == null || value == DBNull.Value ? 0 : Convert.ToDecimal(value);
            }
        }

        private static void AddRangeParameters(SQLiteCommand command, DateTime startTime, DateTime endTime)
        {
            command.Parameters.AddWithValue("@start_time", startTime.ToString("yyyy-MM-dd HH:mm:ss"));
            command.Parameters.AddWithValue("@end_time", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        private static DateTime ParseDate(SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return DateTime.Today;
            }

            DateTime result;
            return DateTime.TryParse(reader.GetString(index), out result) ? result : DateTime.Today;
        }
    }
}
