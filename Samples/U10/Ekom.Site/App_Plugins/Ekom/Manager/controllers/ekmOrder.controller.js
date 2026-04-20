(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $document, eventsService, state, $timeout) {
    var managerState = state.getState();
    var activityLogPreviewCharacterLimit = 180;
    var activityLogTypeInfo = 0;
    var activityLogTypeSuccess = 1;
    var activityLogTypeAlert = 2;

    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    $scope.statusList = managerState.statusList;
    $scope.activityLogs = [];
    $scope.activityLogsLoading = false;
    $scope.activityLogsError = false;
    $scope.activityLogExpandedStates = {};
    $scope.isTrackingExpanded = false;

    var tracking = ($scope.model.editModel.order && $scope.model.editModel.order.tracking) || null;
    $scope.ga4TrackingData = tracking && tracking.ga4 && tracking.ga4.data ? Object.entries(tracking.ga4.data) : [];
    $scope.metaTrackingData = tracking && tracking.meta && tracking.meta.data ? Object.entries(tracking.meta.data) : [];

    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, status) {
      $scope.visibleDropdowns[dropdownId] = false;
      $scope.labelDropdowns[dropdownId] = status;

      if (dropdownId === "dropdownOrderStatusList") {
        $scope.orderChangeStatus = $scope.getStatus(status);
      }
    };

    $scope.labelDropdown = function (dropdownId, defaultText) {
      var label = $scope.labelDropdowns[dropdownId] || defaultText;

      if (dropdownId === "dropdownOrderStatusList") {
        $scope.orderChangeStatus = $scope.getStatus(label);
        return $scope.getStatusLabel(label);
      }

      return label;
    };

    $scope.getStatus = function (value) {
      var item = $scope.statusList.find(function (status) {
        return status.value === value || status.enumValue === value;
      });

      return item || null;
    };

    $scope.getStatusLabel = function (value) {
      var item = $scope.statusList.find(function (status) {
        return status.value === value || status.enumValue === value;
      });

      return item ? item.label : value;
    };

    $scope.orderChangeStatus = $scope.getStatus(managerState.currentOrderStatus);

    var changeOrderStatusButton = document.getElementById("changeOrderStatusButton");

    if (changeOrderStatusButton) {
      changeOrderStatusButton.addEventListener("click", function () {
        var notify = document.getElementById("notifyOrderStatus");

        resources.ChangeOrderStatus({
          orderId: changeOrderStatusButton.getAttribute("data-orderId"),
          orderStatus: $scope.orderChangeStatus.value,
          notify: notify.checked
        })
          .then(function () {
            $scope.model.editModel.order.orderStatus = $scope.orderChangeStatus.value;
            loadActivityLogs();
            notificationsService.success("Success", "Order status updated.");
            eventsService.emit("order.changed", {});
          }, function () {
            notificationsService.error("Error", "Error updating order status.");
          });
      });
    }

    $scope.isDefaultKey = function (key) {
      var normalisedKey = (key || "").toString().toLowerCase();
      var defaults = new Set([
        "shippingname",
        "shippingaddress",
        "shippingcity",
        "shippingcountry",
        "shippingzipcode",
        "shippingphone",
        "customeremail",
        "customername",
        "customeraddress",
        "customercity",
        "customercountry",
        "customerzipcode",
        "customerphone"
      ]);

      return defaults.has(normalisedKey);
    };

    $scope.cleanKey = function (key) {
      return key.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
    };

    function htmlDecode(value) {
      var textArea = document.createElement("textarea");
      textArea.innerHTML = value;
      return textArea.value;
    }

    function formatOrderLinePropertyKey(key) {
      var cleanedKey = (key || "").replace(/^orderline/i, "");
      var spacedKey = cleanedKey.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();

      if (!spacedKey) {
        return "";
      }

      return spacedKey.charAt(0).toUpperCase() + spacedKey.slice(1);
    }

    function getOrderLineProperties(orderLine) {
      var properties = orderLine && orderLine.orderLineInfo && orderLine.orderLineInfo.properties
        ? orderLine.orderLineInfo.properties
        : {};

      return Object.entries(properties)
        .filter(function (entry) {
          return !!entry[1];
        })
        .map(function (entry) {
          return {
            key: formatOrderLinePropertyKey(entry[0]),
            value: htmlDecode(entry[1])
          };
        })
        .filter(function (entry) {
          return !!entry.key;
        });
    }

    var orderLines = ($scope.model.editModel.order && $scope.model.editModel.order.orderLines) || [];

    orderLines.forEach(function (orderLine) {
      orderLine.displayProperties = getOrderLineProperties(orderLine);
    });

    var shippingProps = $scope.model.editModel.order.customerInformation.shipping.properties || {};

    $scope.extraShippingProperties = Object.entries(shippingProps)
      .filter(function (entry) {
        var key = entry[0];
        var value = entry[1];

        if (!value) {
          return false;
        }

        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("shipping") && !$scope.isDefaultKey(normalisedKey);
      })
      .map(function (entry) {
        return [entry[0], htmlDecode(entry[1])];
      });

    var customerProps = $scope.model.editModel.order.customerInformation.customer.properties || {};

    $scope.extraCustomerProperties = Object.entries(customerProps)
      .filter(function (entry) {
        var key = entry[0];
        var value = entry[1];

        if (!value) {
          return false;
        }

        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("customer") && !$scope.isDefaultKey(normalisedKey);
      })
      .map(function (entry) {
        return [entry[0], htmlDecode(entry[1])];
      });

    var customShippingProps = ($scope.model.editModel.order.shippingProvider && $scope.model.editModel.order.shippingProvider.customData) || {};

    $scope.extraCustomShippingProperties = Object.entries(customShippingProps)
      .filter(function (entry) {
        var key = entry[0];
        var value = entry[1];

        if (!value) {
          return false;
        }

        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("customshipping") && !$scope.isDefaultKey(normalisedKey);
      })
      .map(function (entry) {
        return [entry[0], htmlDecode(entry[1])];
      });

    var customPaymentProps = ($scope.model.editModel.order.paymentProvider && $scope.model.editModel.order.paymentProvider.customData) || {};

    $scope.extraCustomPaymentProperties = Object.entries(customPaymentProps)
      .filter(function (entry) {
        var key = entry[0];
        var value = entry[1];

        if (!value) {
          return false;
        }

        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("custompayment") && !$scope.isDefaultKey(normalisedKey);
      })
      .map(function (entry) {
        return [entry[0], htmlDecode(entry[1])];
      });

    $scope.$watch("model.editModel.order.customerInformation.customer.properties", function (props) {
      var liveCustomerProps = props || {};

      $scope.extraCustomerProperties = Object.entries(liveCustomerProps)
        .filter(function (entry) {
          var key = entry[0];
          var value = entry[1];

          return value && key.toLowerCase().startsWith("customer") && !$scope.isDefaultKey(key);
        })
        .map(function (entry) {
          return [entry[0], htmlDecode(entry[1])];
        });
    });

    $scope.hasShippingInfo = function () {
      var shipping = $scope.model.editModel.order.customerInformation.shipping;

      if (!shipping) {
        return false;
      }

      return !!(
        shipping.name ||
        shipping.email ||
        shipping.address ||
        shipping.apartment ||
        shipping.city ||
        shipping.country ||
        shipping.zipCode ||
        shipping.phone
      );
    };

    $scope.hasTrackingData = function () {
      var orderTracking = $scope.model.editModel.order && $scope.model.editModel.order.tracking;

      if (!orderTracking) {
        return false;
      }

      return !!(
        orderTracking.source ||
        orderTracking.medium ||
        orderTracking.campaign ||
        orderTracking.term ||
        orderTracking.content ||
        orderTracking.clickId ||
        orderTracking.clickIdType ||
        orderTracking.landingUrl ||
        orderTracking.referrer ||
        orderTracking.captureMethod ||
        orderTracking.capturedAtUtc ||
        orderTracking.hasCookieSupport !== null && orderTracking.hasCookieSupport !== undefined ||
        (orderTracking.ga4 && (orderTracking.ga4.clientId || orderTracking.ga4.sessionId || $scope.ga4TrackingData.length > 0)) ||
        (orderTracking.meta && (orderTracking.meta.fbp || orderTracking.meta.fbc || $scope.metaTrackingData.length > 0))
      );
    };

    $scope.toggleTracking = function () {
      $scope.isTrackingExpanded = !$scope.isTrackingExpanded;
    };

    $scope.toggleActivityLog = function (index) {
      $scope.activityLogExpandedStates[index] = !$scope.activityLogExpandedStates[index];
    };

    $scope.isActivityLogExpanded = function (index) {
      return !!$scope.activityLogExpandedStates[index];
    };

    $scope.canExpandActivityLog = function (log) {
      return !!(log && log.message && log.message.length > activityLogPreviewCharacterLimit);
    };

    $scope.getActivityLogIcon = function (log) {
      switch ((log && log.logType)) {
        case activityLogTypeSuccess:
          return "✓";
        case activityLogTypeAlert:
          return "!";
        default:
          return "i";
      }
    };

    $scope.getActivityLogTypeClass = function (log) {
      switch ((log && log.logType)) {
        case activityLogTypeSuccess:
          return "ekmOrderActivityLog__icon--success";
        case activityLogTypeAlert:
          return "ekmOrderActivityLog__icon--alert";
        default:
          return "ekmOrderActivityLog__icon--info";
      }
    };

    function loadActivityLogs() {
      var order = $scope.model && $scope.model.editModel && $scope.model.editModel.order;

      if (!order || !order.uniqueId) {
        return;
      }

      $scope.activityLogsLoading = true;
      $scope.activityLogsError = false;

      resources.OrderLogs(order.uniqueId)
        .then(function (result) {
          $scope.activityLogs = result.data || [];
          $scope.activityLogExpandedStates = {};
        }, function () {
          $scope.activityLogs = [];
          $scope.activityLogsError = true;
        })
        .finally(function () {
          $scope.activityLogsLoading = false;
        });
    }

    loadActivityLogs();

    var printOrderButton = document.getElementById("printOrder");

    if (printOrderButton) {
      printOrderButton.addEventListener("click", function () {
        var linkEl = angular.element('<link id="overlay-print-style" rel="stylesheet" media="print" href="/app_plugins/ekom/manager/styles/ekmManagerOrderPrint.css">');
        angular.element($document[0].head).append(linkEl);

        var cleanup = function () {
          linkEl.remove();
          window.removeEventListener("afterprint", cleanup);
        };

        window.addEventListener("afterprint", cleanup);

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
    "$document",
    "eventsService",
    "Ekom.Manager.State",
    "$timeout",
    controller
  ]);
})();
