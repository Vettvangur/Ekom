(function () {
  "use strict";

  app.requires.push('angular.chosen');

  angular.module("umbraco").controller("Ekom.Metafield", [
    '$scope',
    'Ekom.Resources',
    '$routeParams',
    function ($scope, ekmResources, $routeParams) {

      if ($routeParams.section !== 'content') { return; }

      $scope.model.value = $scope.model.value || [];
      $scope.values = [];
      $scope.fields = [];
      $scope.languages = [];
      $scope.loading = true;

      ekmResources.getLanguages().then(function (languages) {

        $scope.languages = languages;

        ekmResources.getMetafields().then(function (fields) {

          $scope.fields = fields;

          $scope.fields.forEach(function (item) {

            var currentValue = $scope.model.value.filter(field => {
              return field.Key === item.key
            })

            if (item.values.length > 0) {
              if (currentValue.length > 0) {
                $scope.values.push(currentValue[0].Values);
              } else {
                $scope.values.push([]);
              }

            } else {
              if (currentValue.length > 0) {
                $scope.values.push(currentValue[0].Values);
              } else {
                $scope.values.push('');
              }
            }
          });


          $scope.loading = false;

        });

      });

      $scope.Reset = function (index) {

        $scope.values[index] = "";

      };

      $scope.$on("formSubmitting", function (ev, args) {
        var modifiedValues = [];

        $scope.values.forEach(function (item, index) {

          var field = $scope.fields[index];

          modifiedValues.push({
            Key: field.key,
            Values: item
          });
        });

        $scope.model.value = modifiedValues;
      });

    }]
  );

})();
