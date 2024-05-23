angular.module('umbraco.resources').factory('Ekom.Listview.Resources',
  function ($q, $http) {
    return {

      GetProducts: function (categoryKey) {
        var url = '/ekom/catalog/products/' + categoryKey;
        return $http({
          method: 'POST',
          url: url
        });
      },

      GetSubCategories: function (categoryKey) {
        var url = '/ekom/catalog/subcategories/' + categoryKey;

        return $http({
          method: 'POST',
          url: url
        });
      }
    };
  });
