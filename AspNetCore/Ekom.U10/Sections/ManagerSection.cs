using Umbraco.Cms.Core.Sections;

namespace Ekom.Umb.Sections
{
    public class ManagerSection : ISection
    {
        /// <inheritdoc />
        public string Alias => "ekommanager";

        /// <inheritdoc />
        public string Name => "Ekom";
    }
}
