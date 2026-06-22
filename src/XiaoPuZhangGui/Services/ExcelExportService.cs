using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using XiaoPuZhangGui.Models;
using XiaoPuZhangGui.Utils;

namespace XiaoPuZhangGui.Services
{
    internal sealed class ExcelExportService
    {
        private readonly ReportService _reportService;

        public ExcelExportService()
            : this(new ReportService())
        {
        }

        internal ExcelExportService(ReportService reportService)
        {
            _reportService = reportService;
        }

        public string ExportReport(string reportName, DateTime startTime, DateTime endTime)
        {
            ReportSummary summary = _reportService.GetSummary(startTime, endTime);
            IList<ProductSalesRankItem> salesRank = _reportService.GetProductSalesRank(startTime, endTime);
            IList<ProductProfitRankItem> profitRank = _reportService.GetProductProfitRank(startTime, endTime);
            IList<LowStockReportItem> lowStockItems = _reportService.GetLowStockItems();
            IList<ExpiringProductReportItem> expiringItems = _reportService.GetExpiringProductsForExport();
            IList<ScrapSummaryItem> scrapItems = _reportService.GetScrapSummary(startTime, endTime);

            IWorkbook workbook = new XSSFWorkbook();
            WorkbookStyles styles = new WorkbookStyles(workbook);

            WriteSummarySheet(workbook, styles, reportName, summary, startTime, endTime);
            WriteSalesRankSheet(workbook, styles, salesRank);
            WriteProfitRankSheet(workbook, styles, profitRank);
            WriteLowStockSheet(workbook, styles, lowStockItems);
            WriteExpiringSheet(workbook, styles, expiringItems);
            WriteScrapSheet(workbook, styles, scrapItems);

            string path = BuildReportPath(reportName, startTime, endTime);
            SaveWorkbook(workbook, path);
            return path;
        }

        public string ExportInventoryList()
        {
            IList<Product> products = _reportService.GetInventoryItemsForExport();
            IWorkbook workbook = new XSSFWorkbook();
            WorkbookStyles styles = new WorkbookStyles(workbook);
            ISheet sheet = workbook.CreateSheet("库存清单");

            WriteTitle(sheet, styles, "库存清单", 12);
            WriteHeader(sheet, styles, 1, new string[]
            {
                "商品名称", "分类", "条码", "规格", "默认售价", "当前库存",
                "库存均价", "最低库存", "是否启用保质期", "到期日期", "状态", "库存提醒", "备注"
            });

            if (products.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 13);
            }
            else
            {
                for (int i = 0; i < products.Count; i++)
                {
                    Product item = products[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.Name, styles.TextStyle);
                    SetCell(row, 1, item.CategoryName, styles.TextStyle);
                    SetCell(row, 2, item.Barcode, styles.TextStyle);
                    SetCell(row, 3, item.Specification, styles.TextStyle);
                    SetMoneyCell(row, 4, item.DefaultPrice, styles.MoneyStyle);
                    SetMoneyCell(row, 5, item.CurrentStock, styles.QuantityStyle);
                    SetMoneyCell(row, 6, item.AverageCost, styles.MoneyStyle);
                    SetMoneyCell(row, 7, item.MinStockAlert, styles.QuantityStyle);
                    SetCell(row, 8, item.RequiresExpiry ? "是" : "否", styles.TextStyle);
                    SetDateCell(row, 9, item.ExpiryDate, styles.DateStyle);
                    SetCell(row, 10, ToDisplayStatus(item.Status), styles.TextStyle);
                    SetCell(row, 11, item.CurrentStock <= item.MinStockAlert ? "低库存" : string.Empty, styles.TextStyle);
                    SetCell(row, 12, item.Remark, styles.TextStyle);
                }
            }

            FinishSheet(sheet, 13);
            string path = BuildSimplePath("库存清单");
            SaveWorkbook(workbook, path);
            return path;
        }

        public string ExportCreditList()
        {
            IList<CreditRecord> records = _reportService.GetOutstandingCreditRecordsForExport();
            IWorkbook workbook = new XSSFWorkbook();
            WorkbookStyles styles = new WorkbookStyles(workbook);
            ISheet sheet = workbook.CreateSheet("赊账清单");

            WriteTitle(sheet, styles, "未结清赊账清单", 8);
            WriteHeader(sheet, styles, 1, new string[]
            {
                "赊账单号", "赊账日期", "欠款人备注", "原始欠款金额",
                "已还金额", "剩余欠款", "状态", "结清时间", "备注"
            });

            if (records.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 9);
            }
            else
            {
                for (int i = 0; i < records.Count; i++)
                {
                    CreditRecord item = records[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.CreditNo, styles.TextStyle);
                    SetDateCell(row, 1, item.CreditDate, styles.DateStyle);
                    SetCell(row, 2, item.DebtorName, styles.TextStyle);
                    SetMoneyCell(row, 3, item.OriginalAmount, styles.MoneyStyle);
                    SetMoneyCell(row, 4, item.PaidAmount, styles.MoneyStyle);
                    SetMoneyCell(row, 5, item.RemainingAmount, styles.MoneyStyle);
                    SetCell(row, 6, ToCreditStatusText(item.Status), styles.TextStyle);
                    SetDateCell(row, 7, item.SettledAt, styles.DateStyle);
                    SetCell(row, 8, item.Remark, styles.TextStyle);
                }
            }

            FinishSheet(sheet, 9);
            string path = BuildSimplePath("赊账清单");
            SaveWorkbook(workbook, path);
            return path;
        }

        public string ExportExpiringList()
        {
            IList<ExpiringProductReportItem> items = _reportService.GetExpiringProductsForExport();
            IWorkbook workbook = new XSSFWorkbook();
            WorkbookStyles styles = new WorkbookStyles(workbook);
            ISheet sheet = workbook.CreateSheet("临期商品");

            WriteTitle(sheet, styles, "临期商品清单", 5);
            WriteHeader(sheet, styles, 1, new string[]
            {
                "商品名称", "批次数量", "到期日期", "剩余天数", "状态", "批次号"
            });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 6);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ExpiringProductReportItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.ProductName, styles.TextStyle);
                    SetMoneyCell(row, 1, item.QuantityRemaining, styles.QuantityStyle);
                    SetDateCell(row, 2, item.ExpiryDate, styles.DateStyle);
                    SetNumberCell(row, 3, item.DaysRemaining, styles.IntegerStyle);
                    SetCell(row, 4, item.DaysRemaining < 0 ? "已过期" : "临期", styles.TextStyle);
                    SetCell(row, 5, item.BatchCode, styles.TextStyle);
                }
            }

            FinishSheet(sheet, 6);
            string path = BuildSimplePath("临期商品清单");
            SaveWorkbook(workbook, path);
            return path;
        }

        public string ExportDirectory
        {
            get { return AppPaths.ExportDirectory; }
        }

        public void OpenExportDirectory()
        {
            AppPaths.EnsureDirectory(AppPaths.ExportDirectory);
            Process.Start(AppPaths.ExportDirectory);
        }

        private static void WriteSummarySheet(IWorkbook workbook, WorkbookStyles styles, string reportName, ReportSummary summary, DateTime startTime, DateTime endTime)
        {
            ISheet sheet = workbook.CreateSheet("经营汇总");
            WriteTitle(sheet, styles, reportName + "经营汇总", 1);
            WriteHeader(sheet, styles, 1, new string[] { "项目", "数值" });

            int rowIndex = 2;
            WriteTextMetric(sheet, styles, rowIndex++, "统计范围", FormatRange(startTime, endTime));
            WriteMoneyMetric(sheet, styles, rowIndex++, "销售应收", summary.SalesReceivable);
            WriteMoneyMetric(sheet, styles, rowIndex++, "实收金额", summary.SalesPaid);
            WriteMoneyMetric(sheet, styles, rowIndex++, "新增赊账", summary.NewCredit);
            WriteMoneyMetric(sheet, styles, rowIndex++, "收回赊账", summary.CreditCollected);
            WriteMoneyMetric(sheet, styles, rowIndex++, "当前未收赊账", summary.OutstandingCredit);
            WriteMoneyMetric(sheet, styles, rowIndex++, "商品成本", summary.ProductCost);
            WriteMoneyMetric(sheet, styles, rowIndex++, "销售毛利润", summary.GrossProfit);
            WriteMoneyMetric(sheet, styles, rowIndex++, "报废损失", summary.ScrapLoss);
            WriteMoneyMetric(sheet, styles, rowIndex++, "商品净利润", summary.NetProfit);
            WriteNumberMetric(sheet, styles, rowIndex++, "销售单数", summary.SalesOrderCount);
            WriteMoneyMetric(sheet, styles, rowIndex++, "卖出件数", summary.SoldQuantity);
            WriteMoneyMetric(sheet, styles, rowIndex++, "进货总额", summary.PurchaseTotal);
            WriteTextMetric(sheet, styles, rowIndex, "导出时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            FinishSheet(sheet, 2);
        }

        private static void WriteSalesRankSheet(IWorkbook workbook, WorkbookStyles styles, IList<ProductSalesRankItem> items)
        {
            ISheet sheet = workbook.CreateSheet("销量排行");
            WriteTitle(sheet, styles, "商品销量排行", 3);
            WriteHeader(sheet, styles, 1, new string[] { "排名", "商品名称", "销售数量", "销售金额" });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 4);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ProductSalesRankItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetNumberCell(row, 0, i + 1, styles.IntegerStyle);
                    SetCell(row, 1, item.ProductName, styles.TextStyle);
                    SetMoneyCell(row, 2, item.SalesQuantity, styles.QuantityStyle);
                    SetMoneyCell(row, 3, item.SalesAmount, styles.MoneyStyle);
                }
            }

            FinishSheet(sheet, 4);
        }

        private static void WriteProfitRankSheet(IWorkbook workbook, WorkbookStyles styles, IList<ProductProfitRankItem> items)
        {
            ISheet sheet = workbook.CreateSheet("毛利排行");
            WriteTitle(sheet, styles, "商品毛利润排行", 5);
            WriteHeader(sheet, styles, 1, new string[] { "排名", "商品名称", "销售数量", "销售金额", "商品成本", "毛利润" });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 6);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ProductProfitRankItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetNumberCell(row, 0, i + 1, styles.IntegerStyle);
                    SetCell(row, 1, item.ProductName, styles.TextStyle);
                    SetMoneyCell(row, 2, item.SalesQuantity, styles.QuantityStyle);
                    SetMoneyCell(row, 3, item.SalesAmount, styles.MoneyStyle);
                    SetMoneyCell(row, 4, item.ProductCost, styles.MoneyStyle);
                    SetMoneyCell(row, 5, item.GrossProfit, styles.MoneyStyle);
                }
            }

            FinishSheet(sheet, 6);
        }

        private static void WriteLowStockSheet(IWorkbook workbook, WorkbookStyles styles, IList<LowStockReportItem> items)
        {
            ISheet sheet = workbook.CreateSheet("低库存提醒");
            WriteTitle(sheet, styles, "低库存提醒", 3);
            WriteHeader(sheet, styles, 1, new string[] { "商品名称", "分类", "当前库存", "最低库存" });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 4);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    LowStockReportItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.ProductName, styles.TextStyle);
                    SetCell(row, 1, item.CategoryName, styles.TextStyle);
                    SetMoneyCell(row, 2, item.CurrentStock, styles.QuantityStyle);
                    SetMoneyCell(row, 3, item.MinStockAlert, styles.QuantityStyle);
                }
            }

            FinishSheet(sheet, 4);
        }

        private static void WriteExpiringSheet(IWorkbook workbook, WorkbookStyles styles, IList<ExpiringProductReportItem> items)
        {
            ISheet sheet = workbook.CreateSheet("临期提醒");
            WriteTitle(sheet, styles, "临期商品提醒", 4);
            WriteHeader(sheet, styles, 1, new string[] { "商品名称", "批次数量", "到期日期", "剩余天数", "状态" });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 5);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ExpiringProductReportItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.ProductName, styles.TextStyle);
                    SetMoneyCell(row, 1, item.QuantityRemaining, styles.QuantityStyle);
                    SetDateCell(row, 2, item.ExpiryDate, styles.DateStyle);
                    SetNumberCell(row, 3, item.DaysRemaining, styles.IntegerStyle);
                    SetCell(row, 4, item.DaysRemaining < 0 ? "已过期" : "临期", styles.TextStyle);
                }
            }

            FinishSheet(sheet, 5);
        }

        private static void WriteScrapSheet(IWorkbook workbook, WorkbookStyles styles, IList<ScrapSummaryItem> items)
        {
            ISheet sheet = workbook.CreateSheet("报废摘要");
            WriteTitle(sheet, styles, "报废摘要", 3);
            WriteHeader(sheet, styles, 1, new string[] { "商品名称", "报废数量", "损失金额", "原因" });

            if (items.Count == 0)
            {
                WriteEmptyRow(sheet, styles, 2, 4);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    ScrapSummaryItem item = items[i];
                    IRow row = sheet.CreateRow(i + 2);
                    SetCell(row, 0, item.ProductName, styles.TextStyle);
                    SetMoneyCell(row, 1, item.Quantity, styles.QuantityStyle);
                    SetMoneyCell(row, 2, item.LossAmount, styles.MoneyStyle);
                    SetCell(row, 3, item.Reason, styles.TextStyle);
                }
            }

            FinishSheet(sheet, 4);
        }

        private static void WriteTitle(ISheet sheet, WorkbookStyles styles, string title, int lastColumn)
        {
            IRow row = sheet.CreateRow(0);
            row.HeightInPoints = 24;
            ICell cell = row.CreateCell(0);
            cell.SetCellValue(title);
            cell.CellStyle = styles.TitleStyle;
            if (lastColumn > 0)
            {
                sheet.AddMergedRegion(new CellRangeAddress(0, 0, 0, lastColumn));
            }
        }

        private static void WriteHeader(ISheet sheet, WorkbookStyles styles, int rowIndex, string[] headers)
        {
            IRow row = sheet.CreateRow(rowIndex);
            for (int i = 0; i < headers.Length; i++)
            {
                SetCell(row, i, headers[i], styles.HeaderStyle);
            }
        }

        private static void WriteEmptyRow(ISheet sheet, WorkbookStyles styles, int rowIndex, int columnCount)
        {
            IRow row = sheet.CreateRow(rowIndex);
            SetCell(row, 0, "暂无数据", styles.TextStyle);
            if (columnCount > 1)
            {
                sheet.AddMergedRegion(new CellRangeAddress(rowIndex, rowIndex, 0, columnCount - 1));
            }
        }

        private static void WriteTextMetric(ISheet sheet, WorkbookStyles styles, int rowIndex, string name, string value)
        {
            IRow row = sheet.CreateRow(rowIndex);
            SetCell(row, 0, name, styles.TextStyle);
            SetCell(row, 1, value, styles.TextStyle);
        }

        private static void WriteMoneyMetric(ISheet sheet, WorkbookStyles styles, int rowIndex, string name, decimal value)
        {
            IRow row = sheet.CreateRow(rowIndex);
            SetCell(row, 0, name, styles.TextStyle);
            SetMoneyCell(row, 1, value, styles.MoneyStyle);
        }

        private static void WriteNumberMetric(ISheet sheet, WorkbookStyles styles, int rowIndex, string name, int value)
        {
            IRow row = sheet.CreateRow(rowIndex);
            SetCell(row, 0, name, styles.TextStyle);
            SetNumberCell(row, 1, value, styles.IntegerStyle);
        }

        private static void SetCell(IRow row, int columnIndex, string value, ICellStyle style)
        {
            ICell cell = row.CreateCell(columnIndex);
            cell.SetCellValue(value ?? string.Empty);
            cell.CellStyle = style;
        }

        private static void SetMoneyCell(IRow row, int columnIndex, decimal value, ICellStyle style)
        {
            ICell cell = row.CreateCell(columnIndex);
            cell.SetCellValue(Convert.ToDouble(value));
            cell.CellStyle = style;
        }

        private static void SetNumberCell(IRow row, int columnIndex, int value, ICellStyle style)
        {
            ICell cell = row.CreateCell(columnIndex);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static void SetDateCell(IRow row, int columnIndex, DateTime value, ICellStyle style)
        {
            ICell cell = row.CreateCell(columnIndex);
            cell.SetCellValue(value);
            cell.CellStyle = style;
        }

        private static void SetDateCell(IRow row, int columnIndex, DateTime? value, ICellStyle style)
        {
            ICell cell = row.CreateCell(columnIndex);
            if (value.HasValue)
            {
                cell.SetCellValue(value.Value);
            }
            else
            {
                cell.SetCellValue(string.Empty);
            }

            cell.CellStyle = style;
        }

        private static void FinishSheet(ISheet sheet, int columnCount)
        {
            for (int i = 0; i < columnCount; i++)
            {
                sheet.AutoSizeColumn(i);
                int width = sheet.GetColumnWidth(i);
                if (width < 2800)
                {
                    sheet.SetColumnWidth(i, 2800);
                }
                else if (width > 7600)
                {
                    sheet.SetColumnWidth(i, 7600);
                }
            }

            sheet.CreateFreezePane(0, 2);
        }

        private static void SaveWorkbook(IWorkbook workbook, string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(stream);
            }
        }

        private static string BuildReportPath(string reportName, DateTime startTime, DateTime endTime)
        {
            if (reportName == "日报")
            {
                return BuildPath("小铺掌柜_日报_" + startTime.ToString("yyyy-MM-dd") + ".xlsx");
            }

            if (reportName == "月报")
            {
                return BuildPath("小铺掌柜_月报_" + startTime.ToString("yyyy-MM") + ".xlsx");
            }

            return BuildPath("小铺掌柜_经营报表_" + startTime.ToString("yyyy-MM-dd") + "_至_" + endTime.AddDays(-1).ToString("yyyy-MM-dd") + ".xlsx");
        }

        private static string BuildSimplePath(string name)
        {
            return BuildPath("小铺掌柜_" + name + "_" + DateTime.Today.ToString("yyyy-MM-dd") + ".xlsx");
        }

        private static string BuildPath(string fileName)
        {
            AppPaths.EnsureDirectory(AppPaths.ExportDirectory);
            string path = Path.Combine(AppPaths.ExportDirectory, fileName);
            if (!File.Exists(path))
            {
                return path;
            }

            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string timestamp = DateTime.Now.ToString("HHmmss");
            return Path.Combine(AppPaths.ExportDirectory, name + "_" + timestamp + extension);
        }

        private static string FormatRange(DateTime startTime, DateTime endTime)
        {
            return startTime.ToString("yyyy-MM-dd") + " 至 " + endTime.AddDays(-1).ToString("yyyy-MM-dd");
        }

        private static string ToDisplayStatus(string status)
        {
            if (status == "在售" || status == "停用")
            {
                return status;
            }

            if (status == "鍦ㄥ敭")
            {
                return "在售";
            }

            if (status == "鍋滅敤")
            {
                return "停用";
            }

            return status ?? string.Empty;
        }

        private static string ToCreditStatusText(string status)
        {
            if (status == "Settled")
            {
                return "已结清";
            }

            if (status == "PartiallyPaid")
            {
                return "部分还款";
            }

            return "未结清";
        }

        private sealed class WorkbookStyles
        {
            public WorkbookStyles(IWorkbook workbook)
            {
                IDataFormat dataFormat = workbook.CreateDataFormat();

                TitleStyle = workbook.CreateCellStyle();
                IFont titleFont = workbook.CreateFont();
                titleFont.IsBold = true;
                titleFont.FontHeightInPoints = 14;
                TitleStyle.SetFont(titleFont);
                TitleStyle.Alignment = HorizontalAlignment.Left;

                HeaderStyle = workbook.CreateCellStyle();
                IFont headerFont = workbook.CreateFont();
                headerFont.IsBold = true;
                HeaderStyle.SetFont(headerFont);
                HeaderStyle.FillForegroundColor = IndexedColors.PaleBlue.Index;
                HeaderStyle.FillPattern = FillPattern.SolidForeground;

                TextStyle = workbook.CreateCellStyle();
                TextStyle.WrapText = false;

                MoneyStyle = workbook.CreateCellStyle();
                MoneyStyle.DataFormat = dataFormat.GetFormat("#,##0.00");

                QuantityStyle = workbook.CreateCellStyle();
                QuantityStyle.DataFormat = dataFormat.GetFormat("#,##0.00");

                IntegerStyle = workbook.CreateCellStyle();
                IntegerStyle.DataFormat = dataFormat.GetFormat("#,##0");

                DateStyle = workbook.CreateCellStyle();
                DateStyle.DataFormat = dataFormat.GetFormat("yyyy-mm-dd");
            }

            public ICellStyle TitleStyle { get; private set; }

            public ICellStyle HeaderStyle { get; private set; }

            public ICellStyle TextStyle { get; private set; }

            public ICellStyle MoneyStyle { get; private set; }

            public ICellStyle QuantityStyle { get; private set; }

            public ICellStyle IntegerStyle { get; private set; }

            public ICellStyle DateStyle { get; private set; }
        }
    }
}
