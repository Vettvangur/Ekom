namespace Ekom.Models
{
    public class MetafieldComparer : IEqualityComparer<MetafieldSlim>
    {

        public bool Equals(MetafieldSlim x, MetafieldSlim y)
        {
            return x.Key == y.Key;
        }

        public int GetHashCode(MetafieldSlim obj)
        {
            return obj.Key.GetHashCode() ^
                obj.Key.GetHashCode();
        }
    }
}
