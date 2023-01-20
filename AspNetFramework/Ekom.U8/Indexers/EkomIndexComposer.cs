using Ekom.U8.Indexers;
using Umbraco.Core;
using Umbraco.Core.Composing;

public class EkomIndexComposer: IUserComposer
{
    public void Compose(Composition composition)
    {
        composition.Components().Append<EkomIndexComponent>();
        composition.RegisterUnique<EkomIndexCreator>();
    }
}
