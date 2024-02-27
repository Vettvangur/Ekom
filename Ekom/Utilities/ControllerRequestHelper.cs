using Ekom.Models;
using Microsoft.AspNetCore.Http;

namespace Ekom.Utilities
{
    public class ControllerRequestHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ControllerRequestHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public void SetEkmRequest(ICategory category) {

            var categoryUrl = category.Url;

            if (_httpContextAccessor.HttpContext != null)
            {
                Lazy<ContentRequest> requestCache = _httpContextAccessor.HttpContext.Items["ekmRequest"] as Lazy<ContentRequest>;

                // If requestCache is null, initialize it with a new Lazy<ContentRequest>
                if (requestCache == null)
                {
                    requestCache = new Lazy<ContentRequest>(() => new ContentRequest());
                    _httpContextAccessor.HttpContext.Items["ekmRequest"] = requestCache;
                }

                requestCache.Value.Category = category;
                requestCache.Value.Store = category.Store;
                requestCache.Value.Url = categoryUrl;
            }
        }
    }
}
