using System;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class UmbracoDomain
    {
        public UmbracoDomain(IDictionary<string, string> properties)
        {
            _properties = new Dictionary<string, string>(properties);
        }

        readonly Dictionary<string, string> _properties;
        /// <summary>
        /// All node properties
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties => _properties;
        public string DomainName
        {
            get
            {

                if (Properties.TryGetValue("DomainName", out string _domainName))
                {
                    return _domainName;
                }

                return string.Empty;
            }

        }
        public Guid Key
        {
            get
            {

                if (Properties.TryGetValue("Key", out string _key))
                {
                    return new Guid(_key);
                }

                return Guid.Empty;
            }

        }
        public int Id
        {
            get
            {

                if (Properties.TryGetValue("Id", out string _id))
                {
                    return Convert.ToInt32(_id);
                }

                return -1;
            }
        }
        public string LanguageIsoCode
        {
            get
            {

                if (Properties.TryGetValue("LanguageIsoCode", out string _languageIsoCode))
                {
                    return _languageIsoCode;
                }

                return string.Empty;
            }
        }

        public int? RootContentId
        {
            get
            {

                if (Properties.TryGetValue("RootContentId", out string _rootContentId))
                {
                    if (int.TryParse(_rootContentId, out int _intRootContentId))
                    {
                        return _intRootContentId;
                    }

                }

                return null;
            }
        }


    }
}
