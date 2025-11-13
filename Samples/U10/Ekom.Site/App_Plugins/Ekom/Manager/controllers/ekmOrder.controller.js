(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $location, $document, eventsService, $rootScope, $timeout) {
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
    $scope.isDefaultKey = function (key) {
      const k = (key || "").toString().toLowerCase();
      const defaults = new Set([
        'shippingname',
        'shippingaddress',
        'shippingcity',
        'shippingcountry',
        'shippingzipcode',
        'shippingphone',
        'customeremail',
        'customername',
        'customeraddress',
        'customercity',
        'customercountry',
        'customerzipcode',
        'customerphone'
      ]);
      return defaults.has(k);
    };

    $scope.cleanKey = function (key) {
      return key.replace(/^customshipping/i, '').replace(/^custompayment/i, '').replace(/^shipping/i, '').replace(/^customer/i, '');
    };

    const shippingProps = $scope.model.editModel.order.customerInformation.shipping.properties || {};

    $scope.extraShippingProperties = Object.entries(shippingProps)
      .filter(([key, value]) => {
        if (!value) return false;
        const k = (key || "").toLowerCase();
        return k.startsWith("shipping") && !$scope.isDefaultKey(k);
      })
      .map(([key, value]) => [key, htmlDecode(value)]);

    const customerProps = $scope.model.editModel.order.customerInformation.customer.properties || {};

    $scope.extraCustomerProperties = Object.entries(customerProps)
      .filter(([key, value]) => {
        if (!value) return false;
        const k = (key || "").toLowerCase();
        return k.startsWith("customer") && !$scope.isDefaultKey(k);
      })
      .map(([key, value]) => [key, htmlDecode(value)]);


    const customShippingProps = $scope.model.editModel.order.shippingProvider.customData || {};

    $scope.extraCustomShippingProperties = Object.entries(customShippingProps)
      .filter(([key, value]) => {
        if (!value) return false;
        const k = (key || "").toLowerCase();
        return k.startsWith("customshipping") && !$scope.isDefaultKey(k);
      })
      .map(([key, value]) => [key, htmlDecode(value)]);

    const customPaymentProps = $scope.model.editModel.order.paymentProvider.customData || {};

    $scope.extraCustomPaymentProperties = Object.entries(customPaymentProps)
      .filter(([key, value]) => {
        if (!value) return false;
        const k = (key || "").toLowerCase();
        return k.startsWith("custompayment") && !$scope.isDefaultKey(k);
      })
      .map(([key, value]) => [key, htmlDecode(value)]);

    function htmlDecode(value) {
      const txt = document.createElement('textarea');
      txt.innerHTML = value;
      return txt.value;
    }

    $scope.$watch('model.editModel.order.customerInformation.customer.properties', function (props) {
      const customerProps = props || {};
      $scope.extraCustomerProperties = Object.entries(customerProps)
        .filter(([key, value]) =>
          value &&
          key.toLowerCase().startsWith('customer') &&
          !$scope.isDefaultKey(key)
        )
        .map(([key, value]) => [key, htmlDecode(value)]);
    });

    $scope.hasShippingInfo = function () {
      var s = $scope.model.editModel.order.customerInformation.shipping;
      if (!s) return false;

      return !!(
        s.name ||
        s.email ||
        s.address ||
        s.apartment ||
        s.city ||
        s.country ||
        s.zipCode ||
        s.phone
      );
    };

    var printOrderButton = document.getElementById('printOrder');

    if (printOrderButton) {

      printOrderButton.addEventListener('click', function () {

        var linkEl = angular.element('<link id="overlay-print-style" rel="stylesheet" media="print" href="/app_plugins/ekom/manager/styles/ekmManagerOrderPrint.css">');
        angular.element($document[0].head).append(linkEl);

        var cleanup = function () {
          linkEl.remove();
          window.removeEventListener('afterprint', cleanup);
        };

        window.addEventListener('afterprint', cleanup);

        $timeout(function () {
          window.print();

          $timeout(cleanup, 2000);
        }, 50);

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
    '$timeout',
    controller
  ]);
})();
