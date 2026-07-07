namespace Ekom.Models.Manager;

public class MostSoldProductListData
{
    public IEnumerable<MostSoldProduct> Products { get; set; } = Array.Empty<MostSoldProduct>();
    public int Count { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalPages => (Count + PageSize - 1) / PageSize;
}
