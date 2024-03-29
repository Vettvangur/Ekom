angular.module('umbraco.resources').factory('Ekom.Manager.Resources',
  function ($q, $http) {
    return {

      baseUrl: Umbraco.Sys.ServerVariables.ekom.managerEndpoint,

      SearchOrders: function (querystring) {
        var url = this.baseUrl + 'SearchOrders' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      StatusList: function () {
        var url = this.baseUrl + 'StatusList';
        return $http({
          method: 'GET',
          url: url
        });
      },
      Stores: function () {
        var url = this.baseUrl + 'Stores';
        return $http({
          method: 'GET',
          url: url
        });
      },
      OrderInfo: function (orderId) {
        var url = this.baseUrl + 'OrderInfo/' + orderId;
        return $http({
          method: 'GET',
          url: url
        });
      },
      Charts: function (querystring) {
        var url = this.baseUrl + 'Charts' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      MostSoldProducts: function (querystring) {
        var url = this.baseUrl + 'MostSoldProducts' + querystring;
        return $http({
          method: 'GET',
          url: url
        });
      },
      ChangeOrderStatus: function (querystring) {
        var url = this.baseUrl + 'ChangeOrderStatus' + querystring;
        return $http({
          method: 'POST',
          url: url
        });
      }
    };
  });
