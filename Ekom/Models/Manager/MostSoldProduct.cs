namespace Ekom.Models.Manager
{
    public class MostSoldProduct
    {
        public string SKU { get; set; }
        public string Title { get; set; }
        public Guid Key { get; set; }
        public int Id { get; set; }
        public int ProductCount { get; set; }
    }
}
