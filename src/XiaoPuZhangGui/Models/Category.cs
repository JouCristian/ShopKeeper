using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class Category
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string StatusText
        {
            get { return IsActive ? "在售" : "停用"; }
        }
    }
}
