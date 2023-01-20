angular.module('umbraco').controller('Ekom.Stock', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
  $scope.storeList = [];
  $scope.content = null;
  $scope.stockValue = 0;
  $scope.storeSelected = '';
  $scope.config = null;
  $scope.perStoreStock = false;
  $scope.model.hideLabel = false;

  $scope.GetStores = function () {
    $http.get(Umbraco.Sys.ServerVariables.ekom.apiEndpoint + 'Stores').then(function (results) {

      $scope.storeList = results.data;

      if ($scope.storeList.length > 0) {
        $scope.storeSelected = $scope.storeList[0].Alias;
        $scope.GetStock($scope.storeList[0].Alias);
      }

    });
  };

  $scope.GetStock = function (storeAlias) {

    if (storeAlias !== '') {
      $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'GetStockByStore?id=' + $scope.content.key + "&storeAlias=" + storeAlias)
        .then(function (result) {

          $scope.stockValue = parseInt(result.data);

        });
    } else {
      $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Stock/' + $scope.content.key).then(function (result) {

        $scope.stockValue = parseInt(result.data);

      });
    }


  };

  $scope.UpdateStock = function () {

    $http.patch(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'stock/' + $scope.content.key + ($scope.storeSelected !== '' ? "/StoreAlias/" + $scope.storeSelected : '') + "/value/" + $scope.stockValue  )
      .then(
        function () {
          notificationsService.success("Success", "Stock has been updated");
        },
        function () {
          notificationsService.error("Update Failed.", "Stock update failed");
        });
  };

  $scope.UpdateStore = function () {
    $scope.GetStock($scope.storeSelected);
  };

  if ($routeParams.section !== 'settings') {

    contentResource.getById($routeParams.id)
      .then(function (data) {
        $scope.content = data;

        $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Config').then(function (result) {

          $scope.config = result.data;

          if ($scope.config.PerStoreCache) {

            $scope.perStoreStock = true;

            $scope.GetStores();
          } else {
            $scope.GetStock('');
          }

        });

      });
  }


});
