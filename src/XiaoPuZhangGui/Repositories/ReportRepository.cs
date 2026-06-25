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

        public IList<Product> GetInventoryItems()
        {
            List<Product> items = new List<Product>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT p.id, p.name, p.category_id, IFNULL(c.name, '') AS category_name,
       p.barcode, p.specification, p.default_price, p.current_stock,
       p.average_cost, p.min_stock_alert, p.requires_expiry, p.expiry_date,
       p.status, p.remark, p.created_at, p.updated_at
FROM products p
LEFT JOIN categories c ON c.id = p.category_id
ORDER BY p.status ASC, p.name ASC, p.id ASC;";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(ReadProduct(reader));
                    }
                }
            }

            return items;
        }

        public IList<CreditRecord> GetOutstandingCreditRecords()
        {
            List<CreditRecord> records = new List<CreditRecord>();

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            using (SQLiteCommand command = connection.CreateCommand())
            {
                connection.Open();
                command.CommandText = @"
SELECT c.id, c.credit_no, c.sales_order_id, IFNULL(o.order_no, '') AS order_no,
       c.debtor_name, c.original_amount, c.paid_amount, c.remaining_amount,
       c.status, c.credit_date, c.settled_at, c.remark, c.created_at, c.updated_at
FROM credit_records c
LEFT JOIN sales_orders o ON o.id = c.sales_order_id
WHERE IFNULL(c.status, '') <> 'Settled'
  AND c.remaining_amount > 0
ORDER BY date(IFNULL(NULLIF(c.credit_date, ''), c.created_at)) DESC, c.id DESC;";

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        records.Add(ReadCreditRecord(reader));
                    }
                }
            }

            return records;
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

        public IList<ProfitTrendPoint> GetProfitTrend(DateTime startTime, DateTime endTime, TimeSpan bucketDuration, int bucketMonths)
        {
            List<ProfitTrendPoint> points = bucketMonths > 0
                ? CreateMonthTrendPoints(startTime, endTime, bucketMonths)
                : CreateDurationTrendPoints(startTime, endTime, bucketDuration);

            if (points.Count == 0)
            {
                return points;
            }

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                ApplySalesProfitTrend(connection, points, startTime, endTime, bucketDuration, bucketMonths);
                ApplyScrapLossTrend(connection, points, startTime, endTime, bucketDuration, bucketMonths);
            }

            return points;
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

        private static List<ProfitTrendPoint> CreateDurationTrendPoints(DateTime startTime, DateTime endTime, TimeSpan bucketDuration)
        {
            List<ProfitTrendPoint> points = new List<ProfitTrendPoint>();
            if (bucketDuration.TotalMinutes <= 0)
            {
                bucketDuration = TimeSpan.FromDays(1);
            }

            DateTime cursor = startTime;
            while (cursor < endTime)
            {
                DateTime next = cursor.Add(bucketDuration);
                if (next > endTime)
                {
                    next = endTime;
                }

                points.Add(new ProfitTrendPoint
                {
                    StartTime = cursor,
                    EndTime = next,
                    Label = FormatTrendLabel(cursor, bucketDuration)
                });
                cursor = next;
            }

            return points;
        }

        private static List<ProfitTrendPoint> CreateMonthTrendPoints(DateTime startTime, DateTime endTime, int bucketMonths)
        {
            List<ProfitTrendPoint> points = new List<ProfitTrendPoint>();
            if (bucketMonths <= 0)
            {
                bucketMonths = 1;
            }

            DateTime cursor = startTime;
            while (cursor < endTime)
            {
                DateTime next = cursor.AddMonths(bucketMonths);
                if (next > endTime)
                {
                    next = endTime;
                }

                points.Add(new ProfitTrendPoint
                {
                    StartTime = cursor,
                    EndTime = next,
                    Label = cursor.ToString(bucketMonths >= 12 ? "yyyy" : "yyyy-MM")
                });
                cursor = next;
            }

            return points;
        }

        private static void ApplySalesProfitTrend(SQLiteConnection connection, IList<ProfitTrendPoint> points, DateTime startTime, DateTime endTime, TimeSpan bucketDuration, int bucketMonths)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT IFNULL(NULLIF(sale_time, ''), sold_at) AS trend_time,
       IFNULL(gross_profit, 0) AS trend_value
FROM sales_orders
WHERE datetime(IFNULL(NULLIF(sale_time, ''), sold_at)) >= datetime(@start_time)
  AND datetime(IFNULL(NULLIF(sale_time, ''), sold_at)) < datetime(@end_time);";
                AddRangeParameters(command, startTime, endTime);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime trendTime = ParseDateTime(reader, 0);
                        decimal value = Convert.ToDecimal(reader.GetValue(1));
                        int index = ResolveTrendBucketIndex(trendTime, startTime, bucketDuration, bucketMonths);
                        if (index >= 0 && index < points.Count)
                        {
                            points[index].GrossProfit += value;
                        }
                    }
                }
            }
        }

        private static void ApplyScrapLossTrend(SQLiteConnection connection, IList<ProfitTrendPoint> points, DateTime startTime, DateTime endTime, TimeSpan bucketDuration, int bucketMonths)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT scrap_date AS trend_time,
       IFNULL(loss_amount, 0) AS trend_value
FROM scrap_records
WHERE datetime(scrap_date) >= datetime(@start_time)
  AND datetime(scrap_date) < datetime(@end_time);";
                AddRangeParameters(command, startTime, endTime);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime trendTime = ParseDateTime(reader, 0);
                        decimal value = Convert.ToDecimal(reader.GetValue(1));
                        int index = ResolveTrendBucketIndex(trendTime, startTime, bucketDuration, bucketMonths);
                        if (index >= 0 && index < points.Count)
                        {
                            points[index].ScrapLoss += value;
                        }
                    }
                }
            }
        }

        private static int ResolveTrendBucketIndex(DateTime trendTime, DateTime startTime, TimeSpan bucketDuration, int bucketMonths)
        {
            if (bucketMonths > 0)
            {
                int startMonth = startTime.Year * 12 + startTime.Month - 1;
                int valueMonth = trendTime.Year * 12 + trendTime.Month - 1;
                int monthDelta = valueMonth - startMonth;
                return monthDelta < 0 ? -1 : monthDelta / bucketMonths;
            }

            double seconds = Math.Max(1D, bucketDuration.TotalSeconds);
            return (int)Math.Floor((trendTime - startTime).TotalSeconds / seconds);
        }

        private static string FormatTrendLabel(DateTime time, TimeSpan bucketDuration)
        {
            if (bucketDuration.TotalHours < 24)
            {
                return time.ToString("HH:mm");
            }

            if (bucketDuration.TotalDays < 365)
            {
                return time.ToString("M-d");
            }

            return time.ToString("yyyy");
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

        private static Product ReadProduct(SQLiteDataReader reader)
        {
            return new Product
            {
                Id = reader.GetInt64(0),
                Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                CategoryId = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                CategoryName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Barcode = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                Specification = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                DefaultPrice = Convert.ToDecimal(reader.GetValue(6)),
                CurrentStock = Convert.ToDecimal(reader.GetValue(7)),
                AverageCost = Convert.ToDecimal(reader.GetValue(8)),
                MinStockAlert = Convert.ToDecimal(reader.GetValue(9)),
                RequiresExpiry = !reader.IsDBNull(10) && Convert.ToInt32(reader.GetValue(10)) == 1,
                ExpiryDate = reader.IsDBNull(11) ? (DateTime?)null : DateTime.Parse(reader.GetString(11)),
                Status = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                Remark = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
                CreatedAt = ParseDateTime(reader, 14),
                UpdatedAt = reader.IsDBNull(15) ? (DateTime?)null : DateTime.Parse(reader.GetString(15))
            };
        }

        private static CreditRecord ReadCreditRecord(SQLiteDataReader reader)
        {
            return new CreditRecord
            {
                Id = reader.GetInt64(0),
                CreditNo = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                SalesOrderId = reader.GetInt64(2),
                SalesOrderNo = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                DebtorName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                OriginalAmount = Convert.ToDecimal(reader.GetValue(5)),
                PaidAmount = Convert.ToDecimal(reader.GetValue(6)),
                RemainingAmount = Convert.ToDecimal(reader.GetValue(7)),
                Status = reader.IsDBNull(8) ? "Unpaid" : reader.GetString(8),
                CreditDate = ParseDateTime(reader, 9),
                SettledAt = reader.IsDBNull(10) ? (DateTime?)null : DateTime.Parse(reader.GetString(10)),
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
    }
}
