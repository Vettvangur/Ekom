using Ekom.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace Ekom.Utilities
{
    public static class ArrayExtensions
    {
        public static IEnumerable<IPublishedContent> GetImageNodes(this Guid[] imageIds)
        {
            return NodeHelper.GetMediaNodesByGuid(imageIds);
        }
    }
}
