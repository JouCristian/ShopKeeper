using System;

namespace XiaoPuZhangGui.Models
{
    internal sealed class Category
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
