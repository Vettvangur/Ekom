using Ekom.Interfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Web;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedVariantGroup
    {
        private readonly IVariant variant;
        private readonly StoreInfo storeInfo;

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public IEnumerable<OrderedVariant> Variants { get; set; }

        public IReadOnlyDictionary<string, string> Properties;

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedVariantGroup(IVariant variant, IVariantGroup variantGroup, StoreInfo storeInfo, decimal productVat)
        {
            this.variant = variant ?? throw new ArgumentNullException(nameof(variant));
            variantGroup = variantGroup ?? throw new ArgumentNullException(nameof(variantGroup));
            storeInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

            var props
                = variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // Prefer properties from variant group
            variantGroup.Properties
                .ToList()
                .ForEach(x => props[x.Key] = x.Value);

            Properties = new ReadOnlyDictionary<string, string>(props);

            Id = variantGroup.Id;
            Key = variantGroup.Key;
            Title = variantGroup.Title;

            var variants = new List<OrderedVariant>
            {
                new OrderedVariant(variant, storeInfo,productVat)
            };

            Variants = variants;
        }

        /// <summary>
        /// Json Constructor
        /// </summary>
        public OrderedVariantGroup(JToken variantGroupObject, StoreInfo storeInfo)
        {
            var logger = Current.Factory.GetInstance<ILogger>();
            logger.Debug<OrderedVariantGroup>("Creating OrderedVariantGroup from Json");

            this.storeInfo = storeInfo;

            Properties = new ReadOnlyDictionary<string, string>(
                variantGroupObject[nameof(Properties)].ToObject<Dictionary<string, string>>());

            Id = (int)variantGroupObject[nameof(Id)];
            Key = (Guid)variantGroupObject[nameof(Key)];
            Title = (string)variantGroupObject[nameof(Title)];

            var variants = variantGroupObject[nameof(Variants)];

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
    }
}
