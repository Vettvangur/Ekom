using System;

namespace Ekom.Models
{
    public class SearchResultEntity
    {
        public string Name { get; set; }
        public int Id { get; set; }
        public Guid Key { get; set; }
        public float Score { get; set; }
        public string Path { get; set; }
        public string DocType { get; set; }
        public string ParentName { get; set; }
        public string SKU { get; set; }
        public string Url { get; set; }
    }
}
