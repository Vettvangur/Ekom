using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace Ekom.Umb.Utilities
{
    public class ImportSerializeContractResolver : DefaultContractResolver
    {
        private readonly HashSet<string> _ignoredPropertyNames = new HashSet<string>();

        public void IgnorePropertyByName(string propertyName)
        {
            _ignoredPropertyNames.Add(propertyName);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);

            if (_ignoredPropertyNames.Contains(property.PropertyName))
            {
                property.ShouldSerialize = instance => false;
            }

            return property;
        }
    }
}
