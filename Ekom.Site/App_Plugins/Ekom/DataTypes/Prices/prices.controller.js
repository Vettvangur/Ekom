angular.module("umbraco").controller("ekom.prices", function ($scope, $http) {

  if (typeof $scope.model.value === 'object' && $scope.model.value !== null && $scope.model.value !== '') {
    $scope.prices = $scope.model.value;
  } else {

    if ($scope.model.value !== undefined) {
      $scope.prices = $scope.model.value.replace(/,/g, '.');
    }
  }

  $scope.fieldAlias = $scope.model.alias;

  $scope.storeAlias = $scope.model.alias.split('.')[1];

  $scope.currencies = [];

  $http.get('/umbraco/backoffice/ekom/api/getAllStores').then(function (results) {

    let store = results.data.find(o => o.Alias === $scope.storeAlias);

    if (isFinite($scope.prices)) {

      $scope.prices = [];

      for (c = 0; store.Currencies.length > c; c += 1) {

        $scope.prices.push({
          Currency: store.Currencies[c].CurrencyValue,
          Price: parseFloat($scope.model.value.replace(/,/g, '.'))
        });

      }

    }

    $scope.currencies = store.Currencies;

    if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {

      $scope.prices = [];

      for (i = 0; $scope.currencies.length > i; i += 1) {

        $scope.prices.push({
          "Price": 0, "Currency": $scope.currencies[i].CurrencyValue
        });

      }

    }

  });

  $scope.$on("formSubmitting", function () {

    $scope.model.value = $scope.prices;
  });

});
