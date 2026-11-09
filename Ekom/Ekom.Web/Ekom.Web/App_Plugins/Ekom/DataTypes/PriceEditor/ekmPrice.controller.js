(function () {
  "use strict";

  angular.module("umbraco").controller("Ekom.Price", [
    '$scope',
    'Ekom.Resources',
    '$routeParams',
    '$http',
    function ($scope, ekmResources, $routeParams, $http) {

      if ($routeParams.section !== 'content') { return; }

      $scope.model.hideLabel = false;

      $scope.fieldAlias = $scope.model.alias;

      let currentPrices = angular.copy($scope.model.value) || {};

      // Backward Compatability
      for (let key in currentPrices) {
        if (key === 'undefined') {
          delete currentPrices[key];
        }
      }

      $scope.stores = [];
      $scope.prices = {};

      var priceStructure = {};
      var nodePrices = {};


      ekmResources.getStoresByNode($routeParams.id).then(function (stores) {
        $scope.stores = stores;

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
          } else {
            console.log('Valid format on price object: ' + JSON.stringify(currentPrices));
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

        if (currentPrices !== null || currentPrices !== undefined || currentPrices !== '') {

          for (let storeAlias in currentPrices) {

            const store = $scope.stores.find(store => store.alias === storeAlias)

            if (store) {

              var priceObjs = currentPrices[storeAlias];

              priceObjs.forEach((obj) => {

                updatePrice(priceStructure, storeAlias, obj.Currency, obj.Price);
                var item = priceStructure[storeAlias].find(p => p.Currency === obj.Currency);
                if (item && obj.EditId) item.EditId = obj.EditId;

              });

            }

          }

        }

        if (currentPrices !== null && currentPrices !== undefined && currentPrices !== '') {
          for (let storeAlias in currentPrices) {
            const storePrices = currentPrices[storeAlias];
            if (Array.isArray(storePrices)) {
              storePrices.forEach(p => {
                if (p.Price === null || p.Price === undefined || p.Price === '') {
                  p.Price = 0;
                }
              });
            }
          }
        }

        nodePrices = angular.copy(currentPrices);
        var endpoint = Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint;
        $scope.priceUrl = endpoint + 'PriceEditor/' + encodeURIComponent($routeParams.id) + '/' + encodeURIComponent($scope.fieldAlias);
        $http.get(endpoint + 'Config').then(function (response) {
          $scope.databasePrices = response.data.useDatabasePrices === true;
          if (!$scope.databasePrices) { $scope.prices = priceStructure; return; }
          return $http.get($scope.priceUrl).then(function (result) {
            result.data.prices.forEach(function (row) {
              var store = Object.keys(priceStructure).find(function (key) { return key.toLowerCase() === row.storeAlias.toLowerCase(); });
              var price = store && priceStructure[store].find(function (item) { return item.Currency.toLowerCase() === row.currency.toLowerCase(); });
              if (price && !price.EditId) price.Price = row.price;
            });
            $scope.prices = priceStructure;
            $scope.priceStatus = 'Save the document to update prices. Saving makes price changes live.';
          });
        }).catch(function () { $scope.priceStatus = 'Could not load prices. Reload before editing.'; });
      });

      $scope.changePrice = function (storeAlias, currency, price) {
        if (!$scope.databasePrices) return;
        if (price == null || !Number.isFinite(price) || price < 0) {
          $scope.priceStatus = 'Enter a non-negative price.';
          return;
        }
        var rows = nodePrices[storeAlias] || (nodePrices[storeAlias] = []);
        var row = rows.find(item => item.Currency === currency);
        if (!row) { row = { Currency: currency }; rows.push(row); }
        row.Price = price;
        row.EditId = window.crypto.randomUUID();
        $scope.model.value = angular.copy(nodePrices);
        $scope.priceStatus = 'Price changed. Save the document to make it live.';
      };

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
              obj[storeName][i].Price = newPrice == null || newPrice === '' ? 0 : newPrice;
            }
          }
        }
        return obj;
      }

      $scope.$on("formSubmitting", function () {
        if ($scope.databasePrices) return;
        for (let storeAlias in $scope.prices) {
          $scope.prices[storeAlias].forEach(p => {
            if (p.Price == null || p.Price === '') {
              p.Price = 0;
            }
          });
        }
        $scope.model.value = $scope.prices;
      });


    }]
  );

})();
