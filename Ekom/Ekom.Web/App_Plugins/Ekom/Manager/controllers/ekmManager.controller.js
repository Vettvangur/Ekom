(function () {
  "use strict";

  function controller($scope, notificationsService, resources, $location, $document, eventsService, state, chartService, $timeout, $q) {
    var managerState = state.getState();
    var unbindDocumentClick = angular.noop;
    var searchDebouncePromise = null;

    $scope.loading = true;
    $scope.loadingMostSoldProducts = true;
    $scope.result = {
      orders: [],
      totalPages: 0
    };
    $scope.filterOpen = false;
    $scope.orderCount = 0;
    $scope.grandTotal = 0;
    $scope.averageAmount = 0;
    $scope.page = 1;
    $scope.pageMostSoldProducts = 1;
    $scope.exportOptions = {
      includeOrderLines: false,
      exporting: false
    };
    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    $scope.filters = managerState.filters;
    $scope.statusList = managerState.statusList;
    $scope.stores = [];
    $scope.paymentProviders = managerState.paymentProviders;
    $scope.mostsoldproducts = [];
    $scope.mostSoldProductsCount = 0;
    $scope.mostSoldProductsTotalPages = 0;
    $scope.location = getLocationFromPath();

      eventsService.on("filter.changed", function (_, args) {
      state.setPaymentProvider(args.paymentProvider);
      state.setProductSku(args.productSku);
      state.setTrackingField("trackingSource", args.trackingSource);
      state.setTrackingField("trackingMedium", args.trackingMedium);
      state.setTrackingField("trackingCampaign", args.trackingCampaign);
      state.setTrackingField("trackingTerm", args.trackingTerm);
      state.setTrackingField("trackingContent", args.trackingContent);
      state.setTrackingField("trackingClickId", args.trackingClickId);
      $scope.page = 1;
      $scope.GetData();
    });

    eventsService.on("order.changed", function () {
      $scope.GetData();
    });

    function getLocationFromPath() {
      var path = $location.path();
      var pathComponents = path.split("/");
      var lastPathComponent = pathComponents[pathComponents.length - 1];

      if (lastPathComponent !== "analytics") {
        return "orders";
      }

      return lastPathComponent;
    }

    function syncState() {
      $scope.statusList = managerState.statusList;
      $scope.paymentProviders = managerState.paymentProviders;
    }

    function closeDropdowns() {
      angular.forEach($scope.visibleDropdowns, function (_, key) {
        $scope.visibleDropdowns[key] = false;
      });
    }

    function attachDocumentClickHandler() {
      unbindDocumentClick = function (event) {
        var isClickInsideDropdown = false;
        var dropdownElements = $document[0].querySelectorAll(".dropdown-toggle");

        for (var i = 0; i < dropdownElements.length; i++) {
          if (dropdownElements[i].contains(event.target)) {
            isClickInsideDropdown = true;
            break;
          }
        }

        if (!isClickInsideDropdown) {
          $scope.$applyAsync(closeDropdowns);
        }
      };

      $document.on("click", unbindDocumentClick);
    }

    function formatDateQueryValue(value) {
      if (!angular.isDate(value)) {
        return value;
      }

      var month = ("0" + (value.getMonth() + 1)).slice(-2);
      var day = ("0" + value.getDate()).slice(-2);

      return value.getFullYear() + "-" + month + "-" + day;
    }

    function buildOrdersQuery() {
      return {
        start: formatDateQueryValue($scope.filters.dateFrom),
        end: formatDateQueryValue($scope.filters.dateTo),
        orderStatus: $scope.filters.orderStatus,
        page: $scope.page,
        pageSize: 20,
        query: $scope.filters.query,
        store: $scope.filters.store,
        paymentProvider: $scope.filters.paymentProvider,
        productSku: $scope.filters.productSku,
        trackingSource: $scope.filters.trackingSource,
        trackingMedium: $scope.filters.trackingMedium,
        trackingCampaign: $scope.filters.trackingCampaign,
        trackingTerm: $scope.filters.trackingTerm,
        trackingContent: $scope.filters.trackingContent,
        trackingClickId: $scope.filters.trackingClickId
      };
    }

    function buildExportOrdersQuery() {
      var query = buildOrdersQuery();

      query.page = 1;
      query.pageSize = $scope.result.count || 0;

      return query;
    }

    function buildChartQuery() {
      return {
        start: formatDateQueryValue($scope.filters.dateFrom),
        end: formatDateQueryValue($scope.filters.dateTo),
        orderStatus: $scope.filters.orderStatus,
        store: $scope.filters.store
      };
    }

    function buildMostSoldProductsQuery() {
      return {
        start: formatDateQueryValue($scope.filters.dateFrom),
        end: formatDateQueryValue($scope.filters.dateTo),
        orderStatus: $scope.filters.orderStatus,
        store: $scope.filters.store,
        page: $scope.pageMostSoldProducts,
        pageSize: 20
      };
    }

    function cancelSearchDebounce() {
      if (searchDebouncePromise) {
        $timeout.cancel(searchDebouncePromise);
        searchDebouncePromise = null;
      }
    }

    function refreshCurrentView() {
      if (!$scope.filters.store) {
        return;
      }

      if ($scope.location === "analytics") {
        $scope.analytics();
        return;
      }

      $scope.GetPaymentProviders(false).finally(function () {
        $scope.GetData();
      });
    }

    function renderAnalyticsCharts(result) {
      chartService.render("chartRevenue", "Sales Revenue", result.revenueChart.labels, result.revenueChart.points, "rgba(30, 64, 175, 1)");
      chartService.render("chartOrders", "Total Orders", result.ordersChart.labels, result.ordersChart.points, "rgba(8, 145, 178, 1)");
      chartService.render("chartAvarage", "Average Order Value", result.avarageChart.labels, result.avarageChart.points, "rgba(217, 119, 6, 1)");
    }

    function loadCharts() {
      return resources.Charts(buildChartQuery())
        .then(function (result) {
          $timeout(function () {
            renderAnalyticsCharts(result.data);
          }, 0, false);
        }, function () {
          notificationsService.error("Error", "Error on chart data.");
        });
    }

    function loadMostSoldProducts() {
      $scope.loadingMostSoldProducts = true;

      return resources.MostSoldProducts(buildMostSoldProductsQuery())
        .then(function (result) {
          var data = result.data || {};

          $scope.mostsoldproducts = data.products || [];
          $scope.mostSoldProductsCount = data.count || 0;
          $scope.mostSoldProductsTotalPages = data.totalPages || 0;
          $scope.pageMostSoldProducts = data.page || $scope.pageMostSoldProducts;
        }, function () {
          notificationsService.error("Error", "Error on most sold products data.");
        })
        .finally(function () {
          $scope.loadingMostSoldProducts = false;
        });
    }

    $scope.GetData = function () {
      $scope.loading = true;

      return resources.SearchOrders(buildOrdersQuery())
        .then(function (result) {
          $scope.result = result.data;
          $scope.result.orders = $scope.result.orders || [];
          $scope.orderCount = $scope.result.count;
          $scope.grandTotal = $scope.result.grandTotal;
          $scope.averageAmount = $scope.result.averageAmount;
        }, function () {
          notificationsService.error("Error", "Error on searching data.");
        })
        .finally(function () {
          $scope.loading = false;
        });
    };

    $scope.GetStatusList = function () {
      return resources.StatusList()
        .then(function (result) {
          state.setStatusList(result.data);
          syncState();
        }, function () {
          notificationsService.error("Error", "Error on getting status list.");
        });
    };

    $scope.GetStores = function () {
      return resources.Stores()
        .then(function (result) {
          $scope.stores = result.data || [];

          if (!$scope.filters.store && $scope.stores.length) {
            state.setStore($scope.stores[0].alias);
          }

          $scope.labelDropdowns.dropdownStores = $scope.filters.store;

          refreshCurrentView();
        }, function () {
          notificationsService.error("Error", "Error on getting stores.");
        });
    };

    $scope.GetPaymentProviders = function (resetSelection) {
      if (!$scope.filters.store) {
        return $q.when();
      }

      return resources.PaymentProviders($scope.filters.store)
        .then(function (result) {
          state.setPaymentProviders(result.data);
          syncState();

          if (resetSelection) {
            state.clearPaymentProvider();
            delete $scope.labelDropdowns.dropdownPaymentProvider;
          }

          if ($scope.filters.paymentProvider) {
            var selectedProviderStillExists = $scope.paymentProviders.some(function (provider) {
              return provider.key === $scope.filters.paymentProvider;
            });

            if (!selectedProviderStillExists) {
              state.clearPaymentProvider();
              delete $scope.labelDropdowns.dropdownPaymentProvider;
            }
          }
        }, function () {
          notificationsService.error("Error", "Error on getting payment providers.");
        });
    };

    $scope.navigateTo = function (location) {
      if ($scope.location === location) {
        return;
      }

      $scope.location = location;
      $location.path("/ekommanager/" + location);

      if (location === "analytics") {
        $scope.analytics();
      } else {
        $scope.GetPaymentProviders(false).finally(function () {
          $scope.GetData();
        });
      }
    };

    $scope.OpenFilter = function () {
      $scope.filterOpen = true;
      $scope.overlay = {
        title: "Filter",
        view: "/App_Plugins/Ekom/Manager/views/overlays/ekmFilter.html",
        show: true,
        submit: function () {
          $scope.CloseModal();
        },
        close: function () {
          $scope.CloseModal();
        }
      };
    };

    $scope.OpenExport = function () {
      $scope.exportOptions = {
        includeOrderLines: false,
        exporting: false
      };

      $scope.overlay = {
        title: "Export orders",
        view: "/App_Plugins/Ekom/Manager/views/overlays/ekmExport.html",
        editModel: {
          options: $scope.exportOptions
        },
        show: true,
        submit: function () {
          $scope.ExportCsv(null, $scope.exportOptions.includeOrderLines);
        },
        close: function () {
          $scope.CloseModal();
        }
      };
    };

    $scope.ViewOrder = function (order) {
      resources.OrderInfo(order.uniqueId)
        .then(function (result) {
          state.setCurrentOrderStatus(result.data.orderStatus);

          $scope.overlay = {
            title: "View Order",
            view: "/App_Plugins/Ekom/Manager/views/overlays/ekmOrder.html",
            editModel: {
              order: result.data,
              statusList: $scope.statusList
            },
            show: true,
            submit: function () {
              $scope.CloseModal();
            },
            close: function () {
              $scope.CloseModal();
            }
          };
        }, function () {
          notificationsService.error("Error", "Error on getting orderInfo.");
        });
    };

    $scope.CloseModal = function () {
      if ($scope.overlay) {
        $scope.overlay.show = false;
      }

      $scope.overlay = null;
      $scope.filterOpen = false;
      document.body.classList.remove("tabbing-active");
    };

    $scope.setPage = function (page) {
      $scope.page = page.toString().replace("...", "");
      $scope.GetData();
    };

    $scope.setPageMostSoldProducts = function (page) {
      $scope.pageMostSoldProducts = page.toString().replace("...", "");
      loadMostSoldProducts();
    };

    $scope.search = function () {
      $scope.page = 1;

      cancelSearchDebounce();
      searchDebouncePromise = $timeout(function () {
        searchDebouncePromise = null;
        $scope.GetData();
      }, 300);
    };

    $scope.onDateRangeChanged = function () {
      $scope.page = 1;

      if ($scope.location === "analytics") {
        $scope.pageMostSoldProducts = 1;
        $scope.analytics();
        return;
      }

      $scope.GetData();
    };

    $scope.pageRange = function () {
      var rangeSize = 5;
      var ret = [];
      var start;

      if ($scope.page <= Math.floor(rangeSize / 2)) {
        start = 1;
      } else if (parseInt($scope.page, 10) + Math.floor(rangeSize / 2) >= $scope.result.totalPages) {
        start = Math.max($scope.result.totalPages - rangeSize + 1, 1);
      } else {
        start = $scope.page - Math.floor(rangeSize / 2);
      }

      for (var i = 0; i < rangeSize; i++) {
        var pageNumber = start + i;

        if (pageNumber <= $scope.result.totalPages) {
          ret.push(pageNumber);
        }
      }

      if (ret.length && ret[ret.length - 1] < $scope.result.totalPages) {
        ret.push("..." + $scope.result.totalPages);
      }

      if (ret.length && ret[0] > 1) {
        ret.unshift("...1");
      }

      return ret;
    };

    $scope.pageRangeMostSoldProducts = function () {
      var rangeSize = 5;
      var ret = [];
      var start;
      var totalPages = Math.max($scope.mostSoldProductsTotalPages || 1, 1);
      var page = $scope.pageMostSoldProducts;

      if (page <= Math.floor(rangeSize / 2)) {
        start = 1;
      } else if (parseInt(page, 10) + Math.floor(rangeSize / 2) >= totalPages) {
        start = Math.max(totalPages - rangeSize + 1, 1);
      } else {
        start = page - Math.floor(rangeSize / 2);
      }

      for (var i = 0; i < rangeSize; i++) {
        var pageNumber = start + i;

        if (pageNumber <= totalPages) {
          ret.push(pageNumber);
        }
      }

      if (ret.length && ret[ret.length - 1] < totalPages) {
        ret.push("..." + totalPages);
      }

      if (ret.length && ret[0] > 1) {
        ret.unshift("...1");
      }

      return ret;
    };

    $scope.analytics = function () {
      loadCharts();
      loadMostSoldProducts();
    };

    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, value) {
      $scope.visibleDropdowns[dropdownId] = false;
      $scope.labelDropdowns[dropdownId] = value;

      if (dropdownId === "dropdownStores") {
        state.setStore(value);
        $scope.page = 1;
        $scope.pageMostSoldProducts = 1;

        if ($scope.location === "analytics") {
          $scope.analytics();
          return;
        }

        $scope.GetPaymentProviders(true).finally(function () {
          $scope.GetData();
        });
        return;
      }

      if (dropdownId === "dropdownStatusList") {
        $scope.filters.orderStatus = value;
      }

      if (dropdownId === "dropdownPaymentProvider") {
        state.setPaymentProvider(value);
      }

      if (dropdownId === "dropdownOrderStatusList") {
        $scope.orderChangeStatus = value;
        return;
      }

      $scope.page = 1;

      if ($scope.location === "analytics") {
        $scope.pageMostSoldProducts = 1;
        $scope.analytics();
      } else {
        $scope.GetData();
      }
    };

    $scope.labelDropdown = function (dropdownId, defaultText) {
      var label = $scope.labelDropdowns[dropdownId] || defaultText;

      if (dropdownId === "dropdownOrderStatusList") {
        $scope.orderChangeStatus = label;
      }

      if (dropdownId === "dropdownPaymentProvider") {
        var provider = $scope.paymentProviders.find(function (item) {
          return item.key === label;
        });

        if (provider) {
          return provider.title;
        }
      }

      return label;
    };

    $scope.getStatusLabel = function (value) {
      var item = $scope.statusList.find(function (status) {
        return status.enumValue === value;
      });

      return item ? item.label : value;
    };

    $scope.onStatusChanged = function (order) {
      resources.ChangeOrderStatus({
        orderId: order.uniqueId,
        orderStatus: order.orderStatusCol,
        notify: true
      })
        .then(function () {
          notificationsService.success("Success", "Order status updated.");
        }, function () {
          notificationsService.error("Error", "Error updating order status.");
        });
    };

    function escapeCsvValue(value) {
      var escapedValue = value;

      if (escapedValue == null) {
        return "";
      }

      if (typeof escapedValue === "object") {
        escapedValue = JSON.stringify(escapedValue);
      }

      escapedValue = String(escapedValue);

      return /[",\r\n]/.test(escapedValue)
        ? '"' + escapedValue.replace(/"/g, '""') + '"'
        : escapedValue;
    }

    function downloadCsv(keys, rows, filename) {
      var lines = [keys.join(",")];

      rows.forEach(function (row) {
        lines.push(keys.map(function (key) {
          return escapeCsvValue(row[key]);
        }).join(","));
      });

      var blob = new Blob(["\uFEFF", lines.join("\r\n")], { type: "text/csv;charset=utf-8;" });
      var url = URL.createObjectURL(blob);
      var anchor = document.createElement("a");

      anchor.href = url;
      anchor.download = filename || "orders.csv";
      document.body.appendChild(anchor);

      return $timeout(function () {
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
      }, 0, false);
    }

    function getOrderLineExportKeys(orders) {
      return ["rowType"].concat(Object.keys(orders[0]), [
        "orderLineKey",
        "productSku",
        "productTitle",
        "variantSku",
        "variantTitle",
        "quantity",
        "unitPrice",
        "vat",
        "discount",
        "lineTotal"
      ]);
    }

    function getEmptyOrderLineExportData() {
      return {
        orderLineKey: "",
        productSku: "",
        productTitle: "",
        variantSku: "",
        variantTitle: "",
        quantity: "",
        unitPrice: "",
        vat: "",
        discount: "",
        lineTotal: ""
      };
    }

    function getOrderLineExportData(orderLine) {
      return {
        orderLineKey: orderLine.key,
        productSku: orderLine.product && orderLine.product.sku,
        productTitle: orderLine.product && orderLine.product.title,
        variantSku: orderLine.variant && orderLine.variant.sku,
        variantTitle: orderLine.variant && orderLine.variant.title,
        quantity: orderLine.quantity,
        unitPrice: orderLine.product && orderLine.product.price && orderLine.product.price.withVat && orderLine.product.price.withVat.currencyString,
        vat: orderLine.amount && orderLine.amount.vat && orderLine.amount.vat.currencyString,
        discount: orderLine.amount && orderLine.amount.discountAmount && orderLine.amount.discountAmount.currencyString,
        lineTotal: orderLine.amount && orderLine.amount.withVat && orderLine.amount.withVat.currencyString
      };
    }

    function getOrderLineRow(order, orderLine) {
      return angular.extend({
        rowType: "OrderLine",
        referenceId: order.referenceId,
        uniqueId: order.uniqueId
      }, getEmptyOrderLineExportData(), getOrderLineExportData(orderLine));
    }

    function getOrderLineExportRows(orders) {
      return $q.all(orders.map(function (order) {
        return resources.OrderInfo(order.uniqueId)
          .then(function (result) {
            var orderInfo = result.data || {};
            var orderLines = orderInfo.orderLines || [];
            var rows = [angular.extend({ rowType: "Order" }, order, getEmptyOrderLineExportData())];

            orderLines.forEach(function (orderLine) {
              rows.push(getOrderLineRow(order, orderLine));
            });

            return rows;
          });
      })).then(function (orderLineRows) {
        return [].concat.apply([], orderLineRows);
      });
    }

    function getExportOrders() {
      if (!$scope.result.count) {
        return $q.when([]);
      }

      return resources.SearchOrders(buildExportOrdersQuery())
        .then(function (result) {
          var data = result.data || {};

          return data.orders || [];
        });
    }

    $scope.ExportCsv = function (filename, includeOrderLines) {
      if (!$scope.result.count) {
        return;
      }

      $scope.exportOptions.exporting = true;

      return getExportOrders()
        .then(function (orders) {
          if (!orders.length) {
            return;
          }

          if (!includeOrderLines) {
            return downloadCsv(Object.keys(orders[0]), orders, filename);
          }

          return getOrderLineExportRows(orders)
            .then(function (rows) {
              return downloadCsv(getOrderLineExportKeys(orders), rows, filename || "orders-with-orderlines.csv");
            });
        })
        .then(function () {
          $scope.CloseModal();
        }, function () {
          notificationsService.error("Error", "Error exporting orders.");
        })
        .finally(function () {
          $scope.exportOptions.exporting = false;
        });
    };

    $scope.$on("$destroy", function () {
      cancelSearchDebounce();
      chartService.destroyAll();
      $document.off("click", unbindDocumentClick);
    });

    attachDocumentClickHandler();

    angular.element(document).ready(function () {
      $scope.GetStatusList();
      $scope.GetStores();
    });
  }

  angular.module("umbraco").controller("Ekom.Manager.Dashboard", [
    "$scope",
    "notificationsService",
    "Ekom.Manager.Resources",
    "$location",
    "$document",
    "eventsService",
    "Ekom.Manager.State",
    "Ekom.Manager.ChartService",
    "$timeout",
    "$q",
    controller
  ]);
})();
