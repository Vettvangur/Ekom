(function () {
  "use strict";

  angular.module("umbraco").controller("Ekom.PropertyEditorPicker", [
    '$scope',
    'Ekom.Resources',
    function ($scope, ekmResources) {

      $scope.model.dataTypes = [];
      $scope.model.value = $scope.model.value || {
        guid: "0cc0eba1-9960-42c9-bf9b-60e150b429ae",
        name: "Textstring",
        propertyEditorAlias: "Umbraco.Textbox"
      };

      ekmResources.getNonEkomDataTypes().then(function (data) {
        $scope.model.dataTypes = data;
      });

    }]
  );

  angular.module("umbraco").controller("Ekom.PropertyEditor", [
    '$scope',
    '$rootScope',
    'editorState',
    'Ekom.Resources',
    'umbPropEditorHelper',
    'appState',
    '$routeParams',
    'Ekom.LocalStorageService',
    'eventsService',
    function ($scope, $rootScope, editorState, ekmResources, umbPropEditorHelper, appState, $routeParams, localStorageService, eventsService) {

      if ($routeParams.section !== 'content') { return; }

      $scope.loading = true;
      $scope.failed = false;

      $scope.model.hideLabel = $scope.model.config.hideLabel == 1;

      $scope.property = {
        config: {},
        view: ""
      };


      $scope.tabs = [];
      $scope.currentTab = undefined;

      if (!angular.isObject($scope.model.value))
        $scope.model.value = undefined;

      $scope.model.value = $scope.model.value || {
        values: {},
        dtdGuid: "00000000-0000-0000-0000-000000000000",
        type: "Language"
      };

      var parentScope = $scope;
      var nodeContext = undefined;

      while (!nodeContext && parentScope.$id !== $rootScope.$id) {
        parentScope = parentScope.$parent;
        nodeContext = parentScope.nodeContext;
      }
      if (!nodeContext) {
        nodeContext = editorState.current;
      }

      ekmResources.getDataTypeById($scope.model.config.dataType.guid).then(function (dataType) {

        // Stash the config in scope for reuse
        $scope.property.config = dataType.preValues;

        // Get the view path
        $scope.property.viewPath = umbPropEditorHelper.getViewPath(dataType.view);

        // Get the property alias
        let propAlias = $scope.model.propertyAlias || $scope.model.alias;

        ekmResources.getDataTypeByAlias(nodeContext.contentTypeAlias, propAlias).then(function (dataType2) {

          $scope.model.value.dtdGuid = dataType2.guid;

          if (dataType2.preValues.useLanguages) {

            $scope.model.value.type = "Language";

            ekmResources.getLanguages().then(function (languages) {

              $scope.tabs = languages.map(x => ({ value: x.IsoCode, text: x.CultureName }));

              $scope.loading = false;

              setValues();

              let eventModel = {
                model: $scope.model,
                tabs: $scope.tabs,
                alias: $scope.model.alias
              }

              eventsService.emit("ekmPropertyLoaded", { value: eventModel });

            }).catch(function () {
              $scope.failed = true;
            });;

          } else {

            $scope.model.value.type = "Store";

            ekmResources.getStores().then(function (stores) {

              $scope.loading = false;

              $scope.tabs = stores.map(x => ({ value: x.Alias, text: x.Title }));

              setValues();

              let eventModel = {
                model: $scope.model,
                tabs: $scope.tabs,
                alias: $scope.model.alias
              }

              eventsService.emit("ekmPropertyLoaded", { value: eventModel });

            }).catch(function () {

            }).catch(function () {
              $scope.failed = true;
            });;

          }

        }).catch(function () {
          $scope.failed = true;
        });;

      }).catch(function () {
        $scope.failed = true;
      });

      $scope.setCurrentTab = function (tab, broadcast) {

        $scope.currentTab = tab;

        if (broadcast) {
          localStorageService.set('ekomCurrentTab', tab.value);
          $rootScope.$broadcast("ekomSync");
        }
        
      }

      $scope.$on("formSubmitting", function (ev, args) {

        $scope.$broadcast("ekomValuesEvent", { tab: $scope.currentTab.value });

        validateProperty();

        if ($scope.ekomPropertyForm.$valid) {

          var cleanValue = {};
          _.each($scope.tabs, function (tab) {

            cleanValue[tab.value] = $scope.model.value.values[tab.value];

          });

          $scope.model.value.values = !_.isEmpty(cleanValue) ? cleanValue : undefined;
        }

      });

      var unsubSync = $scope.$on("ekomSync", function (evt) {
        sync();
      });

      $scope.$on("$destroy", function () {
        unsubSync();
      });

      var sync = function () {
        var currentTabValue = localStorageService.get('ekomCurrentTab');

        var currentTab = _.find($scope.tabs, function (itm) {
          return itm.value == currentTabValue;
        }) || $scope.currentTab;

        $scope.setCurrentTab(currentTab, false);
      };

      var setValues = function () {

        $scope.currentTab = $scope.tabs[0];

        if (!$scope.model.value.values) {
          $scope.model.value.values = {};
        }

        _.each($scope.tabs, function (tab) {
          if (!$scope.model.value.values.hasOwnProperty(tab.value)) {
            $scope.model.value.values[tab.value] = $scope.model.value.values[tab.value];
          }
        });

      }

      var validateProperty = function () {

        if ($scope.model.validation.mandatory) {

          var mandatoryBehaviour = "any";
          var primaryLanguage = "none"

          var isValid = true;

          switch (mandatoryBehaviour) {
            case "all":
              _.each($scope.tabs, function (tab) {
                if (!(tab.value in $scope.model.value.values) ||
                  !$scope.model.value.values[tab.value]) {
                  isValid = false;
                  return;
                }
              });
              break;
            case "any":
              isValid = false;
              _.each($scope.tabs, function (tab) {
                if (tab.value in $scope.model.value.values &&
                  $scope.model.value.values[tab.value]) {
                  isValid = true;
                  return;
                }
              });
              break;
            case "primary":
              if (primaryLanguage in $scope.model.value.values
                && $scope.model.value.values[primaryLanguage]) {
                isValid = true;
              } else {
                isValid = false;
              }
              break;
          }

          $scope.ekomPropertyForm.$setValidity("required", isValid);
        }
      };

    }]
  );

})();

/* Directives */
angular.module("umbraco.directives").directive('ekomProperty',
  function (eventsService, $timeout) {

    var link = function (scope, ctrl) {
      scope[ctrl.$name] = ctrl;

      scope.model = {};

      // Some core property editors update the prevalues
      // but then fail to check them incase the config
      // is in the desired format, so to get round this
      // we give each instance a clone of the original
      // config so that changes made aren't remebered
      // between tab loads
      // bug here http://issues.umbraco.org/issue/U4-8266
      scope.model.config = angular.copy(scope.config);

      scope.model.alias = scope.propertyAlias + "." + scope.tab;
      scope.model.value = scope.value.values ? scope.value.values[scope.tab] : undefined;

      var unsubscribe = scope.$on("ekomValuesEvent", function (ev, args) {

        scope.$broadcast("formSubmitting", { scope: scope });

        if (!scope.value.values)
          scope.value.values = {};

        scope.value.values[scope.tab] = scope.model.value;

      });

      var eventsServiceUnsubscribe = eventsService.on('ekmInputChange', function (event, data) {


        if (data.value.all) {

          if (scope.model.alias.indexOf('title') > -1) {
            scope.$apply(function () {
              scope.model.value = data.value.title;
            });
          }

          if (scope.model.alias.indexOf('slug') > -1) {
            scope.$apply(function () {
              scope.model.value = data.value.slug;
            });
          }
        } else {

          if (scope.model.alias === data.value.alias) {
            scope.$apply(function () {
              scope.model.value = data.value.slug;
            });
          }

        }


      });

      $timeout(function () {


        const params = new Proxy(new URLSearchParams(window.location.href), {
          get: (searchParams, prop) => searchParams.get(prop),
        });

        if (scope.model.alias.startsWith('title.') && params != null && params.create && params.create == 'true') {

          let titleInput = document.getElementById(scope.model.alias);

          if (titleInput) {

            titleInput.addEventListener("keyup", function () {

              var slugInput = document.getElementById('slug.' + scope.tab);

              if (slugInput) {

                var model = {
                  title: titleInput.value,
                  slug: "",
                  alias: scope.model.alias.replace('title','slug')
                };

                let inputValue = titleInput.value;

                Umbraco.Sys.ServerVariables.ekom.charCollections.forEach((char) => {
                  inputValue = inputValue.replace(char.Char, char.Replacement);
                });

                const slugify =
                  inputValue
                    .normalize('NFKD')            // The normalize() using NFKD method returns the Unicode Normalization Form of a given string.
                    .toLowerCase()                  // Convert the string to lowercase letters
                    .trim()                                  // Remove whitespace from both sides of a string (optional)
                    .replace(/\s+/g, '-')            // Replace spaces with -
                    .replace(/[^\w\-]+/g, '')     // Remove all non-word chars
                    .replace(/\-\-+/g, '-');        // Replace multiple - with single -

                model.slug = slugify;

                eventsService.emit("ekmInputChange", { value: model });

              }

            });
          }

        }

      },500);

      scope.$on('$destroy', function () {
        unsubscribe();
        eventsServiceUnsubscribe();
      });
    };

    return {
      require: "^form",
      restrict: "E",
      replace: true,
      link: link,
      template: '<div ng-include="propertyEditorView"></div>',
      scope: {
        propertyEditorView: '=view',
        config: '=',
        tab: '=',
        propertyAlias: '=',
        value: '='
      }
    };
  });

/* Services */
angular.module('umbraco.services').factory('Ekom.LocalStorageService',
  function ($cookies) {

    var supportsLocalStorage = function () {
      try {
        return 'localStorage' in window && window['localStorage'] !== null;
      } catch (e) {
        return false;
      }
    }

    var stash = function (key, value) {
      if (supportsLocalStorage()) {
        localStorage.setItem(key, value);
      } else {
        $cookies[key] = value;
      }
    }

    var unstash = function (key) {
      if (supportsLocalStorage()) {
        return localStorage.getItem(key);
      } else {
        return $cookies[key];
      }
    }

    return {
      get: function (key, fallback) {
        var rawVal = unstash(key);
        if (!rawVal) return fallback;
        return JSON.parse(rawVal);
      },
      set: function (key, obj) {
        stash(key, JSON.stringify(obj));
      }
    };
  }
);
