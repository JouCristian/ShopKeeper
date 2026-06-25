using System;
using System.Collections.Generic;
using System.Data.SQLite;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Repositories
{
    internal sealed class CreditRepository
    {
        private readonly string _connectionString;

        public CreditRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IList<CreditRecord> Search(DateTime startDate, DateTime endDate, string debtorKeyword, string status)
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
WHERE date(c.credit_date) >= date(@start_date)
  AND date(c.credit_date) <= date(@end_date)
  AND (@debtor = '' OR c.debtor_name LIKE @debtor_like)
  AND (@status = '全部' OR c.status = @status)
ORDER BY date(c.credit_date) DESC, c.id DESC;";
                command.Parameters.AddWithValue("@start_date", startDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@end_date", endDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@debtor", debtorKeyword ?? string.Empty);
                command.Parameters.AddWithValue("@debtor_like", "%" + (debtorKeyword ?? string.Empty) + "%");
                command.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(status) ? "全部" : status);

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

        public CreditRecord GetById(long id)
        {
            CreditRecord record = null;

            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteCommand command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT c.id, c.credit_no, c.sales_order_id, IFNULL(o.order_no, '') AS order_no,
       c.debtor_name, c.original_amount, c.paid_amount, c.remaining_amount,
       c.status, c.credit_date, c.settled_at, c.remark, c.created_at, c.updated_at
FROM credit_records c
LEFT JOIN sales_orders o ON o.id = c.sales_order_id
WHERE c.id = @id;";
                    command.Parameters.AddWithValue("@id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            record = ReadRecord(reader);
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
SELECT id, credit_record_id, payment_date, amount, remark, created_at, updated_at
FROM credit_payments
WHERE credit_record_id = @credit_record_id
ORDER BY date(payment_date) ASC, id ASC;";
                    command.Parameters.AddWithValue("@credit_record_id", id);

                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            record.Payments.Add(ReadPayment(reader));
                        }
                    }
                }
            }

            return record;
        }

        public void RegisterPayment(long creditRecordId, decimal amount, DateTime paymentDate, string remark)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        CreditRecord record = GetById(connection, transaction, creditRecordId);
                        if (record == null)
                        {
                            throw new InvalidOperationException("赊账记录不存在。");
                        }

                        if (record.Status == "Settled" || record.RemainingAmount <= 0)
                        {
                            throw new InvalidOperationException("该赊账已结清。");
                        }

                        InsertPayment(connection, transaction, creditRecordId, amount, paymentDate, remark);

                        decimal newPaidAmount = record.PaidAmount + amount;
                        decimal newRemainingAmount = record.OriginalAmount - newPaidAmount;
                        if (newRemainingAmount < 0)
                        {
                            newRemainingAmount = 0;
                        }

                        string newStatus = newRemainingAmount == 0 ? "Settled" : "PartiallyPaid";
                        UpdateCreditAfterPayment(connection, transaction, creditRecordId, newPaidAmount, newRemainingAmount, newStatus, paymentDate);

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

        public void Delete(long id)
        {
            using (SQLiteConnection connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();

                using (SQLiteTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        CreditRecord record = GetById(connection, transaction, id);
                        if (record == null)
                        {
                            throw new InvalidOperationException("赊账记录不存在或已被删除。");
                        }

                        DeletePayments(connection, transaction, id);
                        DeleteRepayments(connection, transaction, id);
                        DeleteRecord(connection, transaction, id);
                        ClearSalesOrderCredit(connection, transaction, record.SalesOrderId);

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

        internal static long InsertInitialCredit(SQLiteConnection connection, SQLiteTransaction transaction, long salesOrderId, string debtorName, decimal originalAmount, string remark)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO credit_records
    (credit_no, sales_order_id, debtor_name, original_amount, paid_amount,
     remaining_amount, status, credit_date, settled_at, remark,
     contact_remark, credit_amount, repaid_amount, balance_amount, created_at)
VALUES
    (@credit_no, @sales_order_id, @debtor_name, @original_amount, 0,
     @remaining_amount, 'Unpaid', @credit_date, NULL, @remark,
     @debtor_name, @original_amount, 0, @remaining_amount, @created_at);
SELECT last_insert_rowid();";
                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                command.Parameters.AddWithValue("@credit_no", GenerateCreditNo());
                command.Parameters.AddWithValue("@sales_order_id", salesOrderId);
                command.Parameters.AddWithValue("@debtor_name", debtorName);
                command.Parameters.AddWithValue("@original_amount", originalAmount);
                command.Parameters.AddWithValue("@remaining_amount", originalAmount);
                command.Parameters.AddWithValue("@credit_date", DateTime.Today.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(remark));
                command.Parameters.AddWithValue("@created_at", now);
                return (long)command.ExecuteScalar();
            }
        }

        private static void DeletePayments(SQLiteConnection connection, SQLiteTransaction transaction, long creditRecordId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM credit_payments WHERE credit_record_id = @credit_record_id;";
                command.Parameters.AddWithValue("@credit_record_id", creditRecordId);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteRepayments(SQLiteConnection connection, SQLiteTransaction transaction, long creditRecordId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM repayment_records WHERE credit_record_id = @credit_record_id;";
                command.Parameters.AddWithValue("@credit_record_id", creditRecordId);
                command.ExecuteNonQuery();
            }
        }

        private static void DeleteRecord(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM credit_records WHERE id = @id;";
                command.Parameters.AddWithValue("@id", id);
                command.ExecuteNonQuery();
            }
        }

        private static void ClearSalesOrderCredit(SQLiteConnection connection, SQLiteTransaction transaction, long salesOrderId)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE sales_orders
SET paid_amount = total_amount,
    credit_amount = 0,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", salesOrderId);
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static CreditRecord GetById(SQLiteConnection connection, SQLiteTransaction transaction, long id)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
SELECT c.id, c.credit_no, c.sales_order_id, IFNULL(o.order_no, '') AS order_no,
       c.debtor_name, c.original_amount, c.paid_amount, c.remaining_amount,
       c.status, c.credit_date, c.settled_at, c.remark, c.created_at, c.updated_at
FROM credit_records c
LEFT JOIN sales_orders o ON o.id = c.sales_order_id
WHERE c.id = @id;";
                command.Parameters.AddWithValue("@id", id);

                using (SQLiteDataReader reader = command.ExecuteReader())
                {
                    return reader.Read() ? ReadRecord(reader) : null;
                }
            }
        }

        private static void InsertPayment(SQLiteConnection connection, SQLiteTransaction transaction, long creditRecordId, decimal amount, DateTime paymentDate, string remark)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO credit_payments
    (credit_record_id, payment_date, amount, remark, created_at)
VALUES
    (@credit_record_id, @payment_date, @amount, @remark, @created_at);";
                command.Parameters.AddWithValue("@credit_record_id", creditRecordId);
                command.Parameters.AddWithValue("@payment_date", paymentDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@amount", amount);
                command.Parameters.AddWithValue("@remark", EmptyToDbNull(remark));
                command.Parameters.AddWithValue("@created_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static void UpdateCreditAfterPayment(SQLiteConnection connection, SQLiteTransaction transaction, long creditRecordId, decimal paidAmount, decimal remainingAmount, string status, DateTime paymentDate)
        {
            using (SQLiteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
UPDATE credit_records
SET paid_amount = @paid_amount,
    remaining_amount = @remaining_amount,
    status = @status,
    settled_at = CASE WHEN @status = 'Settled' THEN @settled_at ELSE settled_at END,
    repaid_amount = @paid_amount,
    balance_amount = @remaining_amount,
    updated_at = @updated_at
WHERE id = @id;";
                command.Parameters.AddWithValue("@id", creditRecordId);
                command.Parameters.AddWithValue("@paid_amount", paidAmount);
                command.Parameters.AddWithValue("@remaining_amount", remainingAmount);
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@settled_at", paymentDate.ToString("yyyy-MM-dd"));
                command.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                command.ExecuteNonQuery();
            }
        }

        private static CreditRecord ReadRecord(SQLiteDataReader reader)
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

        private static CreditPayment ReadPayment(SQLiteDataReader reader)
        {
            return new CreditPayment
            {
                Id = reader.GetInt64(0),
                CreditRecordId = reader.GetInt64(1),
                PaymentDate = ParseDateTime(reader, 2),
                Amount = Convert.ToDecimal(reader.GetValue(3)),
                Remark = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                CreatedAt = ParseDateTime(reader, 5),
                UpdatedAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6))
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

        private static string GenerateCreditNo()
        {
            return "CRD-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }

        private static object EmptyToDbNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }
    }
}
