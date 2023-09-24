angular.module('umbraco').controller('Ekom.Stock', function ($scope, $routeParams, $http, contentResource, $q) {
  $scope.stocks = [];
  $scope.content = null;
  $scope.config = null;
  $scope.perStoreStock = false;
  $scope.model.hideLabel = false;

  $scope.getStockData = function () {

    if ($scope.perStoreStock) {


      $http.get(Umbraco.Sys.ServerVariables.ekom.apiEndpoint + 'Stores').then(function (results) {
        
        if (results.data.length > 1) {
          results.data.forEach(function (store) {

            getStockValue(store.alias).then(function (stockValue) {
              $scope.stocks.push({
                storeAlias: store.alias,
                value: stockValue
              });
            })
          });
        } else {
          getStockValue('').then(function (stockValue) {
            $scope.stocks.push({
              storeAlias: '',
              value: stockValue
            });
          })
        }
      });

    } else {

      getStockValue('').then(function (stockValue) {
        $scope.stocks.push({
          storeAlias: '',
          value: stockValue
        });
      })
    }

  };

  var getStockValue = function (storeAlias) {
    
    if ($scope.content.contentTypeAlias !== 'ekmProduct' && $scope.content.contentTypeAlias !== 'ekmProductVariant') {
      return $q.when(0);
    }

    if (storeAlias !== '') {
      return $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Stock/' + $scope.content.key + "/storeAlias/" + storeAlias)
        .then(function (result) {
          return parseInt(result.data);
        });
    } else {
      return $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Stock/' + $scope.content.key)
        .then(function (result) {
          return parseInt(result.data);
        });
    }
  }

  $scope.init = function () {
    if ($routeParams.section == 'content') {

      contentResource.getById($routeParams.id)
        .then(function (data) {
          $scope.content = data;

          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Config').then(function (result) {

            $scope.config = result.data;

            if ($scope.config.perStoreStock) {

              $scope.perStoreStock = true;
            }

            $scope.getStockData();

          });

        });
    }

  };

  $scope.init();

  $scope.$on("formSubmitting", function () {
    $scope.model.value = $scope.stocks;
  });

});
