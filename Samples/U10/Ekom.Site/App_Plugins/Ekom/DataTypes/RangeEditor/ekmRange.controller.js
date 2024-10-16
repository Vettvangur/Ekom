angular.module("umbraco").controller("Ekom.Range", function ($scope, $http, $routeParams) {

  if ($routeParams.section !== 'content') { return; }

  $scope.model.hideLabel = false;
  $scope.fieldAlias = $scope.model.alias;

  //$scope.currencies = [];
  $scope.stores = [];

  $http.get(Umbraco.Sys.ServerVariables.ekom.apiEndpoint + 'Stores').then(function (results) {

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

    if ($scope.model.value === null || $scope.model.value === '' || $scope.model.value === undefined) {

      $scope.ranges = {};

      for (s = 0; $scope.stores.length > s; s += 1) {

        let store = $scope.stores[s];

        $scope.ranges[store.alias] = [];

        for (c = 0; store.currencies.length > c; c += 1) {

          $scope.ranges[store.alias].push({
            currency: store.currencies[c].currencyValue,
            value: 0
          });

        }

      }

    }
  });

  $scope.$on("formSubmitting", function () {

    $scope.model.value = $scope.ranges;
  });

});
