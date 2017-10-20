using System.Collections.Generic;
using Ekom.Models;

namespace Ekom.Domain.Repositories
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
