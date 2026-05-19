angular.module('umbraco.resources').factory('Ekom.Listview.Resources',
  function ($http) {
    return {
      GetCatalogItems: function (options) {
        return $http({
          method: 'GET',
          url: '/ekom/backoffice/catalog-listview',
          params: options
        });
      }
    };
  });
