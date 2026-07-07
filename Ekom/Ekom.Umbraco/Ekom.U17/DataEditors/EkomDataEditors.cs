using Umbraco.Cms.Core.PropertyEditors;

namespace Ekom.DataEditors;

[DataEditor("Ekom.Price", ValueType = ValueTypes.Json)]
public sealed class EkomPriceEditor : DataEditor
{
    public EkomPriceEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Stock", ValueType = ValueTypes.Json)]
public sealed class EkomStockEditor : DataEditor
{
    public EkomStockEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Coupon", ValueType = ValueTypes.Json)]
public sealed class EkomCouponEditor : DataEditor
{
    public EkomCouponEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Currency", ValueType = ValueTypes.Json)]
public sealed class EkomCurrencyPicker : DataEditor
{
    public EkomCurrencyPicker(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Country", ValueType = ValueTypes.Json)]
public sealed class EkomCountryPicker : DataEditor
{
    public EkomCountryPicker(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Metafield", ValueType = ValueTypes.Json)]
public sealed class EkomMetafieldPicker : DataEditor
{
    public EkomMetafieldPicker(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Metavalue", ValueType = ValueTypes.Json)]
public sealed class EkomMetavaluePicker : DataEditor
{
    public EkomMetavaluePicker(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Range", ValueType = ValueTypes.Json)]
public sealed class EkomRangeEditor : DataEditor
{
    public EkomRangeEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Cache", ValueType = ValueTypes.Json)]
public sealed class EkomCacheEditor : DataEditor
{
    public EkomCacheEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Zone", ValueType = ValueTypes.Json)]
public sealed class EkomZonePicker : DataEditor
{
    public EkomZonePicker(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}

[DataEditor("Ekom.Property", ValueType = ValueTypes.Json)]
public sealed class EkomPropertyEditor : DataEditor
{
    public EkomPropertyEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
    {
    }
}
