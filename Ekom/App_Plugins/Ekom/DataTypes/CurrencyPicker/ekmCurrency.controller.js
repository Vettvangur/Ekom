angular.module("umbraco").controller("Ekom.Currency", function($scope, assetsService, $http) {

    $scope.currencies = $scope.model.value;

    if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {
        $scope.currencies = [];
    }

    $scope.currencyCulture = '';
    $scope.currencyFormat = '';

    $scope.addCurrency = function() {
        event.preventDefault();

        if ($scope.currencyCulture !== '' && $scope.currencyFormat !== '') {

            var currencyItem = { "CurrencyFormat": $scope.currencyFormat, "CurrencyValue": $scope.currencyCulture, "Sort": $scope.currencies.length };

            $scope.currencies.push(currencyItem);
        }

    };

    $scope.combine = function(item) {
        return "Culture: " + item.CurrencyValue + " Format: " + item.CurrencyFormat;
    };

    $scope.removeCurrency = function(itemToRemove) {
        event.preventDefault();

        var idx = $scope.currencies.indexOf(itemToRemove);

        $scope.currencies.splice(idx, 1);

        $scope.updateSort();
    };

    $scope.updateSort = function() {

        for (i = 0; $scope.currencies.length > i; i += 1) {

            $scope.currencies[i].Sort = i;

        }

    };

    $scope.$watch('currencies', function() {

        $scope.model.value = $scope.currencies;

    }, true);

});
