(function () {
  "use strict";

  function controller($scope, resources, ekmResources, notificationsService, editorState, $routeParams) {
    var rootCategoryKey = editorState.current.key;
    var nodeId = editorState.current.id || $routeParams.id;

    $scope.loading = false;
    $scope.loadingStores = false;
    $scope.rootCategoryKey = rootCategoryKey;
    $scope.currentCategoryKey = rootCategoryKey;
    $scope.stores = [];
    $scope.selectedStoreAlias = '';
    $scope.recursive = false;
    $scope.page = 1;
    $scope.pageSize = 24;
    $scope.pageSizes = [12, 24, 48, 96];
    $scope.searchQuery = '';
    $scope.appliedSearchQuery = '';
    $scope.pageCount = 1;
    $scope.productCount = 0;
    $scope.totalProductCount = 0;
    $scope.products = [];
    $scope.categories = [];
    $scope.breadcrumbs = [];
    $scope.category = null;

    $scope.Load = function () {
      $scope.loading = true;

      resources.GetCatalogItems({
        categoryKey: $scope.currentCategoryKey,
        recursive: $scope.recursive,
        page: $scope.page,
        pageSize: $scope.pageSize,
        search: $scope.appliedSearchQuery,
        storeAlias: $scope.selectedStoreAlias
      }).then(function (result) {
        var data = result.data || {};

        $scope.category = data.category;
        $scope.breadcrumbs = data.breadcrumbs || [];
        $scope.categories = data.categories || [];
        $scope.products = data.products || [];
        $scope.page = data.page || 1;
        $scope.pageSize = data.pageSize || $scope.pageSize;
        $scope.pageCount = data.pageCount || 1;
        $scope.productCount = data.productCount || 0;
        $scope.totalProductCount = data.totalProductCount || 0;
        $scope.appliedSearchQuery = data.search || '';
        $scope.loading = false;
      }, function () {
        $scope.loading = false;
        notificationsService.error("Error", "Error fetching catalog listview items.");
      });
    };

    $scope.ToggleRecursive = function () {
      $scope.page = 1;
      $scope.Load();
    };

    $scope.ChangePageSize = function () {
      $scope.page = 1;
      $scope.Load();
    };

    $scope.ChangeStore = function () {
      $scope.searchQuery = '';
      $scope.appliedSearchQuery = '';
      $scope.page = 1;
      $scope.Load();
    };

    $scope.NavigateToCategory = function (category) {
      if (!category || !category.key || category.key === $scope.currentCategoryKey) {
        return;
      }

      $scope.currentCategoryKey = category.key;
      $scope.searchQuery = '';
      $scope.appliedSearchQuery = '';
      $scope.page = 1;
      $scope.Load();
    };

    $scope.GoToRootCategory = function () {
      $scope.NavigateToCategory({ key: $scope.rootCategoryKey });
    };

    $scope.CanGoBack = function () {
      return $scope.currentCategoryKey !== $scope.rootCategoryKey && $scope.breadcrumbs.length > 1;
    };

    $scope.GoBack = function () {
      if (!$scope.CanGoBack()) {
        return;
      }

      $scope.NavigateToCategory($scope.breadcrumbs[$scope.breadcrumbs.length - 2]);
    };

    $scope.Search = function () {
      $scope.appliedSearchQuery = ($scope.searchQuery || '').trim();
      $scope.page = 1;
      $scope.Load();
    };

    $scope.ClearSearch = function () {
      $scope.searchQuery = '';
      $scope.appliedSearchQuery = '';
      $scope.page = 1;
      $scope.Load();
    };

    $scope.HandleSearchKeydown = function ($event) {
      if ($event.key === 'Enter' || $event.keyCode === 13) {
        $event.preventDefault();
        $scope.Search();
      }
    };

    $scope.GoToPage = function (page) {
      if (page < 1 || page > $scope.pageCount || page === $scope.page) {
        return;
      }

      $scope.page = page;
      $scope.Load();
    };

    $scope.GetImageUrl = function (product) {
      if (!product || !product.imageUrl) {
        return '';
      }

      return product.imageUrl + '?width=360&height=260&rmode=boxpad&bgcolor=ffffff';
    };

    $scope.LoadStores = function () {
      $scope.loadingStores = true;

      ekmResources.getStoresByNode(nodeId).then(function (stores) {
        $scope.stores = stores || [];

        if (!$scope.selectedStoreAlias && $scope.stores.length) {
          $scope.selectedStoreAlias = $scope.stores[0].alias;
        }

        $scope.loadingStores = false;
        $scope.Load();
      }, function () {
        $scope.loadingStores = false;
        notificationsService.error("Error", "Error fetching available stores.");
        $scope.Load();
      });
    };

    $scope.LoadStores();
  }

  angular.module("umbraco").controller("Ekom.Listview", [
    "$scope",
    "Ekom.Listview.Resources",
    "Ekom.Resources",
    "notificationsService",
    "editorState",
    "$routeParams",
    controller
  ]);
})();
