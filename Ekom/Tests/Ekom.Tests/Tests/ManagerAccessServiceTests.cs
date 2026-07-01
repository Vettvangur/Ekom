using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerAccessServiceTests
{
    [Fact]
    public void CanAccessManager_Returns_True_When_User_Has_Section_Group()
    {
        var sut = CreateSubject(
            userGroups: ["ekom", "group-a"],
            sectionAccessGroup: "ekom",
            permissions: new Dictionary<string, string[]>
            {
                ["store-a"] = ["group-a"]
            },
            storeAliases: ["store-a"]);

        Assert.True(sut.CanAccessManager());
    }

    [Fact]
    public void CanAccessManager_Returns_True_When_User_Has_Store_Group()
    {
        var sut = CreateSubject(
            userGroups: ["StoreGroup"],
            sectionAccessGroup: "ekom",
            permissions: new Dictionary<string, string[]>
            {
                ["Store"] = ["StoreGroup"]
            },
            storeAliases: ["Store"]);

        Assert.True(sut.CanAccessManager());
    }

    [Fact]
    public void GetAllowedStores_Returns_Only_Stores_Mapped_To_User_Groups()
    {
        var sut = CreateSubject(
            userGroups: ["group-b"],
            sectionAccessGroup: "ekom",
            permissions: new Dictionary<string, string[]>
            {
                ["store-a"] = ["group-a"],
                ["store-b"] = ["group-b"],
                ["store-c"] = ["group-c"]
            },
            storeAliases: ["store-a", "store-b", "store-c"]);

        var allowedStores = sut.GetAllowedStoreAliases();

        Assert.Single(allowedStores);
        Assert.Contains("store-b", allowedStores);
    }

    [Fact]
    public void CanAccessStore_Returns_False_When_Store_Is_Not_Configured()
    {
        var sut = CreateSubject(
            userGroups: ["group-a"],
            sectionAccessGroup: "ekom",
            permissions: new Dictionary<string, string[]>
            {
                ["store-a"] = ["group-a"]
            },
            storeAliases: ["store-a", "store-b"]);

        Assert.False(sut.CanAccessStore("store-b"));
    }

    [Fact]
    public void Admin_Can_Access_All_Stores()
    {
        var sut = CreateSubject(
            userGroups: [],
            sectionAccessGroup: "ekom",
            permissions: new Dictionary<string, string[]>
            {
                ["store-a"] = ["group-a"]
            },
            storeAliases: ["store-a", "store-b"],
            isAdmin: true);

        var allowedStores = sut.GetAllowedStoreAliases();

        Assert.Equal(2, allowedStores.Count);
        Assert.True(sut.CanAccessStore("store-a"));
        Assert.True(sut.CanAccessStore("store-b"));
        Assert.True(sut.CanAccessManager());
    }

    private static ManagerAccessService CreateSubject(
        IReadOnlyCollection<string> userGroups,
        string? sectionAccessGroup,
        Dictionary<string, string[]> permissions,
        IReadOnlyCollection<string> storeAliases,
        bool isAdmin = false)
    {
        var options = Options.Create(new EkomOptions
        {
            Manager = new ManagerOptions
            {
                SectionAccessGroup = sectionAccessGroup,
                StoreGroupPermissions = permissions
            }
        });

        var securityService = new Mock<ISecurityService>();
        securityService.Setup(x => x.GetUmbracoUserGroups()).Returns(userGroups);
        securityService.Setup(x => x.IsCurrentUserAdmin()).Returns(isAdmin);

        var stores = storeAliases.Select(CreateStore).ToArray();

        var storeService = new Mock<IStoreService>();
        storeService.Setup(x => x.GetAllStores()).Returns(stores);

        return new ManagerAccessService(options, securityService.Object, storeService.Object);
    }

    private static IStore CreateStore(string alias)
    {
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns(alias);
        store.SetupGet(x => x.SortOrder).Returns(0);
        return store.Object;
    }
}
