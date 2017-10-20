using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ekom.Models
{
    public class OrderedVariantGroup
    {
        private Variant variant;
        private Store store;
        private StoreInfo storeInfo;

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }

        public IEnumerable<OrderedVariant> Variants { get; set; }

        public OrderedVariantGroup(Variant variant, VariantGroup variantGroup, Store store)
        {
            this.variant = variant;
            this.store = store;

            Id = variantGroup.Id;
            Key = variantGroup.Key;
            Title = variantGroup.Title;

            var variants = new List<OrderedVariant>();

            variants.Add(new OrderedVariant(variant, store));

            Variants = variants;
        }

        public OrderedVariantGroup(JToken variantGroupObject, StoreInfo storeInfo)
        {
            Log.Info("Creating OrderedVariantGroup from Json");

            this.storeInfo = storeInfo;

            Id = (int)variantGroupObject["Id"];
            Key = (Guid)variantGroupObject["Key"];
            Title = (string)variantGroupObject["Title"];

            var variants = variantGroupObject["Variants"];

            var variantsList = new List<OrderedVariant>();

            if (!string.IsNullOrEmpty(variants.ToString()))
            {
                var variantsArray = (JArray)variants;

                if (variantsArray != null && variantsArray.Any())
                {

                    foreach (var variantObject in variantsArray)
                    {
                        var variant = new OrderedVariant(variantObject, storeInfo);

                        variantsList.Add(variant);
                    }
                }
            }

            if (variantsList.Any())
            {
                Variants = variantsList;
            }
            else
            {
                Variants = Enumerable.Empty<OrderedVariant>();
            }
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
