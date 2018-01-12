using Ekom.Models.Behaviors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Represents an object that has constraints available
    /// </summary>
    interface IConstrained
    {

        Constraints Constraints { get; }
    }
}
