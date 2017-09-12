using Examine;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Cache
{
	class ProductCache : PerStoreCache<Product>
	{
		public override string NodeAlias { get; } = "uwbsProduct";

		protected override Product New(SearchResult r, Store store)
		{
			return new Product(r, store);
		}

		/// <summary>
		/// ctor
		/// </summary>
		/// <param name="logFac"></param>
		/// <param name="config"></param>
		/// <param name="examineManager"></param>
		/// <param name="storeCache"></param>
		public ProductCache(
			ILogFactory logFac,
			Configuration config,
			ExamineManager examineManager,
			IBaseCache<Store> storeCache
		)
		{
			_config = config;
			_examineManager = examineManager;
			_storeCache = storeCache;

			_log = logFac.GetLogger(typeof(ProductCache));
		}
	}
}
