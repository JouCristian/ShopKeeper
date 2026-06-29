using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiPurchaseDraftService
    {
        private static readonly string[] PieceUnits = { "瓶", "包", "件", "盒", "袋", "条", "个", "支", "罐" };

        private readonly ProductService _productService;
        private readonly PurchaseService _purchaseService;
        private readonly CategoryService _categoryService;

        public AiPurchaseDraftService()
        {
            _productService = new ProductService();
            _purchaseService = new PurchaseService();
            _categoryService = new CategoryService();
        }

        public bool IsPurchaseIntent(string text)
        {
            text = text ?? string.Empty;
            if (ContainsAny(text, "登记", "入库", "新进", "新到", "我进了", "进了", "采购了", "买了", "到货"))
            {
                return ContainsAny(text, "货", "商品", "进货", "入库", "采购", "瓶", "包", "件", "盒", "袋", "条", "个", "支", "罐", "箱");
            }

            if (ContainsAny(text, "补货建议", "进货建议", "采购建议", "分析", "该不该", "适合补"))
            {
                return false;
            }

            return ContainsAny(text, "进货", "采购", "补货")
                && ContainsAny(text, "登记", "保存", "记一下", "录入", "入库");
        }

        public AiPurchaseDraft UpdateDraft(AiPurchaseDraft currentDraft, string text)
        {
            AiPurchaseDraft draft = currentDraft ?? new AiPurchaseDraft();
            text = text ?? string.Empty;

            string productName = ExtractProductName(text);
            if (!string.IsNullOrWhiteSpace(productName))
            {
                draft.ProductName = productName;
            }

            string specification = ExtractSpecification(text);
            if (!string.IsNullOrWhiteSpace(specification))
            {
                draft.Specification = specification;
            }

            string categoryName = ExtractCategoryName(text);
            if (!string.IsNullOrWhiteSpace(categoryName))
            {
                draft.CategoryName = categoryName;
            }

            ApplyQuantity(text, draft);
            ApplyPrices(text, draft);
            ApplyDatesAndExpiry(text, draft);
            ResolveProductAndCategory(draft);
            return draft;
        }

        public AiPurchaseDraftReview Review(AiPurchaseDraft draft)
        {
            ResolveProductAndCategory(draft);

            Product matchedProduct = draft.MatchedProductId.HasValue
                ? _productService.GetById(draft.MatchedProductId.Value)
                : null;
            Category matchedCategory = ResolveCategory(draft.CategoryName);

            AiPurchaseDraftReview review = new AiPurchaseDraftReview
            {
                Draft = draft,
                MatchedProduct = matchedProduct,
                MatchedCategory = matchedCategory
            };

            if (string.IsNullOrWhiteSpace(draft.ProductName) && matchedProduct == null)
            {
                review.MissingRequiredFields.Add("商品名称");
            }

            if (draft.PackageCount.HasValue && !draft.UnitsPerPackage.HasValue)
            {
                review.MissingRequiredFields.Add("每箱有多少件，系统会统一换算成单件数量");
            }

            if (!draft.Quantity.HasValue || draft.Quantity.Value <= 0)
            {
                review.MissingRequiredFields.Add("入库数量");
            }

            if (!draft.PurchasePrice.HasValue)
            {
                review.MissingRequiredFields.Add("进货单价");
            }

            if (matchedProduct == null)
            {
                if (matchedCategory == null)
                {
                    review.MissingRequiredFields.Add("商品分类");
                }

                if (!draft.SalePrice.HasValue)
                {
                    review.MissingRequiredFields.Add("预计售价");
                }
            }

            if (string.IsNullOrWhiteSpace(draft.Specification))
            {
                review.OptionalReminders.Add("未填写规格，例如 500ml、100g。不是必填，但建议补充，后续更好区分商品。");
            }

            if (!draft.RequiresExpiry.HasValue || draft.RequiresExpiry.Value)
            {
                if (!draft.ProductionDate.HasValue && !draft.ExpiryDate.HasValue)
                {
                    review.OptionalReminders.Add("未填写生产日期或到期日期。本次可以先不启用保质期，但饮料、食品建议后续补充。");
                }
            }

            if (!draft.SalePrice.HasValue && matchedProduct != null)
            {
                review.OptionalReminders.Add("未填写预计售价，将保留商品当前售价 " + FormatMoney(matchedProduct.DefaultPrice) + " 元。");
            }

            if (matchedProduct == null)
            {
                draft.ShouldCreateProduct = true;
            }

            return review;
        }

        public AiPurchaseExecutionResult Execute(AiPurchaseDraft draft)
        {
            AiPurchaseDraftReview review = Review(draft);
            if (!review.IsReady)
            {
                return new AiPurchaseExecutionResult
                {
                    Success = false,
                    Message = "信息还不完整，不能执行入库。"
                };
            }

            try
            {
                Product product = review.MatchedProduct;
                bool createdProduct = false;
                if (product == null)
                {
                    product = new Product
                    {
                        Name = BuildProductDisplayName(draft),
                        CategoryId = review.MatchedCategory.Id,
                        CategoryName = review.MatchedCategory.Name,
                        Barcode = string.Empty,
                        Specification = draft.Specification ?? string.Empty,
                        DefaultPrice = draft.SalePrice.GetValueOrDefault(),
                        CurrentStock = 0,
                        AverageCost = 0,
                        MinStockAlert = 0,
                        RequiresExpiry = draft.RequiresExpiry.GetValueOrDefault(false),
                        ExpiryDate = draft.ExpiryDate,
                        Status = "在售",
                        Remark = "AI 入库助手自动创建"
                    };

                    string productMessage;
                    if (!_productService.TrySave(product, out productMessage))
                    {
                        return new AiPurchaseExecutionResult { Success = false, Message = productMessage };
                    }

                    createdProduct = true;
                }
                else if (draft.SalePrice.HasValue && product.DefaultPrice != draft.SalePrice.Value)
                {
                    product.DefaultPrice = draft.SalePrice.Value;
                    string productMessage;
                    if (!_productService.TrySave(product, out productMessage))
                    {
                        return new AiPurchaseExecutionResult { Success = false, Message = productMessage };
                    }
                }

                PurchaseRecord record = new PurchaseRecord
                {
                    PurchaseDate = draft.PurchaseDate == DateTime.MinValue ? DateTime.Today : draft.PurchaseDate,
                    Remark = "AI 入库助手登记"
                };
                record.Items.Add(new PurchaseItem
                {
                    ProductId = product.Id,
                    Quantity = draft.Quantity.GetValueOrDefault(),
                    PurchasePrice = draft.PurchasePrice.GetValueOrDefault(),
                    ProductionDate = draft.ProductionDate,
                    ExpiryDate = draft.ExpiryDate,
                    Remark = draft.Remark ?? string.Empty
                });

                string purchaseMessage;
                if (!_purchaseService.TrySave(record, out purchaseMessage))
                {
                    return new AiPurchaseExecutionResult { Success = false, Message = purchaseMessage };
                }

                return new AiPurchaseExecutionResult
                {
                    Success = true,
                    ProductId = product.Id,
                    PurchaseRecordId = record.Id,
                    CreatedProduct = createdProduct,
                    Message = (createdProduct ? "已新增商品，并完成入库。" : "已完成入库。")
                };
            }
            catch (Exception ex)
            {
                return new AiPurchaseExecutionResult
                {
                    Success = false,
                    Message = "执行入库失败：" + ex.Message
                };
            }
        }

        public string BuildMissingFieldsMessage(AiPurchaseDraftReview review)
        {
            string productText = string.IsNullOrWhiteSpace(review.Draft.ProductName)
                ? "这批货"
                : BuildProductDisplayName(review.Draft);
            List<string> lines = new List<string>();
            lines.Add("我理解你要登记一笔入库：" + productText);
            lines.Add(string.Empty);
            lines.Add("还需要你补充：");
            for (int index = 0; index < review.MissingRequiredFields.Count; index++)
            {
                lines.Add((index + 1) + ". " + review.MissingRequiredFields[index]);
            }

            if (review.OptionalReminders.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("非必填提醒：");
                foreach (string reminder in review.OptionalReminders)
                {
                    lines.Add("- " + reminder);
                }
            }

            lines.Add(string.Empty);
            lines.Add("你可以直接回复缺少的信息，例如：“一箱24瓶，分类饮料”。");
            return string.Join("\r\n", lines.ToArray());
        }

        public string BuildReadyMessage(AiPurchaseDraftReview review)
        {
            AiPurchaseDraft draft = review.Draft;
            Product product = review.MatchedProduct;
            string action = product == null ? "新增商品并登记入库" : "登记已有商品入库";
            List<string> lines = new List<string>();
            lines.Add("已整理好入库信息，请确认。");
            lines.Add(string.Empty);
            lines.Add("准备执行：" + action);
            lines.Add("商品：" + (product == null ? BuildProductDisplayName(draft) : product.Name));
            lines.Add("分类：" + (review.MatchedCategory != null ? review.MatchedCategory.Name : (product != null ? product.CategoryName : draft.CategoryName)));
            lines.Add("入库数量：" + FormatNumber(draft.Quantity.GetValueOrDefault()) + " 件");
            lines.Add("进货单价：" + FormatMoney(draft.PurchasePrice.GetValueOrDefault()) + " 元");
            lines.Add("预计售价：" + (draft.SalePrice.HasValue ? FormatMoney(draft.SalePrice.Value) + " 元" : "保留当前售价"));
            lines.Add("保质期：" + (draft.RequiresExpiry.GetValueOrDefault(false) ? "启用" : "暂不启用"));
            if (draft.RequiresExpiry.GetValueOrDefault(false))
            {
                lines.Add("生产日期：" + (draft.ProductionDate.HasValue ? draft.ProductionDate.Value.ToString("yyyy-MM-dd") : "未填写"));
                lines.Add("到期日期：" + (draft.ExpiryDate.HasValue ? draft.ExpiryDate.Value.ToString("yyyy-MM-dd") : "未填写"));
            }

            lines.Add("入库日期：" + draft.PurchaseDate.ToString("yyyy-MM-dd"));

            if (review.OptionalReminders.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("非必填提醒：");
                foreach (string reminder in review.OptionalReminders)
                {
                    lines.Add("- " + reminder);
                }
            }

            return string.Join("\r\n", lines.ToArray());
        }

        private void ResolveProductAndCategory(AiPurchaseDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            Product product = FindProduct(draft.ProductName, draft.Specification);
            draft.MatchedProductId = product == null ? (long?)null : product.Id;
            draft.ShouldCreateProduct = product == null;

            if (string.IsNullOrWhiteSpace(draft.CategoryName))
            {
                draft.CategoryName = InferCategoryName(draft.ProductName);
            }
        }

        private Product FindProduct(string productName, string specification)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return null;
            }

            IList<Product> products = _productService.Search(productName.Trim(), null, "在售");
            Product fallback = null;
            foreach (Product product in products)
            {
                if (fallback == null)
                {
                    fallback = product;
                }

                bool nameMatches = ContainsNormalized(product.Name, productName);
                bool specMatches = string.IsNullOrWhiteSpace(specification)
                    || ContainsNormalized(product.Specification, specification)
                    || ContainsNormalized(product.Name, specification);
                if (nameMatches && specMatches)
                {
                    return product;
                }
            }

            return fallback;
        }

        private Category ResolveCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            foreach (Category category in _categoryService.GetActiveCategories())
            {
                if (string.Equals(category.Name, categoryName.Trim(), StringComparison.OrdinalIgnoreCase)
                    || category.Name.IndexOf(categoryName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
                    || categoryName.IndexOf(category.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return category;
                }
            }

            return null;
        }

        private string InferCategoryName(string productName)
        {
            if (string.IsNullOrWhiteSpace(productName))
            {
                return string.Empty;
            }

            string text = productName.ToLowerInvariant();
            string category = string.Empty;
            if (ContainsAny(text, "可乐", "雪碧", "饮料", "矿泉水", "啤酒", "牛奶", "茶", "果汁"))
            {
                category = "饮料";
            }
            else if (ContainsAny(text, "烟", "香烟"))
            {
                category = "香烟";
            }
            else if (ContainsAny(text, "面包", "饼干", "薯片", "零食", "糖", "方便面"))
            {
                category = "零食";
            }

            return ResolveCategory(category) == null ? string.Empty : category;
        }

        private static string ExtractProductName(string text)
        {
            string candidate = MatchValue(text, @"\d+(?:\.\d+)?\s*(?:瓶|包|件|盒|袋|条|个|支|罐|箱)\s*(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,24})");
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = MatchValue(text, @"(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,24})\s*\d+(?:\.\d+)?\s*(?:瓶|包|件|盒|袋|条|个|支|罐|箱)");
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = MatchValue(text, @"(?:商品|货品|名称)(?:是|为|叫)?\s*(?<value>[\u4e00-\u9fa5A-Za-z0-9\s]{1,24})");
            }

            return CleanProductName(candidate);
        }

        private static string CleanProductName(string value)
        {
            value = (value ?? string.Empty).Trim();
            value = Regex.Replace(value, @"\d+(?:\.\d+)?\s*(?:ml|ML|毫升|l|L|升|g|G|克|kg|KG|千克)", string.Empty);
            value = Regex.Replace(value, @"(的|每瓶|每件|每个|进货价|售价|建议售价|分类|保质期|暂时|先不考虑).*", string.Empty);
            value = value.Replace("，", " ").Replace(",", " ").Trim();
            return value;
        }

        private static string ExtractSpecification(string text)
        {
            Match match = Regex.Match(text ?? string.Empty, @"(?<value>\d+(?:\.\d+)?\s*(?:ml|ML|毫升|l|L|升|g|G|克|kg|KG|千克))");
            return match.Success ? match.Groups["value"].Value.Replace(" ", string.Empty) : string.Empty;
        }

        private static string ExtractCategoryName(string text)
        {
            return MatchValue(text, @"(?:分类|类别)(?:是|为|就是)?\s*(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,12})");
        }

        private static void ApplyQuantity(string text, AiPurchaseDraft draft)
        {
            Match packageMatch = Regex.Match(text ?? string.Empty, @"(?<count>\d+(?:\.\d+)?)\s*箱");
            if (packageMatch.Success)
            {
                draft.PackageCount = ParseDecimal(packageMatch.Groups["count"].Value);
                draft.QuantityUnit = "件";
            }

            Match perPackageMatch = Regex.Match(text ?? string.Empty, @"(?:一箱|每箱|1箱)\s*(?<count>\d+(?:\.\d+)?)\s*(?:瓶|包|件|盒|袋|条|个|支|罐)");
            if (perPackageMatch.Success)
            {
                draft.UnitsPerPackage = ParseDecimal(perPackageMatch.Groups["count"].Value);
            }

            if (draft.PackageCount.HasValue && draft.UnitsPerPackage.HasValue)
            {
                draft.Quantity = draft.PackageCount.Value * draft.UnitsPerPackage.Value;
                return;
            }

            foreach (string unit in PieceUnits)
            {
                Match match = Regex.Match(text ?? string.Empty, @"(?<count>\d+(?:\.\d+)?)\s*" + Regex.Escape(unit));
                if (match.Success)
                {
                    draft.Quantity = ParseDecimal(match.Groups["count"].Value);
                    draft.QuantityUnit = unit;
                    return;
                }
            }
        }

        private static void ApplyPrices(string text, AiPurchaseDraft draft)
        {
            decimal? purchasePrice = MatchDecimal(text, @"(?:进货价|进价|成本价|成本单价|成本|采购价)(?:每瓶|每件|每个|单价)?(?:是|为|每瓶)?\s*(?<value>\d+(?:\.\d+)?)");
            if (purchasePrice.HasValue)
            {
                draft.PurchasePrice = purchasePrice.Value;
            }

            decimal? salePrice = MatchDecimal(text, @"(?:建议的售价|建议售价|售价|卖价|零售价|卖)(?:每瓶|每件|每个|单价)?(?:是|为)?\s*(?<value>\d+(?:\.\d+)?)");
            if (salePrice.HasValue)
            {
                draft.SalePrice = salePrice.Value;
            }
        }

        private static void ApplyDatesAndExpiry(string text, AiPurchaseDraft draft)
        {
            text = text ?? string.Empty;
            DateTime? productionDate = MatchDate(text, @"(?:生产日期|生产日|生产时间)(?:是|为)?\s*(?<value>\d{4}(?:[-/年.]\d{1,2})(?:[-/月.]\d{1,2})日?)");
            if (productionDate.HasValue)
            {
                draft.ProductionDate = productionDate.Value;
            }

            DateTime? expiryDate = MatchDate(text, @"(?:到期日期|到期日|过期日期|过期日|保质期到|有效期到)(?:是|为)?\s*(?<value>\d{4}(?:[-/年.]\d{1,2})(?:[-/月.]\d{1,2})日?)");
            if (expiryDate.HasValue)
            {
                draft.ExpiryDate = expiryDate.Value;
            }

            if (draft.ProductionDate.HasValue || draft.ExpiryDate.HasValue)
            {
                draft.RequiresExpiry = true;
            }

            if (ContainsAny(text, "不考虑保质期", "不启用保质期", "没有保质期", "先不考虑保质期"))
            {
                draft.RequiresExpiry = false;
            }
            else if (ContainsAny(text, "保质期", "到期", "过期", "生产日期"))
            {
                draft.RequiresExpiry = true;
            }
        }

        private static DateTime? MatchDate(string text, string pattern)
        {
            string value = MatchValue(text, pattern);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return ParseDate(value);
        }

        private static DateTime? ParseDate(string text)
        {
            string normalized = (text ?? string.Empty).Trim()
                .Replace("年", "-")
                .Replace("月", "-")
                .Replace("日", string.Empty)
                .Replace("/", "-")
                .Replace(".", "-");
            string[] formats =
            {
                "yyyy-M-d",
                "yyyy-MM-dd",
                "yyyy-M-dd",
                "yyyy-MM-d"
            };

            DateTime value;
            if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return value.Date;
            }

            if (DateTime.TryParse(normalized, out value))
            {
                return value.Date;
            }

            return null;
        }

        private static decimal? MatchDecimal(string text, string pattern)
        {
            string value = MatchValue(text, pattern);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return ParseDecimal(value);
        }

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
        }

        private static decimal ParseDecimal(string text)
        {
            decimal value;
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static bool ContainsAny(string text, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value) && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsNormalized(string source, string value)
        {
            source = (source ?? string.Empty).Replace(" ", string.Empty);
            value = (value ?? string.Empty).Replace(" ", string.Empty);
            return !string.IsNullOrWhiteSpace(source)
                && !string.IsNullOrWhiteSpace(value)
                && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildProductDisplayName(AiPurchaseDraft draft)
        {
            if (draft == null)
            {
                return string.Empty;
            }

            string name = (draft.ProductName ?? string.Empty).Trim();
            string specification = (draft.Specification ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(specification) && name.IndexOf(specification, StringComparison.OrdinalIgnoreCase) < 0)
            {
                name = (name + " " + specification).Trim();
            }

            return name;
        }

        private static string FormatMoney(decimal value)
        {
            return value.ToString("0.00");
        }

        private static string FormatNumber(decimal value)
        {
            return value.ToString("0.###");
        }
    }
}
