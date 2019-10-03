using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Attempts to parse countries from an xml document, falls back to .NET cultures.
    /// Caches the result
    /// </summary>
    public interface ICountriesRepository
    {
        /// <summary>
        /// Attempts to parse countries from an xml document, falls back to .NET cultures.
        /// Caches the result
        /// </summary>
        List<Country> GetAllCountries();
    }
}
