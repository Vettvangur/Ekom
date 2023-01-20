using System.Collections.Generic;

namespace Ekom.Models
{
    public class MetafieldComparer : IEqualityComparer<Metafield>
    {

        public bool Equals(Metafield x, Metafield y)
        {
            return x.Key == y.Key;
        }

        public int GetHashCode(Metafield obj)
        {
            return obj.Key.GetHashCode() ^
                obj.Key.GetHashCode();
        }
    }
}
