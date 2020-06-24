angular.module("umbraco").controller("Ekom.Range", function($scope, $http) {
    $scope.fieldAlias = $scope.model.alias;

    //$scope.currencies = [];
    $scope.stores = [];

    $http.get('/umbraco/backoffice/ekom/api/getAllStores').then(function(results) {

        $scope.stores = results.data;

        // Set default ranges value from existing value
        if (typeof $scope.model.value === 'object' && $scope.model.value !== null && $scope.model.value !== '') {
            // If model value is json then return

            if ($scope.model.value.hasOwnProperty('values')) {

                var temp1 = $scope.model.value.values;

                Object.keys(temp1).forEach(key => temp1[key] = JSON.parse(temp1[key]));

                $scope.ranges = temp1;

            } else {
                $scope.ranges = $scope.model.value;
            }

        } else {
            // If model value is not json then return as decimal
            if ($scope.model.value !== undefined) {
                $scope.ranges = $scope.model.value.replace(/,/g, '.');
            }
        }

        // Backward Compatability if value is decimal and not json
        if (isFinite($scope.ranges)) {

            $scope.ranges = {};

            for (s = 0; $scope.stores.length > s; s += 1) {

                let store = $scope.stores[s];

                $scope.ranges[store.Alias] = [];

                for (c = 0; store.Currencies.length > c; c += 1) {

                    $scope.ranges[store.Alias].push({
                        Currency: store.Currencies[c].CurrencyValue,
                        Value: parseFloat($scope.model.value.replace(/,/g, '.'))
                    });

                }

            }

        }

        if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {

            $scope.ranges = {};

            for (s = 0; $scope.stores.length > s; s += 1) {

                let store = $scope.stores[s];

                $scope.ranges[store.Alias] = [];

                for (c = 0; store.Currencies.length > c; c += 1) {

                    $scope.ranges[store.Alias].push({
                        Currency: store.Currencies[c].CurrencyValue,
                        Value: 0
                    });

                }

            }

        }

    });

    $scope.$on("formSubmitting", function() {

        $scope.model.value = $scope.ranges;
    });

});
