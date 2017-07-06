using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Interfaces
{
    public interface IStore : INodeEntitiy
    {
        /// <summary>
        /// Gets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        decimal Vat { get; }

        /// <summary>
        /// Gets the culture.
        /// </summary>
        /// <value>
        /// The culture.
        /// </value>
        CultureInfo Culture { get; }
    }
}
