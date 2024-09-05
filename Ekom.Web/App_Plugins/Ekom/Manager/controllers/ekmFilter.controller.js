(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $location, $document, eventsService) {

    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    $scope.paymentProvider = '';
    $scope.paymentProviders = [];
    $scope.store = '';
    $scope.GetPaymentProviders = function () {

      resources.PaymentProviders($scope.store)
        .then(function (result) {

          $scope.paymentProviders = result.data;

        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on getting payment providers.");
        })
    };

    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, status) {

      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];

      $scope.labelDropdowns[dropdownId] = status;

      if (dropdownId === 'dropdownPaymentProvider') {
        $scope.paymentProvider = status;

        eventsService.emit("elements.updated", {
          paymentProvider: status
        });

      }

    };

    $scope.labelDropdown = function (dropdownId, defaultText) {

      var label = $scope.labelDropdowns[dropdownId];

      label = label || defaultText;

      if (dropdownId === 'dropdownPaymentProvider') {
        const provider = $scope.paymentProviders.find(obj => obj.key === label);

        if (provider) {
          return provider.title;
        }
      }

      return label;

    };

    $scope.GetStores = function () {

      resources.Stores()
        .then(function (result) {

          $scope.stores = result.data;
          $scope.store = $scope.stores[0].alias;

          $scope.GetPaymentProviders();
          
        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on getting stores.");
        })
    };

    $scope.GetStores();

  }

  angular.module("umbraco").controller("Ekom.Manager.Filter", [
    "$scope",
    "notificationsService",
    "Ekom.Manager.Resources",
    "$location",
    "$document",
    'eventsService',
    controller
  ]);
})();
