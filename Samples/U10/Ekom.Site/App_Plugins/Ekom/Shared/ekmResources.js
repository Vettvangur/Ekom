/* Resources */
angular.module('umbraco.resources').factory('Ekom.Resources',
  function ($q, $http, umbRequestHelper) {
    var languagesCache = null;
    var languagesPromise = null;
    var languagesByNodeCache = {};
    var languagesByNodePromise = {};
    var storesByNodeCache = {};
    var storesByNodePromise = {};
    var api = {
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
        if (languagesCache) {
          return $q.resolve(languagesCache);
        }

        if (languagesPromise) {
          return languagesPromise;
        }

        languagesPromise = umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Languages"),
          'Failed to retrieve languages'
        ).then(function (data) {
          languagesCache = data;
          return data;
        }).finally(function () {
          languagesPromise = null;
        });

        return languagesPromise;
      },
      getLanguagesByNode: function (id) {
        if (!id) {
          return api.getLanguages();
        }

        if (languagesByNodeCache[id]) {
          return $q.resolve(languagesByNodeCache[id]);
        }

        if (languagesByNodePromise[id]) {
          return languagesByNodePromise[id];
        }

        languagesByNodePromise[id] = umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Languages/" + id),
          'Failed to retrieve languages'
        ).then(function (data) {
          languagesByNodeCache[id] = data;
          return data;
        }).finally(function () {
          languagesByNodePromise[id] = null;
        });

        return languagesByNodePromise[id];
      },
      getStoresByNode: function (id) {
        if (!id) {
          return umbRequestHelper.resourcePromise(
            $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Stores/" + id),
            'Failed to retrieve stores'
          );
        }

        if (storesByNodeCache[id]) {
          return $q.resolve(storesByNodeCache[id]);
        }

        if (storesByNodePromise[id]) {
          return storesByNodePromise[id];
        }

        storesByNodePromise[id] = umbRequestHelper.resourcePromise(
          $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Stores/" + id),
          'Failed to retrieve stores'
        ).then(function (data) {
          storesByNodeCache[id] = data;
          return data;
        }).finally(function () {
          storesByNodePromise[id] = null;
        });

        return storesByNodePromise[id];
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
    return api;
  }
);
