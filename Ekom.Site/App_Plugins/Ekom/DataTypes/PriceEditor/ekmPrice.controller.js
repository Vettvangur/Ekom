angular.module("umbraco").controller("Ekom.Price", function($scope, $http) {
    $scope.fieldAlias = $scope.model.alias;

    $scope.stores = [];

    $http.get('/umbraco/backoffice/ekom/api/getAllStores').then(function(results) {

        $scope.stores = results.data;

        // Set default prices value from existing value
        if (typeof $scope.model.value === 'object' && $scope.model.value !== null && $scope.model.value !== '') {
            // If model value is json then return

            if ($scope.model.value.hasOwnProperty('values')) {

                var temp1 = $scope.model.value.values;

                Object.keys(temp1).forEach(key => temp1[key] = JSON.parse(temp1[key]));

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

        if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {

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

    $scope.$on("formSubmitting", function() {

        $scope.model.value = $scope.prices;
    });

});
