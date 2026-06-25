using System;
using System.Collections.Generic;
using XiaoPuZhangGui.Database;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Repositories;

namespace XiaoPuZhangGui.Services
{
    internal sealed class CreditService
    {
        private readonly CreditRepository _creditRepository;

        public CreditService()
            : this(DatabaseService.BuildConnectionString(AppConfigService.LoadOrCreateDefault().DatabasePath))
        {
        }

        internal CreditService(string connectionString)
        {
            _creditRepository = new CreditRepository(connectionString);
        }

        public IList<CreditRecord> Search(DateTime startDate, DateTime endDate, string debtorKeyword, string statusText)
        {
            if (endDate < startDate)
            {
                DateTime temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            return _creditRepository.Search(
                startDate.Date,
                endDate.Date,
                string.IsNullOrWhiteSpace(debtorKeyword) ? string.Empty : debtorKeyword.Trim(),
                ToStatusValue(statusText));
        }

        public CreditRecord GetById(long id)
        {
            return _creditRepository.GetById(id);
        }

        public bool TryRegisterPayment(long creditRecordId, decimal amount, DateTime paymentDate, string remark, out string message)
        {
            CreditRecord record = _creditRepository.GetById(creditRecordId);
            if (record == null)
            {
                message = "赊账记录不存在。";
                return false;
            }

            if (record.Status == "Settled" || record.RemainingAmount <= 0)
            {
                message = "该赊账已结清。";
                return false;
            }

            if (amount <= 0)
            {
                message = "本次还款金额必须大于 0。";
                return false;
            }

            if (amount > record.RemainingAmount)
            {
                message = "本次还款金额不能超过剩余欠款。";
                return false;
            }

            _creditRepository.RegisterPayment(creditRecordId, amount, paymentDate.Date, (remark ?? string.Empty).Trim());
            message = "还款已登记。";
            return true;
        }

        public bool TryDelete(long id, out string message)
        {
            try
            {
                _creditRepository.Delete(id);
                message = "赊账记录已删除，关联销售单已清除赊账余额。";
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static string ToStatusValue(string statusText)
        {
            if (statusText == "未结清")
            {
                return "Unpaid";
            }

            if (statusText == "部分还款")
            {
                return "PartiallyPaid";
            }

            if (statusText == "已结清")
            {
                return "Settled";
            }

            return "全部";
        }
    }
}
