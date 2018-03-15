using Ekom.Interfaces;
using log4net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Umbraco.Web;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedVariantGroup
    {
        private IVariant variant;
        private IStore store;
        private StoreInfo storeInfo;

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public Guid[] ImageIds { get; set; }
        public IEnumerable<OrderedVariant> Variants { get; set; }

        public IReadOnlyDictionary<string, string> Properties;

        public OrderedVariantGroup(IVariant variant, IVariantGroup variantGroup, IStore store)
        {
            this.variant = variant;
            this.store = store;

            Log.Info("Debug1");

            Properties = new ReadOnlyDictionary<string, string>(
                variantGroup.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            Log.Info("Debug2");
            Id = variantGroup.Id;

            Log.Info("Debug3");
            Key = variantGroup.Key;

            Log.Info("Debug4");
            Title = variantGroup.Title;

            Log.Info("Debug5");
            ImageIds = variantGroup.Images.Any() ? variantGroup.Images.Select(x => x.GetKey()).ToArray() : new Guid[] { };


            Log.Info("Debug6");
            var variants = new List<OrderedVariant>();


            Log.Info("Debug7");
            variants.Add(new OrderedVariant(variant, store));

            Variants = variants;


        }

        public OrderedVariantGroup(JToken variantGroupObject, StoreInfo storeInfo)
        {
            Log.Info("Creating OrderedVariantGroup from Json");

            this.storeInfo = storeInfo;

            Properties = new ReadOnlyDictionary<string, string>(
                variantGroupObject["Properties"].ToObject<Dictionary<string, string>>());

            Id = (int)variantGroupObject["Id"];
            Key = (Guid)variantGroupObject["Key"];
            Title = (string)variantGroupObject["Title"];
            ImageIds = variantGroupObject["ImageIds"].ToObject<Guid[]>();

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
