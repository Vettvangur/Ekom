angular.module("umbraco").controller("Ekom.Range", function ($scope, ekmResources, $routeParams) {

  if ($routeParams.section !== 'content') { return; }

  $scope.model.hideLabel = false;
  $scope.fieldAlias = $scope.model.alias;
  $scope.stores = [];
  $scope.ranges = {};

  ekmResources.getStoresByNode($routeParams.id).then(function (stores) {

    $scope.stores = stores;
    var currentRanges = normalizeRanges($scope.model.value, stores);

    stores.forEach(function (store) {
      $scope.ranges[store.alias] = [];

      store.currencies.forEach(function (currency) {
        var range = getRange(currentRanges[store.alias], currency.currencyValue);

        $scope.ranges[store.alias].push({
          currency: currency.currencyValue,
          value: range === undefined ? 0 : range
        });
      });
    });
  });

  function normalizeRanges(value, stores) {
    if (value === null || value === undefined || value === '') {
      return {};
    }

    if (typeof value !== 'object') {
      var firstStore = stores[0];
      var firstCurrency = firstStore && firstStore.currencies[0];

      if (!firstStore || !firstCurrency) {
        return {};
      }

      var primitiveRanges = {};
      primitiveRanges[firstStore.alias] = [{
        currency: firstCurrency.currencyValue,
        value: parseRange(value)
      }];
      return primitiveRanges;
    }

    var source = value.values && typeof value.values === 'object'
      ? value.values
      : value;
    var ranges = {};

    Object.keys(source).forEach(function (storeAlias) {
      var storeRanges = source[storeAlias];

      if (typeof storeRanges === 'string') {
        try {
          storeRanges = JSON.parse(storeRanges);
        } catch (error) {
          storeRanges = [];
        }
      }

      if (!Array.isArray(storeRanges)) {
        return;
      }

      ranges[storeAlias] = storeRanges;
    });

    return ranges;
  }

  function getRange(ranges, currency) {
    if (!Array.isArray(ranges)) {
      return undefined;
    }

    var range = ranges.find(function (item) {
      return item && (item.currency === currency || item.Currency === currency);
    });

    if (!range) {
      return undefined;
    }

    return parseRange(range.value === undefined ? range.Value : range.value);
  }

  function parseRange(value) {
    var parsed = parseFloat(String(value).replace(/,/g, '.'));
    return isNaN(parsed) ? 0 : parsed;
  }

  $scope.$on("formSubmitting", function () {

    $scope.model.value = $scope.ranges;
  });

});
