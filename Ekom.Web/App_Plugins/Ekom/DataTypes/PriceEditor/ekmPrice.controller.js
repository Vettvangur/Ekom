angular.module("umbraco").controller("Ekom.Price", function ($scope, $http, $routeParams) {

  if ($routeParams.section !== 'content') { return; }

  $scope.model.hideLabel = false;

  $scope.fieldAlias = $scope.model.alias;

  let currentPrices = angular.copy($scope.model.value);

  // Backward Compatability
  for (let key in currentPrices) {
    if (key === 'undefined') {
      delete currentPrices[key];
    }
  }

  $scope.stores = [];
  $scope.prices = {};

  var priceStructure = {};

  $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'Stores').then(function (results) {

    $scope.stores = results.data;

    // Backward Compatability
    // {"Store":{"0":{"Price":32}}}
    // {"values":{"IS":"1355"},"dtdGuid":"26cc6028-5c7f-49ca-bd3c-145709efd777"}
    if (currentPrices !== null || currentPrices !== undefined || currentPrices !== '') {

      var formatValid = checkFormat(currentPrices);

      if (!formatValid) {
        console.log('Not valid format on price object: ' + JSON.stringify(currentPrices));

        var transformedObject = transformObject(currentPrices, $scope.stores);

        console.log('Transformed object: ' + JSON.stringify(transformedObject));

        currentPrices = transformedObject;
      }
    }

    $scope.stores.forEach((store, storeIndex) => {

      priceStructure[store.alias] = [];

      store.currencies.forEach((currency, currencyIndex) => {

        priceStructure[store.alias].push({
          Currency: currency.currencyValue,
          Price: 0
        });

      });

    });

    //$scope.prices = priceStructure;

    if (currentPrices !== null || currentPrices !== undefined || currentPrices !== '') {

      for (let storeAlias in currentPrices) {

        const store = $scope.stores.find(store => store.alias === storeAlias)

        if (store) {

          var priceObjs = currentPrices[storeAlias];

          priceObjs.forEach((obj) => {

            updatePrice(priceStructure, storeAlias, obj.Currency, obj.Price);

          });

        }

      }

    }

    $scope.prices = priceStructure;
  });

  function checkFormat(obj) {
    const keys = Object.keys(obj);
    for (let key of keys) {
      if (obj[key] && typeof obj[key] === 'object') {
        for (let i = 0; i < obj[key].length; i++) {
          let subObj = obj[key][i];
          if (!subObj.hasOwnProperty('Price') || !subObj.hasOwnProperty('Currency')) {
            return false;
          }
        }
      } else {
        return false;
      }
    }
    return true;
  }

  function transformObject(obj, stores) {

    const keys = Object.keys(obj);
    for (let key of keys) {
      if (obj[key] && typeof obj[key] === 'object') {
        const subKeys = Object.keys(obj[key]);
        const prices = subKeys.map((subKey) => {
          let currency = stores[0].currencies[0].currencyValue; // assuming "en-US" as the default currency
          if (obj[key][subKey].Price !== undefined) { // handle the case when Price is nested
            return {
              "Currency": currency,
              "Price": obj[key][subKey].Price
            };
          } else { // handle the case when Price is direct value
            return {
              "Currency": currency,
              "Price": obj[key][subKey]
            };
          }
        });
        let result = {};
        result[key] = prices;
        return result;
      }
    }

    return obj;
  }

  function updatePrice(obj, storeName, currency, newPrice) {
    // Check if the storeName exists in the object
    if (obj[storeName]) {
      // Loop through the array for the matching store
      for (let i = 0; i < obj[storeName].length; i++) {
        // If the currency matches, update the price
        if (obj[storeName][i].Currency === currency) {
          obj[storeName][i].Price = newPrice;
        }
      }
    }
    return obj;
  }
  $scope.$on("formSubmitting", function () {
    $scope.model.value = $scope.prices;
  });

});
