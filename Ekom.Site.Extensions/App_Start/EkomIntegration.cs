using Ekom.App_Start;
using Ekom.Interfaces;
using Ekom.Models;
using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;

namespace Ekom.Site.Extensions.App_Start
{
    [ComposeAfter(typeof(EkomRegistrations))]
    public class EkomIntegration : ICoreComposer
    {
        public void Compose(Composition composition)
        {
            composition.RegisterUnique<IPerStoreFactory<IVariant>, VariantFactoryOverride>();
        }
    }

    public class VariantFactoryOverride : IPerStoreFactory<IVariant>
    {
        public IVariant Create(ISearchResult item, IStore store)
        {
            return new VariantOverride(item, store);
        }

        public IVariant Create(IContent item, IStore store)
        {
            return new VariantOverride(item, store);
        }
    }

    public class VariantOverride : Variant
    {
        public VariantOverride(IStore store) : base(store)
        {
        }

        public VariantOverride(ISearchResult item, IStore store) : base(item, store)
        {
        }

        public VariantOverride(IContent node, IStore store) : base(node, store)
        {
        }

        public override string Title => base.Title + "-VariantOverride";

        //public override int Stock => -5;
    }
}
