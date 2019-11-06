using Ekom.Interfaces;
using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.Tests
{
    [TestClass]
    public class NodeTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        // This was done to determine why registration overwriting broke
        // registering UmbracoHelper broke overwriting.
        //[TestMethod]
        //public void TestEq2()
        //{
        //    var reg = RegisterFactory.Create();
        //    reg.Register(Mock.Of<IExamineService>());
        //    var fac = reg.CreateFactory();

        //    var mockExamineSvc = new Mock<IExamineService>();
        //    reg.Register<IExamineService>(mockExamineSvc.Object);
        //    Assert.AreEqual(mockExamineSvc.Object, fac.GetInstance<IExamineService>());
        //}
        //[TestMethod]
        //public void TestEq3()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();

        //    var mockExamineSvc = new Mock<IExamineService>();
        //    reg.Register<IExamineService>(mockExamineSvc.Object);
        //    Assert.AreEqual(mockExamineSvc.Object, fac.GetInstance<IExamineService>());
        //}

        /// <summary>
        /// Unpublished items can't be found in the external examine index
        /// </summary>
        [TestMethod]
        public void VariantWithUnpublishedCategoryIsUnpublished()
        {
            var (fac, reg) = Helpers.RegisterAll();
            var mockExamineSvc = new Mock<IExamineService>();
            reg.Register(mockExamineSvc.Object);

            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1179)))
                .Returns(Objects.Objects.Get_Category_Women_SearchResult());
            // The missing node, indicating an unpublished item
            //mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1079)))
            //    .Returns(Shirt_product_2.SearchResult());
            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1195)))
                .Returns(Objects.Objects.Get_shirt2_blue_variantgroup_SearchResult());
            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1200)))
                .Returns(Objects.Objects.Get_shirt2_blue_S_variant_SearchResult());

            Assert.IsTrue(NodeHelper.IsItemUnpublished(
                Objects.Objects.Get_shirt2_blue_S_variant_SearchResult())
            );
        }

        [TestMethod]
        public void VariantWithDisabledCategoryIsDisabled()
        {
            var (fac, reg) = Helpers.RegisterAll();
            var mockExamineSvc = new Mock<IExamineService>();
            reg.Register(mockExamineSvc.Object);

            var isStore = Objects.Objects.Get_IS_Store_Vat_Included();
            var dkStore = Objects.Objects.Get_DK_Store_Vat_Included();

            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1179)))
                .Returns(Objects.Objects.Get_Category_Women_SearchResult());
            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1079)))
                .Returns(Shirt_product_2.SearchResult());
            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1195)))
                .Returns(Objects.Objects.Get_shirt2_blue_variantgroup_SearchResult());
            mockExamineSvc.Setup(x => x.GetExamineNode(It.Is<int>(y => y == 1200)))
                .Returns(Objects.Objects.Get_shirt2_blue_S_variant_SearchResult());

            Assert.IsTrue(NodeHelper.IsItemDisabled(
                Objects.Objects.Get_shirt2_blue_S_variant_SearchResult(),
                isStore)
            );
            Assert.IsFalse(NodeHelper.IsItemDisabled(
                Objects.Objects.Get_shirt2_blue_S_variant_SearchResult(),
                dkStore)
            );
        }
    }
}
