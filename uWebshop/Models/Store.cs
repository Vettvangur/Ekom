using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Models;

namespace uWebshop.Models
{
    public class Store
    {
        public int Id { get; set; }
        public string Alias { get; set; }
        public StoreNode RootNode { get; set; }
        public int StoreRootNode {get; set;}
        public int Level { get; set; }
        public IEnumerable<IDomain> Domains { get; set; }
    }
}
