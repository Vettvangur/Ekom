(function () {
  "use strict";

  function controller($scope, eventsService, state) {
    var managerState = state.getState();

    $scope.filters = managerState.filters;
    $scope.filterPaymentProviders = managerState.paymentProviders;
    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};

    function emitFilterChange() {
      eventsService.emit("filter.changed", {
        paymentProvider: managerState.filters.paymentProvider,
        productSku: managerState.filters.productSku,
        trackingSource: managerState.filters.trackingSource,
        trackingMedium: managerState.filters.trackingMedium,
        trackingCampaign: managerState.filters.trackingCampaign,
        trackingTerm: managerState.filters.trackingTerm,
        trackingContent: managerState.filters.trackingContent,
        trackingClickId: managerState.filters.trackingClickId
      });
    }

    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, value) {
      $scope.visibleDropdowns[dropdownId] = false;
      $scope.labelDropdowns[dropdownId] = value;

      if (dropdownId === "dropdownPaymentProvider") {
        state.setPaymentProvider(value);
        emitFilterChange();
      }
    };

    $scope.onProductSkuChanged = function () {
      state.setProductSku(($scope.filters.productSku || "").trim());
      emitFilterChange();
    };

    $scope.onTrackingFieldChanged = function (field) {
      state.setTrackingField(field, ($scope.filters[field] || "").trim());
      emitFilterChange();
    };

    $scope.labelDropdown = function (dropdownId, defaultText) {
      var label = $scope.labelDropdowns[dropdownId] || defaultText;

      if (managerState.filters.paymentProvider) {
        var provider = $scope.filterPaymentProviders.find(function (item) {
          return item.key === managerState.filters.paymentProvider;
        });

        if (provider) {
          return provider.title;
        }
      }

      return label;
    };
  }

  angular.module("umbraco").controller("Ekom.Manager.Filter", [
    "$scope",
    "eventsService",
    "Ekom.Manager.State",
    controller
  ]);
})();
