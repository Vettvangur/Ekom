angular.module("umbraco.resources").factory("Ekom.Manager.Resources", [
  "$http",
  function ($http) {
    var backofficeBaseUrl = Umbraco.Sys.ServerVariables.ekom.managerEndpoint;
    var baseUrl = "/ekom/";

    function buildQueryString(query) {
      if (!query) {
        return "";
      }

      if (angular.isString(query)) {
        return query.charAt(0) === "?" ? query : "?" + query;
      }

      var parts = [];

      angular.forEach(query, function (value, key) {
        var queryValue = value;

        if (queryValue === undefined || queryValue === null) {
          queryValue = "";
        }

        parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(queryValue));
      });

      return parts.length ? "?" + parts.join("&") : "";
    }

    function get(url, query) {
      return $http({
        method: "GET",
        url: url + buildQueryString(query)
      });
    }

    function post(url, query) {
      return $http({
        method: "POST",
        url: url + buildQueryString(query)
      });
    }

    function postJson(url, data) {
      return $http({
        method: "POST",
        url: url,
        data: data
      });
    }

    return {
      SearchOrders: function (query) {
        return get(backofficeBaseUrl + "SearchOrders", query);
      },
      ExportOrders: function (query) {
        return $http({
          method: "GET",
          url: backofficeBaseUrl + "ExportOrders" + buildQueryString(query),
          responseType: "blob"
        });
      },
      StatusList: function () {
        return get(backofficeBaseUrl + "StatusList");
      },
      Stores: function () {
        return get(backofficeBaseUrl + "Stores");
      },
      OrderInfo: function (orderId) {
        return get(backofficeBaseUrl + "OrderInfo/" + orderId);
      },
      OrderLogs: function (orderId) {
        return get(backofficeBaseUrl + "OrderLogs/" + orderId);
      },
      OrderActions: function (orderId) {
        return get(backofficeBaseUrl + "OrderActions/" + orderId);
      },
      ExecuteOrderAction: function (orderId, actionKey) {
        return $http({
          method: "POST",
          url: backofficeBaseUrl + "OrderActions/" + orderId + "/" + encodeURIComponent(actionKey),
          responseType: "blob"
        });
      },
      Charts: function (query) {
        return get(backofficeBaseUrl + "Charts", query);
      },
      MostSoldProducts: function (query) {
        return get(backofficeBaseUrl + "MostSoldProducts", query);
      },
      ChangeOrderStatus: function (query) {
        return post(backofficeBaseUrl + "ChangeOrderStatus", query);
      },
      UpdateCustomerInformation: function (data) {
        return postJson(backofficeBaseUrl + "UpdateCustomerInformation", data);
      },
      AddOrderLine: function (orderId, data) {
        return postJson(backofficeBaseUrl + "Order/" + encodeURIComponent(orderId) + "/OrderLines", data);
      },
      RemoveOrderLine: function (orderId, lineId) {
        return $http({
          method: "DELETE",
          url: backofficeBaseUrl + "Order/" + encodeURIComponent(orderId) + "/OrderLines/" + encodeURIComponent(lineId)
        });
      },
      PaymentProviders: function (storeAlias) {
        return get(baseUrl + "provider/paymentsproviders/" + storeAlias);
      }
    };
  }
]);
