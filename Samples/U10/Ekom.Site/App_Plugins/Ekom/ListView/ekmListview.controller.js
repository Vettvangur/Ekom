(function () {
  "use strict";

  function controller($scope, resources, notificationsService, $routeParams, editorState) {

    $scope.loading = false;

    $scope.products = [];

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

    $scope.GetProducts();

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
