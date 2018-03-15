﻿using Ekom.Interfaces;
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
        private StoreInfo storeInfo;

        public int Id { get; set; }
        public Guid Key { get; set; }
        public string Title { get; set; }
        public Guid[] ImageIds { get; set; }
        public IEnumerable<OrderedVariant> Variants { get; set; }

        public IReadOnlyDictionary<string, string> Properties;

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedVariantGroup(IVariant variant, IVariantGroup variantGroup, StoreInfo storeInfo)
        {
            this.variant = variant ?? throw new ArgumentNullException(nameof(variant));
            variantGroup = variantGroup ?? throw new ArgumentNullException(nameof(variantGroup));
            storeInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

            Properties = new ReadOnlyDictionary<string, string>(
                variantGroup.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            Id = variantGroup.Id;
            Key = variantGroup.Key;
            Title = variantGroup.Title;
            ImageIds = variantGroup.Images.Any() ? variantGroup.Images.Select(x => x.GetKey()).ToArray() : new Guid[] { };

            var variants = new List<OrderedVariant>
            {
                new OrderedVariant(variant, storeInfo)
            };

            Variants = variants;
        }

        /// <summary>
        /// Json Constructor
        /// </summary>
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

        /// <summary>
        /// 
        /// </summary>
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
