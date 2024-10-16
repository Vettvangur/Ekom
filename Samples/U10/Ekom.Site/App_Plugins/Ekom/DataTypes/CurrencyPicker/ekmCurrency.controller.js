angular.module("umbraco").controller("Ekom.Currency", function ($scope, $routeParams) {

  if ($routeParams.section !== 'content') { return; }

  $scope.currencies = $scope.model.value;
  $scope.model.hideLabel = false;

  if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {
    $scope.currencies = [];
  }

  $scope.currencyCulture = '';
  $scope.currencyFormat = '';

  $scope.addCurrency = function () {
    event.preventDefault();

    if ($scope.currencyCulture !== '' && $scope.currencyFormat !== '') {

      var currencyItem = { "currencyFormat": $scope.currencyFormat, "currencyValue": $scope.currencyCulture, "sort": $scope.currencies.length };

      $scope.currencies.push(currencyItem);
    }

  };

  $scope.combine = function (item) {
    return "Culture: " + item.currencyValue + " Format: " + item.currencyFormat;
  };

  $scope.removeCurrency = function (itemToRemove) {
    event.preventDefault();

    var idx = $scope.currencies.indexOf(itemToRemove);

    $scope.currencies.splice(idx, 1);

    $scope.updateSort();
  };

  $scope.updateSort = function () {

    for (i = 0; $scope.currencies.length > i; i += 1) {

      $scope.currencies[i].Sort = i;

    }

  };

  $scope.$watch('currencies', function () {

    $scope.model.value = $scope.currencies;

  }, true);

});
