using System;
using System.Collections.Generic;
using Umbraco.Core.Models.PublishedContent;

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
