/* Resources */
angular.module('umbraco.resources').factory('Ekom.Resources',
  function ($q, $http, umbRequestHelper) {
    return {
      getNonEkomDataTypes: function () {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "GetNonEkomDataTypes"),
          'Failed to retrieve datatypes'
        );
      },
      getDataTypeById: function (id) {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "DataType/" + id),
          'Failed to retrieve datatype'
        );
      },
      getDataTypeByAlias: function (contentTypeAlias, propertyAlias) {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "DataType/" + contentTypeAlias + "/propertyAlias/" + propertyAlias),
          'Failed to retrieve datatype'
        );
      },
      getLanguages: function () {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Languages"),
          'Failed to retrieve languages'
        );
      },
      getStores: function () {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Stores"),
          'Failed to retrieve stores'
        );
      },
      getMetafields: function () {
        return umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Metafields"),
          'Failed to retrieve stores'
        );
      }
    };
  }
);
