using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using XiaoPuZhangGui.Models;

namespace XiaoPuZhangGui.Services
{
    internal sealed class AiActionDraftService
    {
        private static readonly string[] PieceUnits = { "瓶", "包", "袋", "条", "件", "个", "箱", "盒", "支", "根", "听", "罐", "桶", "杯", "提", "板" };

        private readonly ProductService _productService;
        private readonly CategoryService _categoryService;
        private readonly PurchaseService _purchaseService;
        private readonly SalesService _salesService;
        private readonly InventoryCheckService _inventoryCheckService;
        private readonly ScrapService _scrapService;
        private readonly AiOperationLogService _operationLogService;

        public AiActionDraftService()
        {
            _productService = new ProductService();
            _categoryService = new CategoryService();
            _purchaseService = new PurchaseService();
            _salesService = new SalesService();
            _inventoryCheckService = new InventoryCheckService();
            _scrapService = new ScrapService();
            _operationLogService = new AiOperationLogService();
        }

        public bool MayBeBusinessAction(string text)
        {
            text = text ?? string.Empty;
            return ContainsAny(text,
                "进了", "新进", "到货", "补货", "入库", "采购", "登记",
                "卖了", "销售", "收了", "记账",
                "盘点", "实际", "库存改", "修正库存",
                "赊账", "欠账", "没给钱", "记他",
                "卖", "售价", "改价", "价格",
                "删除", "撤销", "弄错");
        }

        public AiActionDraftParseResult CreateDraftFromJson(long conversationId, string userText, string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return AiActionDraftParseResult.Fail("AI 没有返回动作 JSON。");
            }

            try
            {
                string json = ExtractJson(rawJson);
                Dictionary<string, object> root = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
                if (root == null)
                {
                    return AiActionDraftParseResult.Fail("AI 返回的 JSON 格式无法识别。");
                }

                AiActionDraft draft = new AiActionDraft
                {
                    ConversationId = conversationId,
                    SourceUserMessage = userText ?? string.Empty,
                    RawAiJson = json,
                    Title = ReadString(root, "summary"),
                    Confidence = ReadDecimal(root, "confidence").GetValueOrDefault(0),
                    NeedUserClarification = ReadBool(root, "needUserClarification"),
                    ClarificationQuestion = ReadString(root, "clarificationQuestion")
                };

                object[] actions = root.ContainsKey("actions") ? root["actions"] as object[] : null;
                if (actions != null)
                {
                    for (int index = 0; index < actions.Length; index++)
                    {
                        Dictionary<string, object> action = actions[index] as Dictionary<string, object>;
                        if (action == null)
                        {
                            continue;
                        }

                        AiActionDraftItem item = ReadActionItem(action, index + 1);
                        item.DraftId = draft.Id;
                        draft.Items.Add(item);
                    }
                }

                if (draft.Items.Count == 0)
                {
                    draft.Items.Add(new AiActionDraftItem
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        DraftId = draft.Id,
                        ItemIndex = 1,
                        ActionType = AiActionTypes.Unknown,
                        Notes = "AI 没有识别出可执行的经营动作。"
                    });
                }

                ValidateDraft(draft);
                return new AiActionDraftParseResult
                {
                    Success = true,
                    Draft = draft,
                    RawJson = json,
                    ErrorMessage = string.Empty
                };
            }
            catch
            {
                return AiActionDraftParseResult.Fail("AI 返回的动作 JSON 解析失败。");
            }
        }

        public AiActionDraftParseResult CreateLocalFallbackDraft(long conversationId, string userText)
        {
            AiActionDraft draft = new AiActionDraft
            {
                ConversationId = conversationId,
                SourceUserMessage = userText ?? string.Empty,
                RawAiJson = string.Empty,
                Title = "本地规则识别出的动作草稿",
                Confidence = 0.55M
            };

            IList<AiActionDraftItem> items = ParseUndoItemsLocally(userText);
            if (items.Count == 0)
            {
                items = ParseScrapItemsLocally(userText);
            }

            if (items.Count == 0)
            {
                items = ParseInventoryAdjustItemsLocally(userText);
            }

            if (items.Count == 0)
            {
                items = ParsePriceUpdateItemsLocally(userText);
            }

            if (items.Count == 0)
            {
                items = ParseCreditItemsLocally(userText);
            }

            if (items.Count == 0)
            {
                items = ParseSaleItemsLocally(userText);
            }

            if (items.Count == 0)
            {
                items = ParsePurchaseItemsLocally(userText);
            }
            foreach (AiActionDraftItem item in items)
            {
                item.DraftId = draft.Id;
                item.ItemIndex = draft.Items.Count + 1;
                draft.Items.Add(item);
            }

            if (draft.Items.Count == 0)
            {
                draft.Items.Add(new AiActionDraftItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    DraftId = draft.Id,
                    ItemIndex = 1,
                    ActionType = AiActionTypes.Unknown,
                    Notes = "暂时无法识别成明确动作，请换一种说法，例如“进了24瓶可乐，进价2元”。"
                });
            }

            ValidateDraft(draft);
            return new AiActionDraftParseResult
            {
                Success = true,
                Draft = draft,
                RawJson = SerializeDraft(draft),
                ErrorMessage = string.Empty
            };
        }

        public void ValidateDraft(AiActionDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            string highestRisk = AiActionRiskLevels.Low;
            if (draft.Items == null)
            {
                draft.Items = new List<AiActionDraftItem>();
            }

            for (int index = 0; index < draft.Items.Count; index++)
            {
                AiActionDraftItem item = draft.Items[index];
                if (item.MissingFields == null)
                {
                    item.MissingFields = new List<string>();
                }

                if (item.Warnings == null)
                {
                    item.Warnings = new List<string>();
                }

                if (item.CandidateProductNames == null)
                {
                    item.CandidateProductNames = new List<string>();
                }

                item.ItemIndex = index + 1;
                item.DraftId = draft.Id;
                item.Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id;
                item.ActionType = NormalizeActionType(item.ActionType);
                item.Status = string.IsNullOrWhiteSpace(item.Status) ? AiActionDraftStatus.Pending : item.Status;
                NormalizeItem(item);
                ValidateItem(item);
                highestRisk = MaxRisk(highestRisk, item.RiskLevel);
            }

            draft.ActionType = draft.Items.Count == 1 ? draft.Items[0].ActionType : AiActionTypes.MultiAction;
            draft.RiskLevel = highestRisk;
            draft.Status = ResolveDraftStatus(draft);
            draft.UpdatedAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(draft.Title))
            {
                draft.Title = BuildDraftSummary(draft);
            }
        }

        public bool CanExecuteItem(AiActionDraftItem item)
        {
            if (item == null || item.Status == AiActionDraftStatus.Executed || item.Status == AiActionDraftStatus.Cancelled)
            {
                return false;
            }

            if (item.ActionType != AiActionTypes.PurchaseIn
                && item.ActionType != AiActionTypes.SaleRecord
                && item.ActionType != AiActionTypes.CreditRegister
                && item.ActionType != AiActionTypes.InventoryAdjust
                && item.ActionType != AiActionTypes.ProductPriceUpdate)
            {
                return false;
            }

            foreach (string field in item.MissingFields)
            {
                if (IsBlockingMissingField(field))
                {
                    return false;
                }
            }

            return true;
        }

        public AiActionExecutionResult Execute(AiActionDraft draft, AiActionDraftItem item)
        {
            ValidateItem(item);
            if (!CanExecuteItem(item))
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "这条草稿还不能执行，请先补全关键字段。",
                    BusinessRecordType = item == null ? string.Empty : item.ActionType
                };
            }

            AiActionExecutionResult result;
            if (item.ActionType == AiActionTypes.PurchaseIn)
            {
                result = ExecutePurchaseIn(item);
            }
            else if (item.ActionType == AiActionTypes.SaleRecord)
            {
                result = ExecuteSaleRecord(item);
            }
            else if (item.ActionType == AiActionTypes.CreditRegister)
            {
                result = ExecuteCreditRegister(item);
            }
            else if (item.ActionType == AiActionTypes.InventoryAdjust)
            {
                result = ExecuteInventoryAdjust(item);
            }
            else if (item.ActionType == AiActionTypes.ProductPriceUpdate)
            {
                result = ExecuteProductPriceUpdate(item);
            }
            else
            {
                result = new AiActionExecutionResult
                {
                    Success = false,
                    Message = "删除或撤销属于高风险动作，本轮只生成确认提醒，不直接写入数据库。",
                    BusinessRecordType = item.ActionType
                };
            }

            if (result.Success)
            {
                item.Status = AiActionDraftStatus.Executed;
            }
            else
            {
                item.Status = AiActionDraftStatus.Failed;
            }

            if (draft != null)
            {
                draft.Status = ResolveDraftStatus(draft);
                draft.UpdatedAt = DateTime.Now;
            }

            _operationLogService.Append(draft, item, result);
            return result;
        }

        public string SerializeDraft(AiActionDraft draft)
        {
            return new JavaScriptSerializer().Serialize(draft);
        }

        public AiActionDraft DeserializeDraft(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                AiActionDraft draft = new JavaScriptSerializer().Deserialize<AiActionDraft>(text);
                if (draft != null)
                {
                    ValidateDraft(draft);
                }

                return draft;
            }
            catch
            {
                return null;
            }
        }

        public string BuildDraftSummary(AiActionDraft draft)
        {
            if (draft == null || draft.Items.Count == 0)
            {
                return "AI 没有识别出明确的经营动作。";
            }

            int pendingCount = draft.Items.Count(item => item.Status != AiActionDraftStatus.Executed && item.Status != AiActionDraftStatus.Cancelled);
            return "AI 已生成 " + draft.Items.Count + " 条动作草稿，待确认 " + pendingCount + " 条。";
        }

        public string BuildItemDisplayTitle(AiActionDraftItem item)
        {
            if (item == null)
            {
                return "AI 动作草稿";
            }

            string actionText = ToActionText(item.ActionType);
            string product = string.IsNullOrWhiteSpace(item.ProductName) ? "未填写商品" : BuildProductDisplayName(item);
            if (item.ActionType == AiActionTypes.CreditRegister)
            {
                product = string.IsNullOrWhiteSpace(item.CustomerName) ? "未填写客户" : item.CustomerName;
            }

            return actionText + "：" + product;
        }

        public string BuildExecutionSuccessMessage(AiActionDraftItem item, AiActionExecutionResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(result.Message);
            builder.AppendLine();
            builder.AppendLine("动作：" + ToActionText(item.ActionType));
            if (item.ActionType == AiActionTypes.ProductPriceUpdate)
            {
                builder.AppendLine("商品：" + BuildProductDisplayName(item));
                if (item.PriceChangeOldValue.HasValue)
                {
                    builder.AppendLine("原售价：" + FormatMoney(item.PriceChangeOldValue.Value) + " 元");
                }

                if (item.PriceChangeNewValue.HasValue)
                {
                    builder.AppendLine("新售价：" + FormatMoney(item.PriceChangeNewValue.Value) + " 元");
                }

                builder.AppendLine("数据已写入：商品档案");
                return builder.ToString().TrimEnd();
            }

            if (item.ActionType == AiActionTypes.InventoryAdjust)
            {
                builder.AppendLine("商品：" + BuildProductDisplayName(item));
                if (IsScrapIntent(item))
                {
                    builder.AppendLine("报废数量：" + FormatNumber(item.Quantity.GetValueOrDefault()) + " " + (string.IsNullOrWhiteSpace(item.Unit) ? "件" : item.Unit));
                    builder.AppendLine("数据已写入：报废记录、库存扣减");
                }
                else
                {
                    builder.AppendLine("修正后库存：" + FormatNumber(item.InventoryAdjustQuantity.GetValueOrDefault()) + " " + (string.IsNullOrWhiteSpace(item.Unit) ? "件" : item.Unit));
                    builder.AppendLine("数据已写入：库存盘点、库存修正");
                }

                return builder.ToString().TrimEnd();
            }

            if (item.ActionType == AiActionTypes.CreditRegister)
            {
                builder.AppendLine("商品：" + BuildProductDisplayName(item));
                builder.AppendLine("客户：" + (string.IsNullOrWhiteSpace(item.CustomerName) ? "未填写" : item.CustomerName));
                if (item.Quantity.HasValue)
                {
                    builder.AppendLine("数量：" + FormatNumber(item.Quantity.Value) + " " + (string.IsNullOrWhiteSpace(item.Unit) ? "件" : item.Unit));
                }

                if (item.CreditAmount.HasValue)
                {
                    builder.AppendLine("赊账金额：" + FormatMoney(item.CreditAmount.Value) + " 元");
                }

                builder.AppendLine("数据已写入：销售记录、赊账记录、库存扣减");
                return builder.ToString().TrimEnd();
            }

            builder.AppendLine("商品：" + BuildProductDisplayName(item));
            if (item.Quantity.HasValue)
            {
                builder.AppendLine("数量：" + FormatNumber(item.Quantity.Value) + " " + (string.IsNullOrWhiteSpace(item.Unit) ? "件" : item.Unit));
            }

            if (item.PurchasePrice.HasValue)
            {
                builder.AppendLine("进货单价：" + FormatMoney(item.PurchasePrice.Value) + " 元");
            }

            if (item.SalePrice.HasValue)
            {
                builder.AppendLine("销售单价：" + FormatMoney(item.SalePrice.Value) + " 元");
            }

            if (item.ActualReceivedAmount.HasValue)
            {
                builder.AppendLine("实收金额：" + FormatMoney(item.ActualReceivedAmount.Value) + " 元");
            }

            builder.AppendLine(item.ActionType == AiActionTypes.SaleRecord
                ? "数据已写入：销售记录、库存扣减"
                : "数据已写入：商品管理、进货入库、库存批次");
            return builder.ToString().TrimEnd();
        }

        public string BuildNotExecutableMessage(AiActionDraftItem item)
        {
            if (item == null)
            {
                return "这条草稿暂时不能执行。";
            }

            if (item.ActionType == AiActionTypes.DeleteOrUndoRequest)
            {
                return "已生成“删除或撤销”草稿。删除和撤销属于高风险动作，本轮不会自动写库，请在确认对象和影响范围后到对应页面处理。";
            }

            if (item.MissingFields.Count == 0)
            {
                return "这条" + ToActionText(item.ActionType) + "草稿暂时不能执行，请检查字段。";
            }

            return "这条" + ToActionText(item.ActionType) + "草稿还不能执行："
                + string.Join("、", item.MissingFields.Select(ToFieldDisplayName).ToArray());
        }

        public string ToActionText(string actionType)
        {
            actionType = NormalizeActionType(actionType);
            if (actionType == AiActionTypes.PurchaseIn)
            {
                return "入库登记";
            }

            if (actionType == AiActionTypes.SaleRecord)
            {
                return "销售记账";
            }

            if (actionType == AiActionTypes.InventoryAdjust)
            {
                return "库存修正";
            }

            if (actionType == AiActionTypes.CreditRegister)
            {
                return "赊账登记";
            }

            if (actionType == AiActionTypes.ProductPriceUpdate)
            {
                return "修改售价";
            }

            if (actionType == AiActionTypes.DeleteOrUndoRequest)
            {
                return "删除或撤销";
            }

            return "未知动作";
        }

        public bool IsBlockingMissingField(string field)
        {
            string normalized = NormalizeFieldName(field);
            return normalized == "productName"
                || normalized == "quantity"
                || normalized == "purchasePrice"
                || normalized == "category"
                || normalized == "customerName"
                || normalized == "creditAmount"
                || normalized == "inventoryAdjustQuantity"
                || normalized == "priceChangeNewValue"
                || normalized == "matchedProduct"
                || normalized == "insufficientStock"
                || normalized == "actionType";
        }

        private AiActionExecutionResult ExecutePurchaseIn(AiActionDraftItem item)
        {
            try
            {
                Product product = item.MatchedProductId.HasValue ? _productService.GetById(item.MatchedProductId.Value) : null;
                bool createdProduct = false;
                if (product == null)
                {
                    Category category = ResolveCategory(item.Category);
                    if (category == null)
                    {
                        return new AiActionExecutionResult
                        {
                            Success = false,
                            Message = "无法执行入库：新商品缺少有效分类。",
                            BusinessRecordType = AiActionTypes.PurchaseIn
                        };
                    }

                    product = new Product
                    {
                        Name = BuildProductDisplayName(item),
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        Barcode = string.Empty,
                        Specification = item.ProductSpec ?? string.Empty,
                        DefaultPrice = item.SalePrice.GetValueOrDefault(0),
                        CurrentStock = 0,
                        AverageCost = 0,
                        MinStockAlert = 0,
                        RequiresExpiry = item.ShelfLifeEnabled.GetValueOrDefault(false),
                        ExpiryDate = item.ExpiryDate,
                        Status = "在售",
                        Remark = "AI 动作草稿自动创建"
                    };

                    string productMessage;
                    if (!_productService.TrySave(product, out productMessage))
                    {
                        return new AiActionExecutionResult
                        {
                            Success = false,
                            Message = productMessage,
                            BusinessRecordType = AiActionTypes.PurchaseIn
                        };
                    }

                    createdProduct = true;
                }
                else if (item.SalePrice.HasValue && product.DefaultPrice != item.SalePrice.Value)
                {
                    product.DefaultPrice = item.SalePrice.Value;
                    string productMessage;
                    if (!_productService.TrySave(product, out productMessage))
                    {
                        return new AiActionExecutionResult
                        {
                            Success = false,
                            Message = productMessage,
                            BusinessRecordType = AiActionTypes.PurchaseIn
                        };
                    }
                }

                PurchaseRecord record = new PurchaseRecord
                {
                    PurchaseDate = DateTime.Today,
                    Remark = "AI 动作草稿确认入库"
                };
                record.Items.Add(new PurchaseItem
                {
                    ProductId = product.Id,
                    Quantity = item.Quantity.GetValueOrDefault(),
                    PurchasePrice = item.PurchasePrice.GetValueOrDefault(),
                    ProductionDate = item.ProductionDate,
                    ExpiryDate = item.ExpiryDate,
                    Remark = item.Notes ?? string.Empty
                });

                string purchaseMessage;
                if (!_purchaseService.TrySave(record, out purchaseMessage))
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = purchaseMessage,
                        BusinessRecordType = AiActionTypes.PurchaseIn
                    };
                }

                return new AiActionExecutionResult
                {
                    Success = true,
                    Message = createdProduct ? "已新增商品，并完成入库。" : "已完成入库。",
                    BusinessRecordId = record.Id,
                    BusinessRecordType = AiActionTypes.PurchaseIn
                };
            }
            catch (Exception ex)
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "执行入库失败：" + ex.Message,
                    BusinessRecordType = AiActionTypes.PurchaseIn
                };
            }
        }

        private AiActionExecutionResult ExecuteSaleRecord(AiActionDraftItem item)
        {
            try
            {
                Product product = item.MatchedProductId.HasValue ? _productService.GetById(item.MatchedProductId.Value) : null;
                if (product == null)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行销售记账：没有匹配到系统商品。",
                        BusinessRecordType = AiActionTypes.SaleRecord
                    };
                }

                decimal quantity = item.Quantity.GetValueOrDefault();
                if (quantity <= 0)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行销售记账：销售数量必须大于 0。",
                        BusinessRecordType = AiActionTypes.SaleRecord
                    };
                }

                if (product.CurrentStock < quantity)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行销售记账：库存不足。当前库存 "
                            + FormatNumber(product.CurrentStock) + "，本次销售 " + FormatNumber(quantity) + "。",
                        BusinessRecordType = AiActionTypes.SaleRecord
                    };
                }

                decimal salePrice = item.SalePrice.GetValueOrDefault(product.DefaultPrice);
                decimal expectedAmount = quantity * salePrice;
                decimal paidAmount = item.ActualReceivedAmount.GetValueOrDefault(expectedAmount);

                decimal salePriceForOrder = salePrice;
                if (paidAmount >= 0 && quantity > 0 && Math.Abs(paidAmount - expectedAmount) >= 0.01M)
                {
                    salePriceForOrder = decimal.Round(paidAmount / quantity, 2, MidpointRounding.AwayFromZero);
                }

                SalesOrder order = new SalesOrder
                {
                    SaleTime = DateTime.Now,
                    PaidAmount = paidAmount,
                    PaidAmountSpecified = true,
                    Remark = BuildSaleRemark(item, expectedAmount, paidAmount)
                };
                order.Items.Add(new SalesItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    SalePriceSnapshot = salePriceForOrder,
                    ProductNameSnapshot = product.Name
                });

                string message;
                if (!_salesService.TrySave(order, out message))
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = message,
                        BusinessRecordType = AiActionTypes.SaleRecord
                    };
                }

                item.MatchedProductName = product.Name;
                item.ProductName = product.Name;
                item.ProductSpec = NormalizeSpec(product.Specification);
                NormalizeSpecAndUnit(item);
                item.SalePrice = salePriceForOrder;
                item.ActualReceivedAmount = paidAmount;
                return new AiActionExecutionResult
                {
                    Success = true,
                    Message = "已完成销售记账。",
                    BusinessRecordId = order.Id,
                    BusinessRecordType = AiActionTypes.SaleRecord
                };
            }
            catch (Exception ex)
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "执行销售记账失败：" + ex.Message,
                    BusinessRecordType = AiActionTypes.SaleRecord
                };
            }
        }

        private AiActionExecutionResult ExecuteCreditRegister(AiActionDraftItem item)
        {
            try
            {
                Product product = item.MatchedProductId.HasValue ? _productService.GetById(item.MatchedProductId.Value) : null;
                if (product == null)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行赊账登记：没有匹配到系统商品。本阶段暂不支持脱离商品销售单的纯欠款登记。",
                        BusinessRecordType = AiActionTypes.CreditRegister
                    };
                }

                decimal quantity = item.Quantity.GetValueOrDefault();
                if (quantity <= 0)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行赊账登记：销售数量必须大于 0。",
                        BusinessRecordType = AiActionTypes.CreditRegister
                    };
                }

                if (product.CurrentStock < quantity)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行赊账登记：库存不足。当前库存 "
                            + FormatNumber(product.CurrentStock) + "，本次销售 " + FormatNumber(quantity) + "。",
                        BusinessRecordType = AiActionTypes.CreditRegister
                    };
                }

                decimal salePrice = item.SalePrice.GetValueOrDefault(product.DefaultPrice);
                decimal totalAmount = salePrice * quantity;
                decimal creditAmount = item.CreditAmount.GetValueOrDefault(totalAmount);
                decimal paidAmount = item.ActualReceivedAmount.GetValueOrDefault(Math.Max(0, totalAmount - creditAmount));
                if (paidAmount > totalAmount)
                {
                    paidAmount = totalAmount;
                }

                SalesOrder order = new SalesOrder
                {
                    SaleTime = DateTime.Now,
                    PaidAmount = paidAmount,
                    PaidAmountSpecified = true,
                    DebtorName = (item.CustomerName ?? string.Empty).Trim(),
                    Remark = BuildCreditRemark(item, totalAmount, paidAmount)
                };
                order.Items.Add(new SalesItem
                {
                    ProductId = product.Id,
                    Quantity = quantity,
                    SalePriceSnapshot = salePrice,
                    ProductNameSnapshot = product.Name
                });

                string message;
                if (!_salesService.TrySave(order, out message))
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = message,
                        BusinessRecordType = AiActionTypes.CreditRegister
                    };
                }

                item.MatchedProductName = product.Name;
                item.ProductName = product.Name;
                item.ProductSpec = NormalizeSpec(product.Specification);
                item.SalePrice = salePrice;
                item.CreditAmount = order.CreditAmount;
                item.ActualReceivedAmount = paidAmount;
                NormalizeSpecAndUnit(item);
                return new AiActionExecutionResult
                {
                    Success = true,
                    Message = "已完成赊账登记。",
                    BusinessRecordId = order.Id,
                    BusinessRecordType = AiActionTypes.CreditRegister
                };
            }
            catch (Exception ex)
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "执行赊账登记失败：" + ex.Message,
                    BusinessRecordType = AiActionTypes.CreditRegister
                };
            }
        }

        private AiActionExecutionResult ExecuteInventoryAdjust(AiActionDraftItem item)
        {
            try
            {
                Product product = item.MatchedProductId.HasValue ? _productService.GetById(item.MatchedProductId.Value) : null;
                if (product == null)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行库存动作：没有匹配到系统商品。",
                        BusinessRecordType = AiActionTypes.InventoryAdjust
                    };
                }

                if (IsScrapIntent(item))
                {
                    decimal scrapQuantity = ResolveScrapQuantity(item, product);
                    ScrapRecord record = new ScrapRecord
                    {
                        ScrapDate = DateTime.Now,
                        ProductId = product.Id,
                        Quantity = scrapQuantity,
                        Reason = ResolveScrapReason(item),
                        Remark = string.IsNullOrWhiteSpace(item.Notes) ? "AI 动作草稿确认报废" : item.Notes.Trim()
                    };

                    string scrapMessage;
                    if (!_scrapService.TrySave(record, out scrapMessage))
                    {
                        return new AiActionExecutionResult
                        {
                            Success = false,
                            Message = scrapMessage,
                            BusinessRecordType = AiActionTypes.InventoryAdjust
                        };
                    }

                    item.MatchedProductName = product.Name;
                    item.ProductName = product.Name;
                    item.ProductSpec = NormalizeSpec(product.Specification);
                    item.Quantity = scrapQuantity;
                    NormalizeSpecAndUnit(item);
                    return new AiActionExecutionResult
                    {
                        Success = true,
                        Message = "已完成报废登记。",
                        BusinessRecordId = record.Id,
                        BusinessRecordType = AiActionTypes.InventoryAdjust
                    };
                }

                decimal actualStock = item.InventoryAdjustQuantity.GetValueOrDefault();
                InventoryCheck recordCheck = new InventoryCheck
                {
                    CheckDate = DateTime.Today,
                    Remark = string.IsNullOrWhiteSpace(item.Notes) ? "AI 动作草稿确认库存修正" : item.Notes.Trim()
                };

                decimal diff = actualStock - product.CurrentStock;
                recordCheck.Items.Add(new InventoryCheckItem
                {
                    ProductId = product.Id,
                    ProductNameSnapshot = product.Name,
                    CategoryName = product.CategoryName,
                    SystemStock = product.CurrentStock,
                    ActualStock = actualStock,
                    DifferenceQuantity = diff,
                    CostPriceSnapshot = product.AverageCost,
                    DifferenceAmount = diff * product.AverageCost,
                    Reason = ResolveInventoryAdjustReason(item),
                    Remark = string.IsNullOrWhiteSpace(item.Notes) ? "AI 库存修正" : item.Notes.Trim()
                });

                string checkMessage;
                if (!_inventoryCheckService.TrySave(recordCheck, out checkMessage))
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = checkMessage,
                        BusinessRecordType = AiActionTypes.InventoryAdjust
                    };
                }

                item.MatchedProductName = product.Name;
                item.ProductName = product.Name;
                item.ProductSpec = NormalizeSpec(product.Specification);
                item.InventoryAdjustQuantity = actualStock;
                NormalizeSpecAndUnit(item);
                return new AiActionExecutionResult
                {
                    Success = true,
                    Message = "已完成库存修正。",
                    BusinessRecordId = recordCheck.Id,
                    BusinessRecordType = AiActionTypes.InventoryAdjust
                };
            }
            catch (Exception ex)
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "执行库存动作失败：" + ex.Message,
                    BusinessRecordType = AiActionTypes.InventoryAdjust
                };
            }
        }

        private AiActionExecutionResult ExecuteProductPriceUpdate(AiActionDraftItem item)
        {
            try
            {
                Product product = item.MatchedProductId.HasValue ? _productService.GetById(item.MatchedProductId.Value) : null;
                if (product == null)
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = "无法执行改价：没有匹配到系统商品。",
                        BusinessRecordType = AiActionTypes.ProductPriceUpdate
                    };
                }

                decimal newPrice = item.PriceChangeNewValue.GetValueOrDefault();
                item.PriceChangeOldValue = product.DefaultPrice;
                product.DefaultPrice = newPrice;
                string productMessage;
                if (!_productService.TrySave(product, out productMessage))
                {
                    return new AiActionExecutionResult
                    {
                        Success = false,
                        Message = productMessage,
                        BusinessRecordType = AiActionTypes.ProductPriceUpdate
                    };
                }

                item.MatchedProductName = product.Name;
                item.ProductName = product.Name;
                item.ProductSpec = NormalizeSpec(product.Specification);
                item.PriceChangeNewValue = newPrice;
                NormalizeSpecAndUnit(item);
                return new AiActionExecutionResult
                {
                    Success = true,
                    Message = "已完成商品售价修改。",
                    BusinessRecordId = product.Id,
                    BusinessRecordType = AiActionTypes.ProductPriceUpdate
                };
            }
            catch (Exception ex)
            {
                return new AiActionExecutionResult
                {
                    Success = false,
                    Message = "执行商品改价失败：" + ex.Message,
                    BusinessRecordType = AiActionTypes.ProductPriceUpdate
                };
            }
        }

        private void ValidateItem(AiActionDraftItem item)
        {
            item.MissingFields.Clear();
            item.Warnings.Clear();

            MatchProduct(item);
            item.RiskLevel = ResolveRiskLevel(item.ActionType);

            if (item.Quantity.HasValue && item.Quantity.Value <= 0)
            {
                item.MissingFields.Add("quantity");
            }

            if (item.PurchasePrice.HasValue && item.PurchasePrice.Value < 0)
            {
                item.MissingFields.Add("purchasePrice");
            }

            if (item.SalePrice.HasValue && item.SalePrice.Value < 0)
            {
                item.MissingFields.Add("salePrice");
            }

            if (item.CreditAmount.HasValue && item.CreditAmount.Value < 0)
            {
                item.MissingFields.Add("creditAmount");
            }

            if (item.PriceChangeNewValue.HasValue && item.PriceChangeNewValue.Value < 0)
            {
                item.MissingFields.Add("priceChangeNewValue");
            }

            if (item.ShelfLifeDays.HasValue && item.ShelfLifeDays.Value > 0 && item.ProductionDate.HasValue && !item.ExpiryDate.HasValue)
            {
                item.ExpiryDate = item.ProductionDate.Value.AddDays(item.ShelfLifeDays.Value);
                item.ShelfLifeEnabled = true;
            }

            if (item.ShelfLifeDays.HasValue && item.ShelfLifeDays.Value > 0 && !item.ProductionDate.HasValue)
            {
                item.ShelfLifeEnabled = true;
                item.Warnings.Add("用户提供了保质期，但没有生产日期，暂时无法准确计算到期日期。");
            }

            if (item.ActionType == AiActionTypes.PurchaseIn)
            {
                ValidatePurchaseItem(item);
            }
            else if (item.ActionType == AiActionTypes.SaleRecord)
            {
                ValidateSaleItem(item);
            }
            else if (item.ActionType == AiActionTypes.InventoryAdjust)
            {
                ValidateInventoryAdjustItem(item);
            }
            else if (item.ActionType == AiActionTypes.CreditRegister)
            {
                if (string.IsNullOrWhiteSpace(item.ProductName))
                {
                    item.MissingFields.Add("productName");
                }

                if (!item.MatchedProductId.HasValue)
                {
                    item.MissingFields.Add("matchedProduct");
                }

                if (!item.Quantity.HasValue || item.Quantity.Value <= 0)
                {
                    item.MissingFields.Add("quantity");
                }

                if (!item.SalePrice.HasValue && item.MatchedProductId.HasValue)
                {
                    Product product = _productService.GetById(item.MatchedProductId.Value);
                    if (product != null)
                    {
                        item.SalePrice = product.DefaultPrice;
                        item.Warnings.Add("未说明售价，已使用商品当前售价 " + FormatMoney(product.DefaultPrice) + " 元。");
                    }
                }

                if (string.IsNullOrWhiteSpace(item.CustomerName))
                {
                    item.MissingFields.Add("customerName");
                }

                if (!item.CreditAmount.HasValue || item.CreditAmount.Value <= 0)
                {
                    item.MissingFields.Add("creditAmount");
                    item.Warnings.Add("未说明赊账金额，需要补充金额后才能执行写库。");
                }
            }
            else if (item.ActionType == AiActionTypes.ProductPriceUpdate)
            {
                if (string.IsNullOrWhiteSpace(item.ProductName))
                {
                    item.MissingFields.Add("productName");
                }

                if (!item.MatchedProductId.HasValue)
                {
                    item.MissingFields.Add("matchedProduct");
                }
                else if (!item.PriceChangeOldValue.HasValue)
                {
                    Product product = _productService.GetById(item.MatchedProductId.Value);
                    if (product != null)
                    {
                        item.PriceChangeOldValue = product.DefaultPrice;
                    }
                }

                if (!item.PriceChangeNewValue.HasValue)
                {
                    item.MissingFields.Add("priceChangeNewValue");
                }

                if (item.PriceChangeNewValue.HasValue)
                {
                    string name = string.IsNullOrWhiteSpace(item.MatchedProductName) ? item.ProductName : item.MatchedProductName;
                    string oldPrice = item.PriceChangeOldValue.HasValue ? FormatMoney(item.PriceChangeOldValue.Value) : "当前";
                    item.Warnings.Add("即将把【" + name + "】售价从 " + oldPrice + " 元改为 " + FormatMoney(item.PriceChangeNewValue.Value) + " 元，确认执行后会直接修改商品默认售价。");
                }
            }
            else if (item.ActionType == AiActionTypes.DeleteOrUndoRequest)
            {
                item.RiskLevel = AiActionRiskLevels.High;
                item.Warnings.Add("删除或撤销属于高风险动作，必须明确对象和影响范围，不能自动执行。");
            }
            else
            {
                item.MissingFields.Add("actionType");
                item.Warnings.Add("无法判断用户要执行哪类经营动作。");
            }

            item.MissingFields = item.MissingFields.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
            item.Warnings = item.Warnings.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        }

        private void ValidatePurchaseItem(AiActionDraftItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                item.MissingFields.Add("productName");
            }

            if (!item.Quantity.HasValue || item.Quantity.Value <= 0)
            {
                item.MissingFields.Add("quantity");
            }

            if (!item.PurchasePrice.HasValue)
            {
                if (item.MatchedProductId.HasValue)
                {
                    Product product = _productService.GetById(item.MatchedProductId.Value);
                    if (product != null && product.AverageCost > 0)
                    {
                        item.PurchasePrice = product.AverageCost;
                        item.Warnings.Add("未说明进货价，已先沿用当前库存均价 " + FormatMoney(product.AverageCost) + " 元。");
                    }
                }

                if (!item.PurchasePrice.HasValue)
                {
                    item.MissingFields.Add("purchasePrice");
                }
            }

            if (!item.MatchedProductId.HasValue)
            {
                item.IsNewProduct = true;
                if (string.IsNullOrWhiteSpace(item.Category))
                {
                    item.Category = InferCategoryName(item.ProductName);
                }

                if (ResolveCategory(item.Category) == null)
                {
                    item.MissingFields.Add("category");
                }
            }
            else if (!item.SalePrice.HasValue)
            {
                Product product = _productService.GetById(item.MatchedProductId.Value);
                if (product != null)
                {
                    item.SalePrice = product.DefaultPrice;
                    item.Warnings.Add("未说明建议售价，已保留当前售价 " + FormatMoney(product.DefaultPrice) + " 元。");
                }
            }

            if (!item.SalePrice.HasValue)
            {
                item.MissingFields.Add("salePrice");
                item.Warnings.Add("未说明建议售价，可以先留空，但建议补充后再上架。");
            }

            if (item.ShelfLifeEnabled.GetValueOrDefault(false) && !item.ProductionDate.HasValue && !item.ExpiryDate.HasValue)
            {
                item.Warnings.Add("已启用保质期，但未填写生产日期或到期日期。");
            }
        }

        private void ValidateSaleItem(AiActionDraftItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                item.MissingFields.Add("productName");
            }

            if (!item.MatchedProductId.HasValue)
            {
                item.MissingFields.Add("matchedProduct");
            }

            if (!item.Quantity.HasValue || item.Quantity.Value <= 0)
            {
                item.MissingFields.Add("quantity");
            }

            if (!item.SalePrice.HasValue && item.MatchedProductId.HasValue)
            {
                Product product = _productService.GetById(item.MatchedProductId.Value);
                if (product != null)
                {
                    item.SalePrice = product.DefaultPrice;
                    item.Warnings.Add("未说明售价，已使用商品当前售价 " + FormatMoney(product.DefaultPrice) + " 元。");
                }
            }

            if (item.MatchedProductId.HasValue && item.Quantity.HasValue)
            {
                Product product = _productService.GetById(item.MatchedProductId.Value);
                if (product != null && product.CurrentStock < item.Quantity.Value)
                {
                    item.MissingFields.Add("insufficientStock");
                    item.Warnings.Add("库存不足：当前库存 " + FormatNumber(product.CurrentStock)
                        + "，本次销售 " + FormatNumber(item.Quantity.Value) + "。");
                }
            }

            if (item.SalePrice.HasValue && item.Quantity.HasValue && item.Quantity.Value > 0)
            {
                decimal receivable = item.SalePrice.Value * item.Quantity.Value;
                decimal? discount = ExtractDiscountFromNotes(item.Notes);
                if (!item.ActualReceivedAmount.HasValue)
                {
                    item.ActualReceivedAmount = discount.HasValue
                        ? Math.Max(0, receivable - discount.Value)
                        : receivable;
                    item.Warnings.Add(discount.HasValue
                        ? "已按优惠 " + FormatMoney(discount.Value) + " 元计算实收金额。"
                        : "未说明实收金额，已按应收金额 " + FormatMoney(receivable) + " 元填写。");
                }
                else if (Math.Abs(item.ActualReceivedAmount.Value - receivable) >= 0.01M)
                {
                    item.Warnings.Add("实收金额和应收金额不一致，应收 " + FormatMoney(receivable)
                        + " 元，实收 " + FormatMoney(item.ActualReceivedAmount.Value) + " 元。");
                }
            }

            if (item.SalePrice.HasValue && item.MatchedProductId.HasValue)
            {
                Product product = _productService.GetById(item.MatchedProductId.Value);
                if (product != null && Math.Abs(product.DefaultPrice - item.SalePrice.Value) >= 0.01M)
                {
                    item.Warnings.Add("售价不是当前默认售价。当前默认 " + FormatMoney(product.DefaultPrice)
                        + " 元，本次 " + FormatMoney(item.SalePrice.Value) + " 元。");
                }
            }

            if (item.CandidateProductNames.Count > 1 && !string.IsNullOrWhiteSpace(item.MatchedProductName))
            {
                item.Warnings.Add("商品名称较模糊，已默认匹配为：" + item.MatchedProductName + "，请确认是否正确。");
            }

            if (!item.SalePrice.HasValue)
            {
                item.MissingFields.Add("salePrice");
            }
        }

        private void ValidateInventoryAdjustItem(AiActionDraftItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                item.MissingFields.Add("productName");
            }

            if (!item.MatchedProductId.HasValue)
            {
                item.MissingFields.Add("matchedProduct");
            }

            if (IsScrapIntent(item))
            {
                if (!item.Quantity.HasValue || item.Quantity.Value <= 0)
                {
                    item.MissingFields.Add("quantity");
                }

                item.Warnings.Add("报废会直接扣减库存，确认前请核对商品和数量。");
            }
            else if (!item.InventoryAdjustQuantity.HasValue)
            {
                item.MissingFields.Add("inventoryAdjustQuantity");
            }
            else
            {
                item.Warnings.Add("库存修正会直接调整当前库存，确认前请核对修正后数量。");
            }
        }

        private void MatchProduct(AiActionDraftItem item)
        {
            item.MatchedProductId = null;
            item.MatchedProductName = string.Empty;
            item.CandidateProductNames.Clear();
            if (string.IsNullOrWhiteSpace(item.ProductName))
            {
                return;
            }

            IList<Product> products = _productService.Search(item.ProductName, null, "在售");
            Product best = null;
            foreach (Product product in products)
            {
                if (product == null)
                {
                    continue;
                }

                if (item.CandidateProductNames.Count < 5)
                {
                    item.CandidateProductNames.Add(product.Name);
                }

                if (IsSameProduct(product, item))
                {
                    best = product;
                    break;
                }

                if (best == null)
                {
                    best = product;
                }
            }

            if (best != null)
            {
                item.MatchedProductId = best.Id;
                item.MatchedProductName = best.Name;
                if (string.IsNullOrWhiteSpace(item.Category))
                {
                    item.Category = best.CategoryName;
                }

                if (string.IsNullOrWhiteSpace(item.ProductSpec))
                {
                    item.ProductSpec = NormalizeSpec(best.Specification);
                }
            }
        }

        private bool IsSameProduct(Product product, AiActionDraftItem item)
        {
            string productName = NormalizeProductText(product.Name);
            string requestedName = NormalizeProductText(item.ProductName);
            string productSpec = NormalizeSpec(product.Specification);
            string requestedSpec = NormalizeSpec(item.ProductSpec);

            bool nameMatches = productName.Contains(requestedName)
                || requestedName.Contains(productName)
                || IsAliasMatch(productName, requestedName);
            bool specMatches = string.IsNullOrWhiteSpace(requestedSpec)
                || productSpec.Contains(requestedSpec)
                || requestedSpec.Contains(productSpec)
                || NormalizeProductText(product.Name).Contains(requestedSpec);
            return nameMatches && specMatches;
        }

        private static bool IsAliasMatch(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            return (left.Contains("可口可乐") && right.Contains("可乐"))
                || (right.Contains("可口可乐") && left.Contains("可乐"));
        }

        private void NormalizeItem(AiActionDraftItem item)
        {
            NormalizeSpecAndUnit(item);
            item.Category = CleanText(item.Category);
            item.CustomerName = CleanText(item.CustomerName);
        }

        public void NormalizeSpecAndUnit(AiActionDraftItem item)
        {
            if (item == null)
            {
                return;
            }

            string productName = CleanText(item.ProductName);
            string rawSpec = CleanText(item.ProductSpec);
            string rawUnit = CleanText(item.Unit);

            string unitFromName;
            productName = StripTrailingUnit(productName, out unitFromName);
            if (string.IsNullOrWhiteSpace(rawUnit) || rawUnit == "件")
            {
                rawUnit = unitFromName;
            }

            string unitFromSpec;
            rawSpec = StripTrailingUnit(rawSpec, out unitFromSpec);
            if (string.IsNullOrWhiteSpace(rawUnit) || rawUnit == "件")
            {
                rawUnit = unitFromSpec;
            }

            if (IsPieceUnit(rawSpec))
            {
                if (string.IsNullOrWhiteSpace(rawUnit) || rawUnit == "件")
                {
                    rawUnit = rawSpec;
                }

                rawSpec = string.Empty;
            }

            string productSpec = NormalizeSpec(rawSpec);
            if (string.IsNullOrWhiteSpace(productSpec))
            {
                productSpec = ExtractSpec(productName);
            }

            if (!string.IsNullOrWhiteSpace(productSpec))
            {
                productName = Regex.Replace(productName ?? string.Empty, Regex.Escape(productSpec), string.Empty, RegexOptions.IgnoreCase).Trim();
                productName = Regex.Replace(productName, @"\s{2,}", " ").Trim();
            }

            string extraUnit;
            productName = StripTrailingUnit(productName, out extraUnit);
            if (string.IsNullOrWhiteSpace(rawUnit) || rawUnit == "件")
            {
                rawUnit = extraUnit;
            }

            if (!string.IsNullOrWhiteSpace(rawUnit) && string.Equals(productSpec, rawUnit, StringComparison.OrdinalIgnoreCase))
            {
                productSpec = string.Empty;
            }

            item.ProductName = CleanText(productName);
            item.ProductSpec = productSpec;
            item.Unit = NormalizeUnit(rawUnit);
        }

        private static AiActionDraftItem ReadActionItem(Dictionary<string, object> action, int itemIndex)
        {
            AiActionDraftItem item = new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemIndex = itemIndex,
                ActionType = ReadString(action, "actionType"),
                ProductName = ReadString(action, "productName"),
                ProductSpec = ReadString(action, "productSpec"),
                Category = ReadString(action, "category"),
                Quantity = ReadDecimal(action, "quantity"),
                Unit = ReadString(action, "unit"),
                PurchasePrice = ReadDecimal(action, "purchasePrice"),
                SalePrice = ReadDecimal(action, "salePrice"),
                ProductionDate = ReadDate(action, "productionDate"),
                ExpiryDate = ReadDate(action, "expiryDate"),
                ShelfLifeEnabled = ReadNullableBool(action, "shelfLifeEnabled"),
                ShelfLifeDays = ReadNullableInt(action, "shelfLifeDays"),
                CustomerName = ReadString(action, "customerName"),
                CreditAmount = ReadDecimal(action, "creditAmount"),
                ActualReceivedAmount = ReadDecimal(action, "actualReceivedAmount"),
                InventoryAdjustQuantity = ReadDecimal(action, "inventoryAdjustQuantity"),
                PriceChangeOldValue = ReadDecimal(action, "priceChangeOldValue"),
                PriceChangeNewValue = ReadDecimal(action, "priceChangeNewValue"),
                Confidence = ReadDecimal(action, "confidence").GetValueOrDefault(0),
                Notes = ReadString(action, "notes")
            };

            AddStrings(item.MissingFields, action, "missingFields");
            AddStrings(item.Warnings, action, "warnings");
            return item;
        }

        private IList<AiActionDraftItem> ParseUndoItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "撤销", "撤回", "取消上一", "删掉刚才", "删除上一", "弄错了"))
            {
                return items;
            }

            items.Add(new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.DeleteOrUndoRequest,
                Confidence = 0.72M,
                Notes = "暂时没有找到可撤销的上一条 AI 操作。"
            });
            return items;
        }

        private IList<AiActionDraftItem> ParseCreditItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "赊", "欠", "没给钱", "没付钱", "记他", "记她", "先欠着", "拿了"))
            {
                return items;
            }

            Match productMatch = Regex.Match(source, BuildQuantityProductPattern());
            if (!productMatch.Success)
            {
                return items;
            }

            string customer = ExtractCreditCustomerName(source);
            string productName = CleanLocalActionProductName(productMatch.Groups["name"].Value);
            decimal? quantity = ParseSpokenCount(productMatch.Groups["count"].Value);
            string unit = productMatch.Groups["unit"].Value;
            decimal? creditAmount = ExtractPrice(source, @"(?:记他|记她|欠款|欠|赊账金额|金额)(?:是|为)?\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)|[一二三四五六七八九十两半]+(?:块[一二三四五六七八九十两]?|毛|元))");

            if (string.IsNullOrWhiteSpace(productName))
            {
                return items;
            }

            items.Add(new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.CreditRegister,
                CustomerName = customer,
                ProductName = productName,
                Quantity = quantity,
                Unit = unit,
                CreditAmount = creditAmount,
                ActualReceivedAmount = 0,
                Confidence = 0.72M,
                Notes = string.IsNullOrWhiteSpace(creditAmount.HasValue ? "x" : string.Empty)
                    ? "赊账登记"
                    : "赊账登记，用户说明欠款 " + FormatMoney(creditAmount.Value) + " 元"
            });
            return items;
        }

        private IList<AiActionDraftItem> ParseInventoryAdjustItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "盘点", "实际只剩", "实际剩", "库存改成", "修正库存", "只剩"))
            {
                return items;
            }

            string unitPattern = BuildUnitPattern();
            Match match = Regex.Match(source, @"(?<name>[\u4e00-\u9fa5A-Za-z0-9]{1,24}?)(?:实际只剩|实际剩|库存改成|库存修正为|修正为|只剩)\s*(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*(?<unit>" + unitPattern + @")?");
            if (!match.Success)
            {
                return items;
            }

            string productName = CleanLocalActionProductName(match.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(productName))
            {
                return items;
            }

            items.Add(new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.InventoryAdjust,
                ProductName = productName,
                InventoryAdjustQuantity = ParseSpokenCount(match.Groups["count"].Value),
                Unit = string.IsNullOrWhiteSpace(match.Groups["unit"].Value) ? "件" : match.Groups["unit"].Value,
                Confidence = 0.72M,
                Notes = "AI 盘点库存修正：" + source.Trim()
            });
            return items;
        }

        private IList<AiActionDraftItem> ParseScrapItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "报废", "扔掉", "丢掉", "坏了", "过期处理"))
            {
                return items;
            }

            Match match = Regex.Match(source, @"(?:报废|扔掉|丢掉|过期处理)\s*(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*(?<unit>" + BuildUnitPattern() + @")\s*(?<name>[\u4e00-\u9fa5A-Za-z0-9]{1,24})");
            if (!match.Success)
            {
                return items;
            }

            string productName = CleanLocalActionProductName(match.Groups["name"].Value);
            if (string.IsNullOrWhiteSpace(productName))
            {
                return items;
            }

            items.Add(new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.InventoryAdjust,
                ProductName = productName,
                Quantity = ParseSpokenCount(match.Groups["count"].Value),
                Unit = match.Groups["unit"].Value,
                Confidence = 0.76M,
                Notes = "报废：" + source.Trim()
            });
            return items;
        }

        private IList<AiActionDraftItem> ParsePriceUpdateItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "以后", "售价", "卖", "改价", "价格改", "调价", "调到", "改成"))
            {
                return items;
            }

            if (ContainsAny(source, "多少钱", "价格是多少", "售价是多少"))
            {
                return items;
            }

            Match match = Regex.Match(source, @"(?:以后|今后|从现在开始)?\s*(?<name>[\u4e00-\u9fa5A-Za-z0-9]{1,24}?)(?:售价|价格|卖价|卖|改成|调到|调整为)\s*(?<price>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
            if (!match.Success)
            {
                return items;
            }

            string productName = CleanLocalActionProductName(match.Groups["name"].Value);
            decimal? newPrice = ParseSpokenMoney(match.Groups["price"].Value);
            if (string.IsNullOrWhiteSpace(productName) || !newPrice.HasValue)
            {
                return items;
            }

            items.Add(new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.ProductPriceUpdate,
                ProductName = productName,
                PriceChangeNewValue = newPrice,
                Confidence = 0.74M,
                Notes = "AI 商品改价：" + source.Trim()
            });
            return items;
        }

        private IList<AiActionDraftItem> ParseSaleItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string source = text ?? string.Empty;
            if (!ContainsAny(source, "卖", "销售", "收了", "记账"))
            {
                return items;
            }

            decimal? explicitReceived = ExtractPrice(source, @"(?:实收|收款|收了|收)(?:是|为)?\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
            decimal? discount = ExtractPrice(source, @"(?:便宜了|优惠了|少收了)\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
            MatchCollection matches = Regex.Matches(source, @"(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*(?<unit>瓶|包|袋|条|件|个|箱|盒|支|根|听|罐|桶|杯|提|板)\s*(?<name>[\u4e00-\u9fa5A-Za-z0-9]{1,24})");
            foreach (Match match in matches)
            {
                string name = CleanLocalSaleName(match.Groups["name"].Value);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                AiActionDraftItem item = new AiActionDraftItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ActionType = AiActionTypes.SaleRecord,
                    ProductName = name,
                    Quantity = ParseSpokenCount(match.Groups["count"].Value),
                    Unit = match.Groups["unit"].Value,
                    Confidence = 0.58M,
                    Notes = string.Empty
                };
                if (matches.Count == 1 && explicitReceived.HasValue)
                {
                    item.ActualReceivedAmount = explicitReceived;
                }

                if (discount.HasValue)
                {
                    item.Notes = "熟人优惠 " + FormatMoney(discount.Value) + " 元";
                }

                items.Add(item);
            }

            return items;
        }

        private IList<AiActionDraftItem> ParsePurchaseItemsLocally(string text)
        {
            List<AiActionDraftItem> items = new List<AiActionDraftItem>();
            string normalized = text ?? string.Empty;
            string[] parts = Regex.Split(normalized, @"(?:然后我又|然后又|又进了|又买了|另外|还有)");
            foreach (string part in parts)
            {
                AiActionDraftItem item = ParseSinglePurchaseItemLocally(part);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private AiActionDraftItem ParseSinglePurchaseItemLocally(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !ContainsAny(text, "进", "入库", "采购", "买", "补"))
            {
                return null;
            }

            AiActionDraftItem item = new AiActionDraftItem
            {
                Id = Guid.NewGuid().ToString("N"),
                ActionType = AiActionTypes.PurchaseIn,
                Confidence = 0.55M
            };

            string spec = ExtractSpec(text);
            item.ProductSpec = spec;
            item.ProductName = ExtractLocalProductName(text);
            item.Unit = ExtractUnit(text);
            decimal? quantity = ExtractQuantity(text);
            item.Quantity = quantity;
            ApplyPackageQuantity(text, item);
            item.PurchasePrice = ExtractPrice(text, @"(?:进货价|进价|成本价|成本|采购价)(?:每瓶|每包|每件|每个|一瓶|一包|单价)?(?:是|为)?\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
            item.SalePrice = ExtractPrice(text, @"(?:建议的售价|建议售价|售价|卖价|零售价|卖)(?:每瓶|每包|每件|每个|一瓶|一包|单价)?(?:是|为)?\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
            ApplyDates(text, item);
            if (ContainsAny(text, "保质期"))
            {
                item.ShelfLifeEnabled = true;
                int? days = ExtractShelfLifeDays(text);
                if (days.HasValue)
                {
                    item.ShelfLifeDays = days;
                }
            }

            return item;
        }

        private static void ApplyPackageQuantity(string text, AiActionDraftItem item)
        {
            Match packageMatch = Regex.Match(text ?? string.Empty, @"(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*箱");
            Match perPackageMatch = Regex.Match(text ?? string.Empty, @"(?:一箱|每箱|1箱)\s*(?<count>\d+(?:\.\d+)?)\s*(?<unit>瓶|包|袋|条|件|个|箱|盒|支|根|听|罐|桶|杯|提|板)");
            if (packageMatch.Success && perPackageMatch.Success)
            {
                decimal boxes = ParseSpokenCount(packageMatch.Groups["count"].Value).GetValueOrDefault();
                decimal perBox = ParseDecimal(perPackageMatch.Groups["count"].Value).GetValueOrDefault();
                if (boxes > 0 && perBox > 0)
                {
                    item.Quantity = boxes * perBox;
                    item.Unit = perPackageMatch.Groups["unit"].Value;
                    item.Warnings.Add("已按箱规换算：" + FormatNumber(boxes) + " 箱 × 每箱 " + FormatNumber(perBox) + " " + item.Unit + "。");
                }
            }
        }

        private static decimal? ExtractQuantity(string text)
        {
            foreach (string unit in PieceUnits)
            {
                Match match = Regex.Match(text ?? string.Empty, @"(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*" + Regex.Escape(unit));
                if (match.Success)
                {
                    return ParseSpokenCount(match.Groups["count"].Value);
                }
            }

            return null;
        }

        private static string ExtractUnit(string text)
        {
            foreach (string unit in PieceUnits)
            {
                if ((text ?? string.Empty).Contains(unit))
                {
                    return unit;
                }
            }

            return "件";
        }

        private static string ExtractLocalProductName(string text)
        {
            string value = MatchValue(text, @"(?:\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*箱\s*(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,24})");
            if (string.IsNullOrWhiteSpace(value))
            {
                value = MatchValue(text, @"\d+(?:\.\d+)?\s*(?:瓶|包|袋|条|件|个|箱|盒|支|根|听|罐|桶|杯|提|板)\s*(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,24})");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = MatchValue(text, @"(?:把)?(?<value>[\u4e00-\u9fa5A-Za-z]{1,24}?)(?:补|进|入库)?\s*\d+(?:\.\d+)?\s*(?:瓶|包|袋|条|件|个|箱|盒|支|根|听|罐|桶|杯|提|板)");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = MatchValue(text, @"(?<value>[\u4e00-\u9fa5A-Za-z]{1,24})\s*(?:，|,|的)?\s*\d+(?:\.\d+)?\s*(?:ml|毫升|升|g|克|kg|千克)?");
            }

            value = Regex.Replace(value ?? string.Empty, @"(这次|今天|新进|进了|我进了|帮我|登记|一下|又|然后|入库|补|货)", string.Empty);
            value = value.Replace("把", string.Empty);
            value = Regex.Replace(value, @"\d+(?:\.\d+)?\s*(?:ml|毫升|升|g|克|kg|千克)", string.Empty, RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\d+(?:\.\d+)?\s*(?:瓶|包|袋|条|件|个|箱|盒|支|根|听|罐|桶|杯|提|板)", string.Empty);
            value = Regex.Replace(value, @"(一箱|每箱|两箱|二箱)", string.Empty);
            value = Regex.Replace(value, @"(进货价|进价|售价|卖价|建议售价|每瓶|每包|一包|一瓶).*", string.Empty);
            return CleanText(value);
        }

        private static decimal? ExtractPrice(string text, string pattern)
        {
            string value = MatchValue(text, pattern);
            return ParseSpokenMoney(value);
        }

        private static void ApplyDates(string text, AiActionDraftItem item)
        {
            DateTime? productionDate = ParseDate(MatchValue(text, @"(?:生产日期|生产日|生产时间)(?:是|为)?\s*(?<value>\d{4}(?:[-/年.]\d{1,2})(?:[-/月.]\d{1,2})日?)"));
            DateTime? expiryDate = ParseDate(MatchValue(text, @"(?:到期日期|到期日|过期日期|过期日|保质期到|有效期到)(?:是|为)?\s*(?<value>\d{4}(?:[-/年.]\d{1,2})(?:[-/月.]\d{1,2})日?)"));
            if (productionDate.HasValue)
            {
                item.ProductionDate = productionDate;
                item.ShelfLifeEnabled = true;
            }

            if (expiryDate.HasValue)
            {
                item.ExpiryDate = expiryDate;
                item.ShelfLifeEnabled = true;
            }
        }

        private static int? ExtractShelfLifeDays(string text)
        {
            Match monthMatch = Regex.Match(text ?? string.Empty, @"保质期\s*(?<count>\d+)\s*个?月");
            if (monthMatch.Success)
            {
                return int.Parse(monthMatch.Groups["count"].Value) * 30;
            }

            Match yearMatch = Regex.Match(text ?? string.Empty, @"保质期\s*(?<count>\d+)\s*年");
            if (yearMatch.Success)
            {
                return int.Parse(yearMatch.Groups["count"].Value) * 365;
            }

            Match dayMatch = Regex.Match(text ?? string.Empty, @"保质期\s*(?<count>\d+)\s*天");
            if (dayMatch.Success)
            {
                return int.Parse(dayMatch.Groups["count"].Value);
            }

            return null;
        }

        private Category ResolveCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return null;
            }

            foreach (Category category in _categoryService.GetActiveCategories())
            {
                if (category.Name.IndexOf(categoryName.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
                    || categoryName.IndexOf(category.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return category;
                }
            }

            return null;
        }

        private string InferCategoryName(string productName)
        {
            string text = (productName ?? string.Empty).ToLowerInvariant();
            string category = string.Empty;
            if (ContainsAny(text, "可乐", "雪碧", "饮料", "矿泉水", "啤酒", "牛奶", "茶", "果汁", "水"))
            {
                category = "饮料";
            }
            else if (ContainsAny(text, "烟", "香烟"))
            {
                category = "香烟";
            }
            else if (ContainsAny(text, "薯片", "辣条", "面包", "饼干", "零食", "糖", "方便面"))
            {
                category = "零食";
            }

            return ResolveCategory(category) == null ? string.Empty : category;
        }

        private static string ResolveDraftStatus(AiActionDraft draft)
        {
            if (draft.Items.Count == 0)
            {
                return AiActionDraftStatus.Pending;
            }

            bool anyPending = draft.Items.Any(item => item.Status != AiActionDraftStatus.Executed && item.Status != AiActionDraftStatus.Cancelled);
            bool anyFailed = draft.Items.Any(item => item.Status == AiActionDraftStatus.Failed);
            if (anyFailed)
            {
                return AiActionDraftStatus.Failed;
            }

            return anyPending ? AiActionDraftStatus.Pending : AiActionDraftStatus.Executed;
        }

        private static string BuildSaleRemark(AiActionDraftItem item, decimal expectedAmount, decimal paidAmount)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                parts.Add(item.Notes.Trim());
            }

            if (Math.Abs(expectedAmount - paidAmount) >= 0.01M)
            {
                parts.Add("AI 销售记账：应收 " + FormatMoney(expectedAmount) + " 元，实收 " + FormatMoney(paidAmount) + " 元");
            }
            else
            {
                parts.Add("AI 动作草稿确认销售");
            }

            return string.Join("；", parts.Distinct().ToArray());
        }

        private static string BuildCreditRemark(AiActionDraftItem item, decimal expectedAmount, decimal paidAmount)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.Notes))
            {
                parts.Add(item.Notes.Trim());
            }

            parts.Add("AI 赊账登记：应收 " + FormatMoney(expectedAmount) + " 元，实收 " + FormatMoney(paidAmount) + " 元。");
            return string.Join("；", parts.Distinct().ToArray());
        }

        private static bool IsScrapIntent(AiActionDraftItem item)
        {
            if (item == null)
            {
                return false;
            }

            string text = ((item.Notes ?? string.Empty) + " " + (item.ProductName ?? string.Empty)).Trim();
            return ContainsAny(text, "报废", "过期", "临期处理", "损坏", "破损", "丢失", "自用", "赠送", "坏了", "扔掉");
        }

        private static decimal ResolveScrapQuantity(AiActionDraftItem item, Product product)
        {
            decimal quantity = item.Quantity.GetValueOrDefault();
            if (quantity <= 0 && item.InventoryAdjustQuantity.HasValue && product != null)
            {
                quantity = product.CurrentStock - item.InventoryAdjustQuantity.Value;
            }

            return quantity;
        }

        private static string ResolveScrapReason(AiActionDraftItem item)
        {
            string notes = item == null ? string.Empty : item.Notes ?? string.Empty;
            if (ContainsAny(notes, "过期", "临期"))
            {
                return "过期";
            }

            if (ContainsAny(notes, "破损", "损坏", "坏了"))
            {
                return "破损";
            }

            if (ContainsAny(notes, "自用", "赠送", "丢失"))
            {
                return "其他";
            }

            return "其他";
        }

        private static string ResolveInventoryAdjustReason(AiActionDraftItem item)
        {
            string notes = item == null ? string.Empty : item.Notes ?? string.Empty;
            if (ContainsAny(notes, "盘点", "实盘"))
            {
                return "盘点差异";
            }

            if (ContainsAny(notes, "录错", "修正", "改成"))
            {
                return "录入修正";
            }

            return "AI 库存修正";
        }

        private static decimal? ExtractDiscountFromNotes(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            return ExtractPrice(notes, @"(?:优惠|便宜|少收)\s*(?<value>\d+(?:\.\d+)?(?:块\d+|毛|块|元)?|[一二三四五六七八九十两半块毛角]+)");
        }

        private static string CleanLocalSaleName(string name)
        {
            string value = CleanText(name);
            value = Regex.Replace(value, @"(，|,|。|；|;|、).*", string.Empty);
            value = Regex.Replace(value, @"(都是|按标价|原价|收了|实收|收款|帮我|记账|记一下|卖的|一共|总共).*", string.Empty);
            value = Regex.Replace(value, @"^\s*(了|卖了|销售)\s*", string.Empty);
            return CleanText(value);
        }

        private static string BuildUnitPattern()
        {
            return string.Join("|", PieceUnits.Select(Regex.Escape).ToArray());
        }

        private static string BuildQuantityProductPattern()
        {
            return @"(?<count>\d+(?:\.\d+)?|[一二三四五六七八九十两]+)\s*(?<unit>" + BuildUnitPattern() + @")\s*(?<name>[\u4e00-\u9fa5A-Za-z0-9]{1,24})";
        }

        private static string ExtractCreditCustomerName(string text)
        {
            string source = text ?? string.Empty;
            string customer = MatchValue(source, @"^(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,12})(?:今天|刚才|又|这次)?(?:拿了|赊了|赊|欠了|欠)");
            if (string.IsNullOrWhiteSpace(customer))
            {
                customer = MatchValue(source, @"(?<value>[\u4e00-\u9fa5A-Za-z0-9]{1,12})(?:没给钱|没付钱|先欠着|记他|记她)");
            }

            customer = Regex.Replace(customer ?? string.Empty, @"(今天|刚才|又|这次|拿了|赊了|赊|欠了|欠|没给钱|没付钱|先欠着|记他|记她)", string.Empty);
            return CleanText(customer);
        }

        private static string CleanLocalActionProductName(string name)
        {
            string value = CleanText(name);
            value = Regex.Replace(value, @"(，|,|。|；|;|、).*", string.Empty);
            value = Regex.Replace(value, @"(没给钱|没付钱|先欠着|记他|记她|赊账|欠账|帮我|记一下|登记|盘点|了一下|实际只剩|实际剩|库存改成|修正库存|报废|扔掉|丢掉|坏了|过期处理|以后|今后|售价|价格|卖价|改成|调到|调整为).*", string.Empty);
            value = Regex.Replace(value, @"^(今天|刚才|这次|又|我|帮我|给我|把|了|一下|盘点)\s*", string.Empty);
            value = Regex.Replace(value, @"\d+(?:\.\d+)?\s*(?:ml|ML|mL|毫升|l|L|升|g|G|克|kg|KG|千克|斤|两)", string.Empty, RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"\d+(?:\.\d+)?\s*(?:" + BuildUnitPattern() + ")", string.Empty);
            return CleanText(value);
        }

        public string ToFieldDisplayName(string field)
        {
            string normalized = NormalizeFieldName(field);
            if (normalized == "productName")
            {
                return "商品名为空";
            }

            if (normalized == "quantity")
            {
                return "数量为空或小于等于 0";
            }

            if (normalized == "matchedProduct")
            {
                return "销售商品无法匹配到系统商品";
            }

            if (normalized == "insufficientStock")
            {
                return "库存不足";
            }

            if (normalized == "purchasePrice")
            {
                return "缺少进货价";
            }

            if (normalized == "salePrice")
            {
                return "缺少售价";
            }

            if (normalized == "category")
            {
                return "缺少商品分类";
            }

            if (normalized == "customerName")
            {
                return "缺少客户名";
            }

            if (normalized == "creditAmount")
            {
                return "缺少赊账金额";
            }

            if (normalized == "inventoryAdjustQuantity")
            {
                return "缺少实际库存";
            }

            if (normalized == "priceChangeNewValue")
            {
                return "缺少新价格";
            }

            if (normalized == "actionType")
            {
                return "无法确定要执行的动作";
            }

            return string.IsNullOrWhiteSpace(field) ? "字段不完整" : field;
        }

        private static string ResolveRiskLevel(string actionType)
        {
            actionType = NormalizeActionType(actionType);
            if (actionType == AiActionTypes.DeleteOrUndoRequest || actionType == AiActionTypes.ProductPriceUpdate)
            {
                return AiActionRiskLevels.High;
            }

            if (actionType == AiActionTypes.InventoryAdjust || actionType == AiActionTypes.SaleRecord || actionType == AiActionTypes.CreditRegister)
            {
                return AiActionRiskLevels.Medium;
            }

            return AiActionRiskLevels.Low;
        }

        private static string MaxRisk(string left, string right)
        {
            return RiskRank(right) > RiskRank(left) ? right : left;
        }

        private static int RiskRank(string risk)
        {
            if (risk == AiActionRiskLevels.High)
            {
                return 3;
            }

            if (risk == AiActionRiskLevels.Medium)
            {
                return 2;
            }

            return 1;
        }

        private static string NormalizeActionType(string actionType)
        {
            string value = (actionType ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "purchase" || value == "purchase_in" || value == "入库" || value == "进货")
            {
                return AiActionTypes.PurchaseIn;
            }

            if (value == "sale" || value == "sale_record" || value == "销售")
            {
                return AiActionTypes.SaleRecord;
            }

            if (value == "inventory" || value == "inventory_adjust" || value == "盘点")
            {
                return AiActionTypes.InventoryAdjust;
            }

            if (value == "credit" || value == "credit_register" || value == "赊账")
            {
                return AiActionTypes.CreditRegister;
            }

            if (value == "price" || value == "product_price_update" || value == "改价")
            {
                return AiActionTypes.ProductPriceUpdate;
            }

            if (value == "delete" || value == "undo" || value == "delete_or_undo_request" || value == "撤销")
            {
                return AiActionTypes.DeleteOrUndoRequest;
            }

            return AiActionTypes.Unknown;
        }

        private static string NormalizeFieldName(string field)
        {
            string value = (field ?? string.Empty).Trim();
            if (value == "商品名称" || value == "商品" || value == "product")
            {
                return "productName";
            }

            if (value == "数量")
            {
                return "quantity";
            }

            if (value == "进货价" || value == "成本价")
            {
                return "purchasePrice";
            }

            if (value == "售价" || value == "建议售价")
            {
                return "salePrice";
            }

            if (value == "分类")
            {
                return "category";
            }

            return value;
        }

        private static string NormalizeUnit(string unit)
        {
            unit = CleanText(unit);
            return string.IsNullOrWhiteSpace(unit) ? "件" : unit;
        }

        private static bool IsPieceUnit(string value)
        {
            return PieceUnits.Any(unit => string.Equals(unit, CleanText(value), StringComparison.OrdinalIgnoreCase));
        }

        private static string StripTrailingUnit(string text, out string unit)
        {
            unit = string.Empty;
            string value = CleanText(text);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string unitPattern = string.Join("|", PieceUnits.Select(Regex.Escape).ToArray());
            Match separated = Regex.Match(value, @"^(?<name>.+?)[\s　]+(?<unit>" + unitPattern + @")$");
            if (separated.Success)
            {
                unit = separated.Groups["unit"].Value;
                return CleanText(separated.Groups["name"].Value);
            }

            Match afterSpec = Regex.Match(value, @"^(?<name>.+?(?:\d+(?:\.\d+)?\s*(?:ml|ML|mL|毫升|l|L|升|g|G|克|kg|KG|千克|斤|两)|\d+\s*(?:片装|支装|瓶装|袋装|包装)))[\s　]*(?<unit>" + unitPattern + @")$");
            if (afterSpec.Success)
            {
                unit = afterSpec.Groups["unit"].Value;
                return CleanText(afterSpec.Groups["name"].Value);
            }

            return value;
        }

        private static string NormalizeSpec(string spec)
        {
            spec = CleanText(spec).Replace("毫升", "ml").Replace("升", "L").Replace("克", "g").Replace("千克", "kg");
            if (IsPieceUnit(spec))
            {
                return string.Empty;
            }

            Match literMatch = Regex.Match(spec, @"(?<value>\d+(?:\.\d+)?)\s*L", RegexOptions.IgnoreCase);
            if (literMatch.Success)
            {
                decimal value = ParseDecimal(literMatch.Groups["value"].Value).GetValueOrDefault();
                if (value > 0)
                {
                    return (value * 1000).ToString("0.###") + "ml";
                }
            }

            return spec.Replace(" ", string.Empty);
        }

        private static string ExtractSpec(string text)
        {
            Match match = Regex.Match(text ?? string.Empty, @"(?<value>\d+(?:\.\d+)?\s*(?:ml|ML|mL|毫升|l|L|升|g|G|克|kg|KG|千克|斤|两)|\d+\s*(?:片装|支装|瓶装|袋装|包装))");
            return match.Success ? NormalizeSpec(match.Groups["value"].Value) : string.Empty;
        }

        private static string NormalizeProductText(string text)
        {
            string unit;
            return NormalizeSpec(StripTrailingUnit(CleanText(text), out unit)).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static string BuildProductDisplayName(AiActionDraftItem item)
        {
            string name = item == null ? string.Empty : CleanText(item.ProductName);
            string trailingUnit;
            name = StripTrailingUnit(name, out trailingUnit);
            string spec = item == null ? string.Empty : NormalizeSpec(item.ProductSpec);
            if (IsPieceUnit(spec) || (!string.IsNullOrWhiteSpace(item == null ? string.Empty : item.Unit) && string.Equals(spec, item.Unit, StringComparison.OrdinalIgnoreCase)))
            {
                spec = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(spec) && name.IndexOf(spec, StringComparison.OrdinalIgnoreCase) < 0)
            {
                name = (name + " " + spec).Trim();
            }

            return name;
        }

        private static string CleanText(string text)
        {
            return (text ?? string.Empty).Trim().Trim('，', ',', '。', '.', '；', ';', '：', ':');
        }

        private static string ExtractJson(string text)
        {
            string value = (text ?? string.Empty).Trim();
            Match fence = Regex.Match(value, "```(?:json)?\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
            if (fence.Success)
            {
                value = fence.Groups["json"].Value.Trim();
            }

            int start = value.IndexOf('{');
            int end = value.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                value = value.Substring(start, end - start + 1);
            }

            return value;
        }

        private static void AddStrings(IList<string> target, Dictionary<string, object> source, string key)
        {
            if (target == null || source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return;
            }

            object[] values = source[key] as object[];
            if (values == null)
            {
                string single = source[key].ToString();
                if (!string.IsNullOrWhiteSpace(single))
                {
                    target.Add(single);
                }

                return;
            }

            foreach (object value in values)
            {
                if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                {
                    target.Add(value.ToString());
                }
            }
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return string.Empty;
            }

            return source[key].ToString();
        }

        private static decimal? ReadDecimal(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return null;
            }

            return ParseDecimal(source[key].ToString());
        }

        private static bool ReadBool(Dictionary<string, object> source, string key)
        {
            bool? value = ReadNullableBool(source, key);
            return value.GetValueOrDefault(false);
        }

        private static bool? ReadNullableBool(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return null;
            }

            bool value;
            if (bool.TryParse(source[key].ToString(), out value))
            {
                return value;
            }

            return null;
        }

        private static int? ReadNullableInt(Dictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null)
            {
                return null;
            }

            int value;
            if (int.TryParse(source[key].ToString(), out value))
            {
                return value;
            }

            return null;
        }

        private static DateTime? ReadDate(Dictionary<string, object> source, string key)
        {
            return ParseDate(ReadString(source, key));
        }

        private static DateTime? ParseDate(string text)
        {
            string normalized = (text ?? string.Empty).Trim()
                .Replace("年", "-")
                .Replace("月", "-")
                .Replace("日", string.Empty)
                .Replace("/", "-")
                .Replace(".", "-");
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            string[] formats = { "yyyy-M-d", "yyyy-MM-dd", "yyyy-M-dd", "yyyy-MM-d" };
            DateTime value;
            if (DateTime.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)
                || DateTime.TryParse(normalized, out value))
            {
                return value.Date;
            }

            return null;
        }

        private static decimal? ParseDecimal(string text)
        {
            decimal value;
            return decimal.TryParse((text ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : (decimal?)null;
        }

        private static decimal? ParseSpokenMoney(string text)
        {
            text = (text ?? string.Empty).Trim();
            decimal? numeric = ParseDecimal(text);
            if (numeric.HasValue)
            {
                return numeric;
            }

            text = text.Replace("元", "块").Replace("角", "毛");
            Match digitHalfMatch = Regex.Match(text, @"(?<whole>\d+)块(?<fraction>\d+)");
            if (digitHalfMatch.Success)
            {
                decimal whole = ParseDecimal(digitHalfMatch.Groups["whole"].Value).GetValueOrDefault();
                decimal fraction = ParseDecimal(digitHalfMatch.Groups["fraction"].Value).GetValueOrDefault();
                return whole + fraction / 10M;
            }

            if (text.Contains("毛"))
            {
                string left = text.Replace("毛", string.Empty);
                decimal? value = ParseChineseNumber(left);
                return value.HasValue ? value.Value / 10M : (decimal?)null;
            }

            Match halfMatch = Regex.Match(text, @"(?<whole>[一二三四五六七八九十两\d]+)?块半");
            if (halfMatch.Success)
            {
                decimal whole = ParseChineseNumber(halfMatch.Groups["whole"].Value).GetValueOrDefault(1);
                return whole + 0.5M;
            }

            Match oneHalfMatch = Regex.Match(text, @"(?<whole>[一二三四五六七八九十两\d]+)块(?<fraction>[一二三四五六七八九十两\d])");
            if (oneHalfMatch.Success)
            {
                decimal whole = ParseChineseNumber(oneHalfMatch.Groups["whole"].Value).GetValueOrDefault();
                decimal fraction = ParseChineseNumber(oneHalfMatch.Groups["fraction"].Value).GetValueOrDefault();
                return whole + fraction / 10M;
            }

            return ParseChineseNumber(text.Replace("块", string.Empty));
        }

        private static decimal? ParseSpokenCount(string text)
        {
            decimal? numeric = ParseDecimal(text);
            return numeric.HasValue ? numeric : ParseChineseNumber(text);
        }

        private static decimal? ParseChineseNumber(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            decimal numeric;
            if (decimal.TryParse(text, out numeric))
            {
                return numeric;
            }

            text = text.Replace("两", "二");
            Dictionary<char, int> digits = new Dictionary<char, int>
            {
                { '零', 0 }, { '一', 1 }, { '二', 2 }, { '三', 3 }, { '四', 4 },
                { '五', 5 }, { '六', 6 }, { '七', 7 }, { '八', 8 }, { '九', 9 }
            };

            if (text == "十")
            {
                return 10;
            }

            int tenIndex = text.IndexOf('十');
            if (tenIndex >= 0)
            {
                int tens = tenIndex == 0 ? 1 : (digits.ContainsKey(text[0]) ? digits[text[0]] : 0);
                int ones = tenIndex < text.Length - 1 && digits.ContainsKey(text[text.Length - 1]) ? digits[text[text.Length - 1]] : 0;
                return tens * 10 + ones;
            }

            if (text.Length == 1 && digits.ContainsKey(text[0]))
            {
                return digits[text[0]];
            }

            return null;
        }

        private static string MatchValue(string text, string pattern)
        {
            Match match = Regex.Match(text ?? string.Empty, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["value"].Value.Trim() : string.Empty;
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
