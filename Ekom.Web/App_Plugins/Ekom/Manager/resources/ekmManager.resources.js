angular.module('umbraco.resources').factory('Ekom.Manager.Resources',
  function ($q, $http) {
    return {

      backofficeBaseUrl: Umbraco.Sys.ServerVariables.ekom.managerEndpoint,
      apiUrl: Umbraco.Sys.ServerVariables.ekom.apiEndpoint,
      baseUrl: '/ekom/',
      SearchOrders: function (querystring) {
        var url = this.backofficeBaseUrl + 'SearchOrders' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      StatusList: function () {
        var url = this.backofficeBaseUrl + 'StatusList';
        return $http({
          method: 'GET',
          url: url
        });
      },
      Stores: function () {
        var url = this.backofficeBaseUrl + 'Stores';
        return $http({
          method: 'GET',
          url: url
        });
      },
      OrderInfo: function (orderId) {
        var url = this.backofficeBaseUrl + 'OrderInfo/' + orderId;
        return $http({
          method: 'GET',
          url: url
        });
      },
      Charts: function (querystring) {
        var url = this.backofficeBaseUrl + 'Charts' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      MostSoldProducts: function (querystring) {
        var url = this.backofficeBaseUrl + 'MostSoldProducts' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      ChangeOrderStatus: function (querystring) {
        var url = this.backofficeBaseUrl + 'ChangeOrderStatus' + querystring;
        return $http({
          method: 'POST',
          url: url
        });
      },
      PaymentProviders: function (storeAlias) {
        var url = this.baseUrl + 'provider/paymentsproviders/' + storeAlias;
        return $http({
          method: 'GET',
          url: url
        });
      },
    };
  });
