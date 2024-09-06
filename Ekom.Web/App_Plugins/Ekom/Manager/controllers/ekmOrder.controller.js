(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $location, $document, eventsService, $rootScope) {
    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    
    $scope.statusList = $rootScope.sharedData.statusList;
    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, status) {

      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];

      $scope.labelDropdowns[dropdownId] = status;

      if (dropdownId === 'dropdownOrderStatusList') {
        $scope.orderChangeStatus = $scope.getStatus(status);
      }

    };

    $scope.labelDropdown = function (dropdownId, defaultText) {

      var label = $scope.labelDropdowns[dropdownId];

      label = label || defaultText;

      if (dropdownId == 'dropdownPaymentProvider' && $rootScope.sharedData.paymentProvider !== '') {
        const provider = $scope.filterPaymentProviders.find(obj => obj.key === $rootScope.sharedData.paymentProvider);

        if (provider) {

          const providerName = provider.title;

          return providerName;
        }
      }

      if (dropdownId === 'dropdownOrderStatusList') {
        $scope.orderChangeStatus = $scope.getStatus(label);

        return $scope.getStatusLabel(label);
      }

      return label;

    };
    $scope.getStatus = function (value) {

      const item = $scope.statusList.find(obj => obj.value === value);

      if (item) {
        return item;
      }
      return null;

    }
    $scope.getStatusLabel = function (value) {

      const item = $scope.statusList.find(obj => obj.value === value);

      if (item) {
        return item.label;
      }
      return value;

    }

    $scope.orderChangeStatus = $scope.getStatus($rootScope.sharedData.orderStatus);

    var changeOrderStatusButton = document.getElementById('changeOrderStatusButton');

    if (changeOrderStatusButton) {
      changeOrderStatusButton.addEventListener('click', function () {

        var notify = document.getElementById('notifyOrderStatus');

        resources.ChangeOrderStatus('?orderId=' + changeOrderStatusButton.getAttribute('data-orderId') + '&orderStatus=' + $scope.orderChangeStatus.value + '&notify=' + notify.checked)
          .then(function (result) {

            notificationsService.success("Success", "Order status updated.");

            eventsService.emit("order.changed", {

            });
          }, function errorCallback(data) {
            notificationsService.error("Error", "Error updating order status.");
          })

      });
    }

  }

  angular.module("umbraco").controller("Ekom.Manager.Order", [
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
