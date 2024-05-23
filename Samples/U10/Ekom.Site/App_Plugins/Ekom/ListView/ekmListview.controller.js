(function () {
  "use strict";

  function controller($scope, resources, notificationsService, $routeParams, editorState) {

    $scope.loading = false;

    $scope.products = [];
    $scope.categories = [];

    var currentNodeKey = editorState.current.key;

    $scope.GetProducts = function () {

      resources.GetProducts(currentNodeKey)
        .then(function (result) {

          $scope.loading = false;
          $scope.products = result.data.products;

        }, function errorCallback(data) {
          $scope.loading = false;
          notificationsService.error("Error", "Error on fetching products.");
        })
    };

    $scope.GetSubCategories = function () {

      resources.GetSubCategories(currentNodeKey)
        .then(function (result) {

          $scope.loading = false;
          $scope.categories = result.data;

        }, function errorCallback(data) {
          $scope.loading = false;
          notificationsService.error("Error", "Error on fetching categories.");
        })
    };

    $scope.GetImage = function (product) {

      var imageUrl = product.images.length > 0 ? product.images[0].url : '';

      return imageUrl != '' ? '<img src="' + imageUrl + '?width=282&height=282&rmode=boxpad&bgcolor=ffffff" loading="lazy" decode="async" alt="' + product.title + '" />' : '';
    }

    $scope.Init = function () {
      $scope.GetProducts();
      $scope.GetSubCategories();
    }

    $scope.Init();
  }


  angular.module("umbraco").controller("Ekom.Listview", [
    "$scope",
    "Ekom.Listview.Resources",
    "notificationsService",
    "$routeParams",
    "editorState",
    controller
  ]);
})();
