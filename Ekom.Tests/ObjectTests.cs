using Ekom.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Composing;

namespace Ekom.Tests
{
    [TestClass]
    public class ObjectTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        [TestMethod]
        public void CategoriesCanCallGetPropertyValueWithStoreAlias()
        {
            Helpers.RegisterAll();
            
            ICategory cat = Objects.Objects.Get_Category_Women();
            Assert.AreEqual("Konur", cat.GetPropertyValue("title", "IS"));
        }
    }
}
