using Ekom.Models;
using Ekom.Tests.Objects;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Ekom.Tests.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class OrderJsonHydrationCollection
{
    public const string Name = "Order JSON hydration";
}

[Collection(OrderJsonHydrationCollection.Name)]
public class OrderJsonHydrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructor_PreservesStoreAndProviderSnapshots(bool encodedObjects)
    {
        WithDefaultSettings(null, () =>
        {
            using var configuration = new ConfigurationScope();
            var json = CreateOrderJson();
            if (encodedObjects)
            {
                foreach (var name in SnapshotNames)
                    json[name] = json[name]!.ToString();
            }

            var data = new OrderData { OrderInfo = json.ToString() };
            var order = new OrderInfo(data);
            var parsed = JObject.Parse(data.OrderInfo);
            var expectedStore = new StoreInfo(LegacyObject(parsed, "StoreInfo")!);
            var expectedShipping = new OrderedShippingProvider(LegacyObject(parsed, "ShippingProvider")!, expectedStore);
            var expectedPayment = new OrderedPaymentProvider(LegacyObject(parsed, "PaymentProvider")!, expectedStore);

            AssertJsonEqual(expectedStore, order.StoreInfo);
            AssertProvidersEqual(expectedShipping, order.ShippingProvider!);
            AssertProvidersEqual(expectedPayment, order.PaymentProvider!);
            Assert.Equal(0.24m, order.StoreInfo.Vat);
            Assert.True(order.StoreInfo.VatIncludedInPrice);
            Assert.True(order.StoreInfo.ApplyVatOnShipping);
            Assert.Equal(2, order.StoreInfo.Currencies.Count);
            Assert.Equal(31.25m, order.PaymentProvider!.Price.OriginalValue);
            Assert.Equal(14.5m, order.ShippingProvider!.Price.OriginalValue);
            Assert.Equal("en-US", order.PaymentProvider.Price.Currency.CurrencyValue);
            Assert.Equal("receipt", order.PaymentProvider.CustomData["custompaymentnote"]);
            Assert.Equal("door", order.ShippingProvider.CustomData["customshippingnote"]);
        });
    }

    [Theory]
    [InlineData("StoreInfo")]
    [InlineData("ShippingProvider")]
    [InlineData("PaymentProvider")]
    [InlineData("CustomerInformation")]
    [InlineData("Tracking")]
    [InlineData("Consent")]
    public void Helpers_ReturnNullForMissingNullAndEmptyTokens(string name)
    {
        WithDefaultSettings(null, () =>
        {
            var order = CreateHelperTarget();
            Assert.Null(InvokeHelper(order, name, new JObject()));
            Assert.Null(InvokeHelper(order, name, new JObject { [name] = JValue.CreateNull() }));
            Assert.Null(InvokeHelper(order, name, new JObject { [name] = "" }));
        });
    }

    [Theory]
    [InlineData("StoreInfo")]
    [InlineData("ShippingProvider")]
    [InlineData("PaymentProvider")]
    public void ObjectHelpers_PreserveLegacyExceptionsForInvalidNonObjects(string name)
    {
        WithDefaultSettings(null, () =>
        {
            var order = CreateHelperTarget();
            JToken[] invalidTokens = [new JValue("invalid"), new JValue(" "), new JArray(), new JValue(42), new JValue(true)];
            foreach (var token in invalidTokens)
            {
                var json = new JObject { [name] = token };
                var expected = Record.Exception(() => LegacyObject(json, name));
                var actual = Assert.Throws<TargetInvocationException>(() => InvokeHelper(order, name, json));
                Assert.NotNull(expected);
                Assert.IsType(expected.GetType(), actual.InnerException);
            }
        });
    }

    [Fact]
    public void Constructor_PreservesLegacyScalarStoreCurrencyAndPriceObject()
    {
        WithDefaultSettings(null, () =>
        {
            using var configuration = new ConfigurationScope();
            var json = CreateOrderJson();
            json["StoreInfo"]!["Currency"] = "en-US";
            json["StoreInfo"]!["Currencies"] = "legacy";
            foreach (var name in new[] { "PaymentProvider", "ShippingProvider" })
            {
                ((JObject)json[name]!).Remove("Prices");
                json[name]!["Price"] = new JObject { ["OriginalValue"] = 17.75m, ["Quantity"] = 3m };
            }

            var order = new OrderInfo(new OrderData { OrderInfo = json.ToString() });
            var store = new StoreInfo(LegacyObject(json, "StoreInfo")!);
            AssertJsonEqual(store, order.StoreInfo);
            Assert.Equal("en-US", order.StoreInfo.Currency.CurrencyValue);
            Assert.Equal("is-IS", Assert.Single(order.StoreInfo.Currencies).CurrencyValue);
            AssertProvidersEqual(new OrderedPaymentProvider(LegacyObject(json, "PaymentProvider")!, store), order.PaymentProvider!);
            AssertProvidersEqual(new OrderedShippingProvider(LegacyObject(json, "ShippingProvider")!, store), order.ShippingProvider!);
            Assert.Equal(17.75m, order.PaymentProvider!.Price.OriginalValue);
            Assert.Equal(3m, Assert.IsType<Price>(order.PaymentProvider.Price).Quantity);
        });
    }

    [Theory]
    [InlineData("2026-01-02T03:04:05.123Z", false)]
    [InlineData("2026-01-02T03:04:05+02:00", false)]
    [InlineData("2026-01-02T03:04:05", false)]
    [InlineData("2026-01-02T03:04:05.123Z", true)]
    [InlineData("2026-01-02T03:04:05+02:00", true)]
    public void TypedHelpers_RetainTextNormalizationForStringAndDateTokens(string timestamp, bool dateTokens)
    {
        WithDefaultSettings(null, () => AssertTypedHelpers(timestamp, dateTokens));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedHelpers_HonorGlobalDateParseHandlingAndConverters(bool converters)
    {
        var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
        if (converters)
        {
            settings.Converters.Add(new TypedSnapshotConverter<CustomerInfo>());
            settings.Converters.Add(new TypedSnapshotConverter<OrderTracking>());
            settings.Converters.Add(new TypedSnapshotConverter<OrderConsent>());
        }
        WithDefaultSettings(() => settings, () =>
        {
            AssertTypedHelpers("2026-01-02T03:04:05.123Z", false, converters);
            AssertTypedHelpers("2026-01-02T03:04:05+02:00", true, converters);
        });
    }

    [Theory]
    [InlineData("ShippingProvider", false)]
    [InlineData("PaymentProvider", false)]
    [InlineData("ShippingProvider", true)]
    [InlineData("PaymentProvider", true)]
    public void EkomSerializerCustomization_PreservesRootedPriceReaderPathAndLegacyLineInfo(string name, bool customResolver)
    {
        WithDefaultSettings(null, () =>
        {
            var serializer = EkomJsonDotNet.Serializer;
            var previousResolver = serializer.ContractResolver;
            var converter = new ObservingPriceConverter();
            try
            {
                if (customResolver)
                    serializer.ContractResolver = new PriceContractResolver(converter);
                else
                    serializer.Converters.Add(converter);

                var json = CreateOrderJson();
                ((JObject)json[name]!).Remove("Prices");
                var target = CreateHelperTarget();
                var legacyObject = LegacyObject(json, name)!;
                if (name == "PaymentProvider")
                    _ = new OrderedPaymentProvider(legacyObject, target.StoreInfo);
                else
                    _ = new OrderedShippingProvider(legacyObject, target.StoreInfo);
                var expected = Assert.Single(converter.Reads);
                converter.Reads.Clear();

                var actual = InvokeHelper(target, name, json);

                Assert.NotNull(actual);
                Assert.Equal(expected, Assert.Single(converter.Reads));
                Assert.Equal("Price", expected.Path);
                Assert.True(expected.HasLineInfo);
                Assert.True(expected.LineNumber > 0);
            }
            finally
            {
                serializer.Converters.Remove(converter);
                serializer.ContractResolver = previousResolver;
            }
        });
    }

    [Theory]
    [InlineData("PaymentProvider")]
    [InlineData("ShippingProvider")]
    public void CachedPriceContractConverter_PreservesLegacyTextBoundary(string name)
    {
        WithDefaultSettings(null, () =>
        {
            var serializer = EkomJsonDotNet.Serializer;
            var resolver = Assert.IsType<DefaultContractResolver>(serializer.ContractResolver);
            Assert.Empty(serializer.Converters);
            var contract = resolver.ResolveContract(typeof(Price));
            var previousConverter = contract.Converter;
            var converter = new ObservingPriceConverter();
            try
            {
                // Customize the exact resolver's cached contract, not its type or the serializer's converter list.
                contract.Converter = converter;
                var json = CreateOrderJson();
                ((JObject)json[name]!).Remove("Prices");
                var target = CreateHelperTarget();
                var legacyObject = LegacyObject(json, name)!;
                if (name == "PaymentProvider")
                    _ = new OrderedPaymentProvider(legacyObject, target.StoreInfo);
                else
                    _ = new OrderedShippingProvider(legacyObject, target.StoreInfo);
                var expected = Assert.Single(converter.Reads);
                converter.Reads.Clear();

                Assert.NotNull(InvokeHelper(target, name, json));

                Assert.Equal(expected, Assert.Single(converter.Reads));
                Assert.Equal("Price", expected.Path);
                Assert.True(expected.HasLineInfo);
                Assert.True(expected.LineNumber > 0);
            }
            finally
            {
                contract.Converter = previousConverter;
            }
        });
    }

    [Theory]
    [InlineData("PaymentProvider")]
    [InlineData("ShippingProvider")]
    public void OrdinaryProviderObject_IsDeepClonedWithoutRewritingLineInfo(string name)
    {
        WithDefaultSettings(null, () =>
        {
            Assert.Empty(EkomJsonDotNet.Serializer.Converters);
            Assert.IsType<DefaultContractResolver>(EkomJsonDotNet.Serializer.ContractResolver);
            Assert.Null(EkomJsonDotNet.Serializer.TraceWriter);
            var json = JObject.Parse("\n\n\n" + CreateOrderJson().ToString());
            var original = Assert.IsType<JObject>(json[name]);
            var method = typeof(OrderInfo).GetMethod("ReadOrderJsonObject", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var clone = Assert.IsType<JObject>(method.Invoke(null, [original, true]));

            Assert.NotSame(original, clone);
            Assert.Null(clone.Parent);
            Assert.Equal(string.Empty, clone.Path);
            Assert.True(JToken.DeepEquals(original, clone));
            Assert.Equal(((IJsonLineInfo)original).LineNumber, ((IJsonLineInfo)clone).LineNumber);
            Assert.Equal(((IJsonLineInfo)original).LinePosition, ((IJsonLineInfo)clone).LinePosition);
            Assert.NotEqual(((IJsonLineInfo)LegacyObject(json, name)!).LineNumber, ((IJsonLineInfo)clone).LineNumber);
            Assert.NotSame(original["Prices"], clone["Prices"]);
            clone["Properties"]!["reference"] = "changed";
            Assert.Equal("saved", original["Properties"]!["reference"]!.Value<string>());
        });
    }

    [Theory]
    [InlineData("PaymentProvider")]
    [InlineData("ShippingProvider")]
    public void GlobalDefaults_PreserveRootedDictionaryReaderAndReparsedLineInfo(string name)
    {
        var converter = new ObservingDictionaryConverter();
        var settings = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
        settings.Converters.Add(converter);
        WithDefaultSettings(() => settings, () =>
        {
            var json = CreateOrderJson();
            var target = CreateHelperTarget();
            var legacyObject = LegacyObject(json, name)!;
            if (name == "PaymentProvider")
                _ = new OrderedPaymentProvider(legacyObject, target.StoreInfo);
            else
                _ = new OrderedShippingProvider(legacyObject, target.StoreInfo);
            var expected = converter.Reads.ToArray();
            converter.Reads.Clear();

            Assert.NotNull(InvokeHelper(target, name, json));

            Assert.Equal(expected, converter.Reads);
            Assert.Equal(new[] { "Properties", "CustomData" }, expected.Select(x => x.Path));
            Assert.All(expected, read => Assert.True(read.HasLineInfo));
        });
    }

    [Fact]
    public void Constructor_DoesNotShareMutableSnapshotsBetweenModelsFromSameOrderData()
    {
        WithDefaultSettings(null, () =>
        {
            using var configuration = new ConfigurationScope();
            var data = new OrderData { OrderInfo = CreateOrderJson().ToString() };
            var originalJson = data.OrderInfo;
            var first = new OrderInfo(data);
            var second = new OrderInfo(data);

            Assert.NotSame(first.StoreInfo.Currency, second.StoreInfo.Currency);
            Assert.NotSame(first.StoreInfo.Currencies, second.StoreInfo.Currencies);
            Assert.NotSame(first.StoreInfo.Currencies[0], second.StoreInfo.Currencies[0]);
            Assert.NotSame(first.PaymentProvider!.Properties, second.PaymentProvider!.Properties);
            Assert.NotSame(first.ShippingProvider!.Properties, second.ShippingProvider!.Properties);
            Assert.NotSame(first.PaymentProvider.Price, second.PaymentProvider.Price);
            Assert.NotSame(first.ShippingProvider.Price, second.ShippingProvider.Price);
            first.StoreInfo.Currency.CurrencyValue = "da-DK";
            first.StoreInfo.Currencies[0].CurrencyValue = "fi-FI";
            first.PaymentProvider.CustomData["custompaymentnote"] = "changed";
            first.ShippingProvider.CustomData["customshippingnote"] = "changed";
            first.PaymentProvider.Prices[0].Currency.CurrencyValue = "is-IS";
            first.ShippingProvider.Prices.Clear();

            Assert.Equal("en-US", second.StoreInfo.Currency.CurrencyValue);
            Assert.Equal("en-US", second.StoreInfo.Currencies[0].CurrencyValue);
            Assert.Equal("receipt", second.PaymentProvider.CustomData["custompaymentnote"]);
            Assert.Equal("door", second.ShippingProvider.CustomData["customshippingnote"]);
            Assert.Equal("da-DK", second.PaymentProvider.Prices[0].Currency.CurrencyValue);
            Assert.Equal(2, second.ShippingProvider.Prices.Count);
            Assert.Equal(originalJson, data.OrderInfo);
        });
    }

    private static readonly string[] SnapshotNames = ["StoreInfo", "ShippingProvider", "PaymentProvider"];

    private static JObject CreateOrderJson()
    {
        return new JObject
        {
            ["Culture"] = "en-US",
            ["OrderLines"] = new JArray(),
            ["StoreInfo"] = new JObject
            {
                ["Key"] = "11111111-1111-1111-1111-111111111111",
                ["Currency"] = Currency("en-US"),
                ["Currencies"] = new JArray(Currency("en-US"), Currency("da-DK")),
                ["Culture"] = "en-US",
                ["Alias"] = "Historic store",
                ["Vat"] = 0.24m,
                ["VatIncludedInPrice"] = true,
                ["ApplyVatOnShipping"] = true,
            },
            ["PaymentProvider"] = Provider("Payment", 31.25m, "custompaymentnote", "receipt"),
            ["ShippingProvider"] = Provider("Shipping", 14.5m, "customshippingnote", "door"),
        };
    }

    private static JObject Currency(string value) => new()
    {
        ["CurrencyValue"] = value,
        ["CurrencyFormat"] = "C",
    };

    private static JObject Provider(string title, decimal amount, string customKey, string customValue) => new()
    {
        ["Id"] = 7,
        ["Key"] = "22222222-2222-2222-2222-222222222222",
        ["Title"] = title,
        ["Method"] = "Pickup",
        ["Properties"] = new JObject { ["reference"] = "saved" },
        ["CustomData"] = new JObject { [customKey] = customValue },
        ["Price"] = PriceJson(amount, "en-US"),
        ["Prices"] = new JArray(PriceJson(99m, "da-DK"), PriceJson(amount, "en-US")),
    };

    private static JObject PriceJson(decimal amount, string currency) => new()
    {
        ["OriginalValue"] = amount,
        ["Quantity"] = 2m,
        ["Currency"] = Currency(currency),
    };

    private static OrderInfo CreateHelperTarget() => new(new OrderData
    {
        OrderInfo = new JObject
        {
            ["OrderLines"] = new JArray(),
            ["StoreInfo"] = CreateOrderJson()["StoreInfo"]!.DeepClone(),
        }.ToString(),
    });

    private static object? InvokeHelper(OrderInfo target, string name, JObject json)
    {
        var method = typeof(OrderInfo).GetMethod($"Create{name}FromJson", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method.Invoke(target, [json]);
    }

    // Deliberately retain the pre-optimization stringify/parse boundary as the oracle.
    private static JObject? LegacyObject(JObject json, string name)
    {
        var text = json[name]?.ToString();
        return string.IsNullOrEmpty(text) ? null : JObject.Parse(text);
    }

    private static void AssertTypedHelpers(string timestamp, bool dateTokens, bool converters = false)
    {
        JToken value = dateTokens
            ? JObject.Parse(new JObject { ["value"] = timestamp }.ToString())["value"]!
            : new JValue(timestamp);
        var json = new JObject
        {
            ["CustomerInformation"] = new JObject { ["CustomerIpAddress"] = value.DeepClone() },
            ["Tracking"] = new JObject { ["Source"] = value.DeepClone(), ["CapturedAtUtc"] = value.DeepClone() },
            ["Consent"] = new JObject { ["Source"] = value.DeepClone(), ["ResolvedAtUtc"] = value.DeepClone(), ["Marketing"] = true },
        };
        var target = CreateHelperTarget();
        var customer = Assert.IsType<CustomerInfo>(InvokeHelper(target, "CustomerInformation", json));
        var tracking = Assert.IsType<OrderTracking>(InvokeHelper(target, "Tracking", json));
        var consent = Assert.IsType<OrderConsent>(InvokeHelper(target, "Consent", json));
        AssertJsonEqual(JsonConvert.DeserializeObject<CustomerInfo>(json["CustomerInformation"]!.ToString())!, customer);
        AssertJsonEqual(JsonConvert.DeserializeObject<OrderTracking>(json["Tracking"]!.ToString())!, tracking);
        AssertJsonEqual(JsonConvert.DeserializeObject<OrderConsent>(json["Consent"]!.ToString())!, consent);
        if (converters)
        {
            Assert.StartsWith("converted:", customer.CustomerIpAddress, StringComparison.Ordinal);
            Assert.StartsWith("converted:", tracking.Source, StringComparison.Ordinal);
            Assert.StartsWith("converted:", consent.Source, StringComparison.Ordinal);
        }
    }

    private static void AssertProvidersEqual(OrderedPaymentProvider expected, OrderedPaymentProvider actual)
    {
        AssertJsonEqual(expected, actual);
        AssertJsonEqual(expected.Prices, actual.Prices);
    }

    private static void AssertProvidersEqual(OrderedShippingProvider expected, OrderedShippingProvider actual)
    {
        AssertJsonEqual(expected, actual);
        AssertJsonEqual(expected.Prices, actual.Prices);
    }

    private static void AssertJsonEqual(object expected, object actual)
    {
        var serializer = JsonSerializer.Create();
        Assert.True(JToken.DeepEquals(JToken.FromObject(expected, serializer), JToken.FromObject(actual, serializer)));
    }

    private static void WithDefaultSettings(Func<JsonSerializerSettings>? settings, Action action)
    {
        var previous = JsonConvert.DefaultSettings;
        try
        {
            JsonConvert.DefaultSettings = settings;
            action();
        }
        finally
        {
            JsonConvert.DefaultSettings = previous;
        }
    }

    private sealed record ReaderObservation(string Path, bool HasLineInfo, int LineNumber, int LinePosition)
    {
        public static ReaderObservation Capture(JsonReader reader)
        {
            var lineInfo = reader as IJsonLineInfo;
            return new(reader.Path, lineInfo?.HasLineInfo() == true, lineInfo?.LineNumber ?? 0, lineInfo?.LinePosition ?? 0);
        }
    }

    private sealed class ObservingPriceConverter : JsonConverter<Price>
    {
        public List<ReaderObservation> Reads { get; } = [];
        public override Price? ReadJson(JsonReader reader, Type objectType, Price? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Reads.Add(ReaderObservation.Capture(reader));
            var token = JObject.Load(reader);
            return new Price(token, new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" }, 0.24m, true);
        }
        public override void WriteJson(JsonWriter writer, Price? value, JsonSerializer serializer) => throw new NotSupportedException();
    }

    private sealed class PriceContractResolver(ObservingPriceConverter converter) : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);
            if (objectType == typeof(Price))
                contract.Converter = converter;
            return contract;
        }
    }

    private sealed class ObservingDictionaryConverter : JsonConverter<Dictionary<string, string>>
    {
        public List<ReaderObservation> Reads { get; } = [];
        public override Dictionary<string, string>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            Reads.Add(ReaderObservation.Capture(reader));
            return JObject.Load(reader).ToObject<Dictionary<string, string>>(JsonSerializer.Create());
        }
        public override void WriteJson(JsonWriter writer, Dictionary<string, string>? value, JsonSerializer serializer) => throw new NotSupportedException();
    }

    private sealed class TypedSnapshotConverter<T> : JsonConverter<T> where T : class
    {
        public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = JObject.Load(reader).ToObject<T>(JsonSerializer.Create());
            switch (value)
            {
                case CustomerInfo customer: customer.CustomerIpAddress = "converted:" + customer.CustomerIpAddress; break;
                case OrderTracking tracking: tracking.Source = "converted:" + tracking.Source; break;
                case OrderConsent consent: consent.Source = "converted:" + consent.Source; break;
            }
            return value;
        }
        public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer) => throw new NotSupportedException();
    }
}
