(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $location, $document, eventsService, $rootScope) {

    var sharedData = $rootScope.sharedData;

    $scope.store = sharedData.store;
    $scope.filterPaymentProviders = sharedData.paymentProviders;

    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};

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

        $rootScope.sharedData.paymentProvider = status;

        eventsService.emit("filter.changed", {
          paymentProvider: status
        });

      }

    };

    $scope.labelDropdown = function (dropdownId, defaultText) {

      var label = $scope.labelDropdowns[dropdownId];

      label = label || defaultText;

      if ($rootScope.sharedData.paymentProvider !== '') {
        const provider = $scope.filterPaymentProviders.find(obj => obj.key === $rootScope.sharedData.paymentProvider);

        if (provider) {

          const providerName = provider.title;

          return providerName;
        }
      }

      return label;

    };

  }

  angular.module("umbraco").controller("Ekom.Manager.Filter", [
    "$scope",
    "notificationsService",
    "Ekom.Manager.Resources",
    "$location",
    "$document",
    'eventsService',
    '$rootScope',
    controller
  ]);
})();
