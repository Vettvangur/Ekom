using HtmlAgilityPack;
using System.Text;
using System.Text.Json;

namespace Ekom.Algolia.Mappers;

internal static class AlgoliaHtmlTextConverter
{
    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "dd", "div", "dl", "dt", "figcaption", "figure",
        "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "li", "main", "nav",
        "ol", "p", "pre", "section", "table", "tbody", "td", "tfoot", "th", "thead", "tr", "ul",
    };

    public static object? Convert(object? value)
        => value is string text ? ConvertToText(text) : value;

    public static string ConvertToText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var markup = TryGetMarkup(value) ?? value;
        var document = new HtmlDocument
        {
            OptionFixNestedTags = true,
        };
        document.LoadHtml(markup);

        var nonContentNodes = document.DocumentNode.SelectNodes("//script|//style|//noscript|//template");
        if (nonContentNodes is not null)
        {
            foreach (var node in nonContentNodes)
                node.Remove();
        }

        var text = new StringBuilder(markup.Length);
        AppendText(document.DocumentNode, text);

        return NormalizeWhitespace(HtmlEntity.DeEntitize(text.ToString()));
    }

    private static void AppendText(HtmlNode node, StringBuilder text)
    {
        if (node.NodeType == HtmlNodeType.Comment)
            return;

        if (node.NodeType == HtmlNodeType.Text)
        {
            text.Append(node.InnerText);
            return;
        }

        var isSeparator = node.Name.Equals("br", StringComparison.OrdinalIgnoreCase)
            || BlockElements.Contains(node.Name);

        if (isSeparator)
            text.Append(' ');

        foreach (var child in node.ChildNodes)
            AppendText(child, text);

        if (isSeparator)
            text.Append(' ');
    }

    private static string? TryGetMarkup(string value)
    {
        var trimmed = value.TrimStart();
        if (!trimmed.StartsWith('{'))
            return null;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("markup", out var markup)
                && markup.ValueKind == JsonValueKind.String)
            {
                return markup.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static string NormalizeWhitespace(string value)
    {
        var normalized = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var character in value)
        {
            if (char.IsWhiteSpace(character) || character == '\u00a0')
            {
                pendingSpace = normalized.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                normalized.Append(' ');
                pendingSpace = false;
            }

            normalized.Append(character);
        }

        return normalized.ToString();
    }
}
