angular.module("umbraco").controller("Ekom.Price", function ($scope, $http, $routeParams) {

  if ($routeParams.section !== 'content') { return; }

  $scope.model.hideLabel = false;

  $scope.fieldAlias = $scope.model.alias;

  //$scope.currencies = [];
  $scope.stores = [];

  $http.get(Umbraco.Sys.ServerVariables.ekom.apiEndpoint + 'Stores').then(function (results) {


    $scope.stores = results.data;

    // Set default prices value from existing value
    if (typeof $scope.model.value === 'object' && $scope.model.value !== null && $scope.model.value !== '') {
      // If model value is json then return

      if ($scope.model.value.hasOwnProperty('values')) {

        var temp1 = $scope.model.value.values;

        // Not sure that this applies to current implementation of SetPriceStoreValue
        // Wrapping in try-catch
        try {
          Object.keys(temp1).forEach(key => temp1[key] = JSON.parse(temp1[key]));
        }
        catch { }

        $scope.prices = temp1;

      } else {
        $scope.prices = $scope.model.value;
      }

    } else {
      // If model value is not json then return as decimal
      if ($scope.model.value !== undefined) {
        $scope.prices = $scope.model.value.replace(/,/g, '.');
      }
    }

    // Backward Compatability if value is decimal and not json
    if (isFinite($scope.prices)) {

      $scope.prices = {};

      for (s = 0; $scope.stores.length > s; s += 1) {

        let store = $scope.stores[s];

        $scope.prices[store.Alias] = [];

        for (c = 0; store.Currencies.length > c; c += 1) {

          $scope.prices[store.Alias].push({
            Currency: store.Currencies[c].CurrencyValue,
            Price: parseFloat($scope.model.value.replace(/,/g, '.'))
          });

        }

      }

    }

    // Reset Prices if currency is not included in the current model
    Object.entries($scope.prices).forEach(([storeAlias, storeArr], priceIndex) => {
      const store = $scope.stores.find(store => store.Alias === storeAlias)
      if (store) {

        const validCurr = store.Currencies.map(curr => curr.CurrencyValue);
        const storeArrValues = storeArr.map(curr => curr.Currency);

        storeArr.forEach((priceCurrency, i) => {

          if (!validCurr.includes(priceCurrency.Currency)) {

            $scope.prices[storeAlias].splice($scope.prices[storeAlias].indexOf($scope.prices[storeAlias][i]), 1);

          }

        })

        validCurr.forEach((curr, i) => {

          if (!storeArrValues.includes(curr)) {

            $scope.prices[store.Alias].push({
              Currency: curr,
              Price: 0
            });

          }

        })

      }
    })

    if ($scope.prices === 'null' || $scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {

      $scope.prices = {};

      for (s = 0; $scope.stores.length > s; s += 1) {

        let store = $scope.stores[s];

        $scope.prices[store.Alias] = [];

        for (c = 0; store.Currencies.length > c; c += 1) {

          $scope.prices[store.Alias].push({
            Currency: store.Currencies[c].CurrencyValue,
            Price: 0
          });

        }

      }

    }

  });

  $scope.$on("formSubmitting", function () {

    $scope.model.value = $scope.prices;
  });

});
