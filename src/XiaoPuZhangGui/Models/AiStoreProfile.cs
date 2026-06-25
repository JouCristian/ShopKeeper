using System;
using System.Text;

namespace XiaoPuZhangGui.Models
{
    internal sealed class AiStoreProfile
    {
        public bool IsInitialized { get; set; }

        public string StoreName { get; set; }

        public string StoreLocation { get; set; }

        public string BusinessType { get; set; }

        public string MainCustomers { get; set; }

        public string MainProducts { get; set; }

        public string OpeningHours { get; set; }

        public string OwnerPreference { get; set; }

        public string PricingStyle { get; set; }

        public string RestockPreference { get; set; }

        public string CreditPolicy { get; set; }

        public string Notes { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string ToPromptText()
        {
            if (!IsInitialized)
            {
                return "店铺基础背景：用户暂未填写店铺记忆。";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("【店铺基础背景】");
            Append(builder, "店铺名称", StoreName);
            Append(builder, "大概位置", StoreLocation);
            Append(builder, "经营类型", BusinessType);
            Append(builder, "主要顾客", MainCustomers);
            Append(builder, "主要商品", MainProducts);
            Append(builder, "营业时间", OpeningHours);
            Append(builder, "店主关注点", OwnerPreference);
            Append(builder, "定价风格", PricingStyle);
            Append(builder, "补货偏好", RestockPreference);
            Append(builder, "赊账规则", CreditPolicy);
            Append(builder, "其他长期记忆", Notes);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                builder.AppendLine(label + "：" + value.Trim());
            }
        }
    }
}
