using Umbraco.Cms.Core.Models;

namespace Ekom.Umb.Services;

/// <summary>
/// Reads and writes the change-detection value the import keeps on a content or media node.
/// The property is <c>ekmComparer</c>. A site that set the import up with the older alias,
/// <c>comparer</c>, keeps working: a type that has only <c>comparer</c> is read and written there.
/// </summary>
internal static class ImportComparer
{
    /// <summary>
    /// The stored value. When the type has both properties and <c>ekmComparer</c> is still empty, the value
    /// in <c>comparer</c> is used, so that adding <c>ekmComparer</c> to a site does not make every node look changed.
    /// </summary>
    public static string? Get(IContentBase content)
    {
        if (content.HasProperty(Configuration.ImportAliasComparer))
        {
            var value = content.GetValue<string>(Configuration.ImportAliasComparer);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return content.GetValue<string>(Configuration.ImportAliasLegacyComparer);
    }

    public static void Set(IContentBase content, string? value)
        => content.SetValue(
            content.HasProperty(Configuration.ImportAliasComparer)
                ? Configuration.ImportAliasComparer
                : Configuration.ImportAliasLegacyComparer,
            value);
}
