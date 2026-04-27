(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $document, eventsService, state, $timeout) {
    var managerState = state.getState();
    var activityLogPreviewCharacterLimit = 180;
    var activityLogTypeInfo = 0;
    var activityLogTypeSuccess = 1;
    var activityLogTypeAlert = 2;
    var orderId = $scope.model && $scope.model.editModel && $scope.model.editModel.order && $scope.model.editModel.order.uniqueId;

    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    $scope.statusList = managerState.statusList;
    $scope.activityLogs = [];
    $scope.activityLogsLoading = false;
    $scope.activityLogsError = false;
    $scope.activityLogExpandedStates = {};
    $scope.isTrackingExpanded = false;
    $scope.isConsentExpanded = false;
    $scope.orderActions = [];
    $scope.orderActionsLoading = false;
    $scope.executingOrderActionKey = null;
    $scope.ga4TrackingData = [];
    $scope.metaTrackingData = [];

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

    function parseProperties(properties, predicate) {
      return Object.entries(properties || {})
        .filter(function (entry) {
          var key = entry[0];
          var value = entry[1];

          if (!value) {
            return false;
          }

          return predicate(key, value);
        })
        .map(function (entry) {
          return [entry[0], htmlDecode(entry[1])];
        });
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

    function applyOrderData(order) {
      var tracking = (order && order.tracking) || null;
      var orderLines = (order && order.orderLines) || [];
      var shippingProps = order.customerInformation.shipping.properties || {};
      var customerProps = order.customerInformation.customer.properties || {};
      var customShippingProps = (order.shippingProvider && order.shippingProvider.customData) || {};
      var customPaymentProps = (order.paymentProvider && order.paymentProvider.customData) || {};

      $scope.model.editModel.order = order;
      $scope.ga4TrackingData = tracking && tracking.ga4 && tracking.ga4.data ? Object.entries(tracking.ga4.data) : [];
      $scope.metaTrackingData = tracking && tracking.meta && tracking.meta.data ? Object.entries(tracking.meta.data) : [];

      orderLines.forEach(function (orderLine) {
        orderLine.displayProperties = getOrderLineProperties(orderLine);
      });

      $scope.extraShippingProperties = parseProperties(shippingProps, function (key) {
        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("shipping") && !$scope.isDefaultKey(normalisedKey);
      });

      $scope.extraCustomerProperties = parseProperties(customerProps, function (key) {
        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("customer") && !$scope.isDefaultKey(normalisedKey);
      });

      $scope.extraCustomShippingProperties = parseProperties(customShippingProps, function (key) {
        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("customshipping") && !$scope.isDefaultKey(normalisedKey);
      });

      $scope.extraCustomPaymentProperties = parseProperties(customPaymentProps, function (key) {
        var normalisedKey = (key || "").toLowerCase();
        return normalisedKey.startsWith("custompayment") && !$scope.isDefaultKey(normalisedKey);
      });
    }

    function loadOrder() {
      if (!orderId) {
        return Promise.resolve();
      }

      return resources.OrderInfo(orderId)
        .then(function (result) {
          applyOrderData(result.data);
        }, function (error) {
          window.alert(getErrorMessage(error, "Failed to reload order."));
        });
    }

    function loadOrderActions() {
      if (!orderId) {
        return Promise.resolve();
      }

      $scope.orderActionsLoading = true;

      return resources.OrderActions(orderId)
        .then(function (result) {
          $scope.orderActions = result.data || [];
        }, function (error) {
          $scope.orderActions = [];
          window.alert(getErrorMessage(error, "Failed to load order actions."));
        })
        .finally(function () {
          $scope.orderActionsLoading = false;
        });
    }

    function readBlobText(blob) {
      if (!blob) {
        return Promise.resolve("");
      }

      if (typeof blob.text === "function") {
        return blob.text();
      }

      return new Promise(function (resolve, reject) {
        var reader = new FileReader();
        reader.onload = function () {
          resolve(reader.result || "");
        };
        reader.onerror = reject;
        reader.readAsText(blob);
      });
    }

    function tryParseMessage(text) {
      if (!text) {
        return "";
      }

      try {
        var data = JSON.parse(text);
        return data && data.message ? data.message : text;
      } catch (error) {
        return text;
      }
    }

    function getErrorMessage(response, fallbackMessage) {
      if (!response || !response.data) {
        return fallbackMessage;
      }

      if (typeof response.data === "string") {
        return response.data || fallbackMessage;
      }

      if (response.data && typeof response.data.message === "string") {
        return response.data.message || fallbackMessage;
      }

      return fallbackMessage;
    }

    function isFileResponse(response) {
      var contentDisposition = (response.headers("content-disposition") || "").toLowerCase();
      var contentType = (response.headers("content-type") || "").toLowerCase();

      return contentDisposition.indexOf("filename=") !== -1 ||
        contentType.indexOf("application/pdf") === 0 ||
        contentType.indexOf("application/octet-stream") === 0 ||
        contentType.indexOf("image/") === 0;
    }

    function refreshOrderView() {
      return loadOrder()
        .then(function () {
          loadActivityLogs();
          return loadOrderActions();
        });
    }

    $scope.getOrderActionButtonClass = function (action) {
      if (action && action.look === "primary") {
        return "btn-success";
      }

      return "btn-outline umb-outline";
    };

    $scope.executeOrderAction = function (action) {
      if (!action || !action.key || $scope.executingOrderActionKey) {
        return;
      }

      if (action.confirmMessage && !window.confirm(action.confirmMessage)) {
        return;
      }

      $scope.executingOrderActionKey = action.key;

      resources.ExecuteOrderAction(orderId, action.key)
        .then(function (response) {
          if (isFileResponse(response)) {
            var fileUrl = window.URL.createObjectURL(response.data);
            window.open(fileUrl, "_blank");
            return;
          }

          return readBlobText(response.data)
            .then(function (text) {
              var message = tryParseMessage(text);

              if (message) {
                notificationsService.success("Success", message);
              }

              return refreshOrderView();
            });
        }, function (error) {
          return readBlobText(error.data)
            .then(function (text) {
              var message = tryParseMessage(text) || getErrorMessage(error, "Order action failed.");
              notificationsService.error("Error", message);
            });
        })
        .finally(function () {
          $scope.executingOrderActionKey = null;
        });
    };

    applyOrderData($scope.model.editModel.order);

    $scope.$watch("model.editModel.order.customerInformation.customer.properties", function (props) {
      var liveCustomerProps = props || {};

      $scope.extraCustomerProperties = parseProperties(liveCustomerProps, function (key) {
        return key.toLowerCase().startsWith("customer") && !$scope.isDefaultKey(key);
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

    $scope.hasConsentData = function () {
      var consent = $scope.model.editModel.order && $scope.model.editModel.order.consent;

      if (!consent) {
        return false;
      }

      return !!(
        consent.resolvedAtUtc ||
        consent.source ||
        consent.analytics !== null && consent.analytics !== undefined ||
        consent.marketing !== null && consent.marketing !== undefined
      );
    };

    $scope.toggleConsent = function () {
      $scope.isConsentExpanded = !$scope.isConsentExpanded;
    };

    $scope.getConsentValueLabel = function (value) {
      if (value === true) {
        return "Yes";
      }

      if (value === false) {
        return "No";
      }

      return "Unknown";
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
    loadOrderActions();

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
