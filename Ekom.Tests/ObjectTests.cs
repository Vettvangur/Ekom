using Ekom.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Tests
{
    [TestClass]
    public class ObjectTests
    {
        [TestMethod]
        public void CategoriesCanCallGetPropertyValueWithStoreAlias()
        {
            var container = Helpers.InitMockContainer();
            
            ICategory cat = Objects.Objects.Get_Category_Women();
            Assert.AreEqual("Konur", cat.GetPropertyValue("title", "IS"));
        }
    }
}
