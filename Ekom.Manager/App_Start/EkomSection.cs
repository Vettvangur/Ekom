using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models.Sections;

namespace Ekom.Manager.App_Start
{
    public class EkomSection : ISection
    {
        /// <inheritdoc />
        public string Alias => "ekommanager";

        /// <inheritdoc />
        public string Name => "Ekom";
    }
    
}
