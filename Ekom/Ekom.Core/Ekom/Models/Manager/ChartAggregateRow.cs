namespace Ekom.Models.Manager;

public class ChartAggregateRow
{
    public DateTime BucketDate { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public decimal AverageAmount { get; set; }
}
