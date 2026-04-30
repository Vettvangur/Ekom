using Ekom;
using Examine;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Umbraco.Cms.Core.Composing;

namespace Ekom.Umb.Services;

internal sealed class ExamineSearchIndexComponent : IComponent
{
    private readonly IExamineManager _examineManager;
    private readonly Configuration _config;
    private readonly ILogger<ExamineSearchIndexComponent> _logger;
    private readonly List<BaseIndexProvider> _indexes = new();

    public ExamineSearchIndexComponent(
        IExamineManager examineManager,
        Configuration config,
        ILogger<ExamineSearchIndexComponent> logger)
    {
        _examineManager = examineManager;
        _config = config;
        _logger = logger;
    }

    public void Initialize()
    {
        foreach (var indexName in GetIndexNames())
        {
            if (!_examineManager.TryGetIndex(indexName, out var index) || index is not BaseIndexProvider baseIndex)
            {
                _logger.LogWarning("Could not attach Ekom search normalizer to Examine index {IndexName}.", indexName);
                continue;
            }

            _indexes.Add(baseIndex);
            baseIndex.TransformingIndexValues += TransformingIndexValues;
        }
    }

    public void Terminate()
    {
        foreach (var index in _indexes)
        {
            index.TransformingIndexValues -= TransformingIndexValues;
        }

        _indexes.Clear();
    }

    private IEnumerable<string> GetIndexNames()
    {
        return new[]
            {
                _config.ExamineSearchIndex,
                "ExternalIndex",
                "InternalIndex"
            }
            .Where(indexName => !string.IsNullOrWhiteSpace(indexName))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void TransformingIndexValues(object? sender, IndexingItemEventArgs e)
    {
        var normalizedFields = _config.ExamineSearchNormalizedFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedFields.Count == 0)
            return;

        var values = e.ValueSet.Values.ToDictionary(
            kvp => kvp.Key,
            kvp => (IEnumerable<object>)kvp.Value,
            StringComparer.OrdinalIgnoreCase);

        var wroteValues = false;

        foreach (var field in normalizedFields)
        {
            var fieldValues = GetFieldValues(e.ValueSet.Values, field).ToList();
            if (fieldValues.Count == 0)
                continue;

            var normalizedValue = string.Join(
                ' ',
                fieldValues
                    .Select(value => ExamineSearchTextNormalizer.Normalize(value?.ToString() ?? string.Empty))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(normalizedValue))
                continue;

            values[ExamineSearchTextNormalizer.NormalizedFieldName(field)] = [normalizedValue];
            wroteValues = true;
        }

        if (!wroteValues)
            return;

        e.SetValues(values);
    }

    private static IEnumerable<object> GetFieldValues(
        IReadOnlyDictionary<string, IReadOnlyList<object>> values,
        string fieldName)
    {
        foreach (var value in values)
        {
            if (!IsFieldOrCultureVariant(value.Key, fieldName))
                continue;

            foreach (var fieldValue in value.Value)
            {
                yield return fieldValue;
            }
        }
    }

    private static bool IsFieldOrCultureVariant(string valueFieldName, string fieldName)
    {
        if (valueFieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!valueFieldName.StartsWith(fieldName + "_", StringComparison.OrdinalIgnoreCase))
            return false;

        var suffix = valueFieldName[(fieldName.Length + 1)..];
        if (string.IsNullOrWhiteSpace(suffix))
            return false;

        try
        {
            _ = CultureInfo.GetCultureInfo(suffix);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }
}
