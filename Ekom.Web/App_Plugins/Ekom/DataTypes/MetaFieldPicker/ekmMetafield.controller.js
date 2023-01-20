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

          $scope.fields.forEach(function (item, index) {
            if (item.Values.length > 0) {
              $scope.values.push([]);
            } else {
              $scope.values.push("");
            }
          });

          if ($scope.model.value.length > 0) {
            $scope.model.value.forEach(function (item, index) {

              var fieldExist = $scope.fields.filter(field => {
                return field.Key === item.Key
              })

              if (fieldExist.length > 0) {
                $scope.values[index] = item.Values;
              }  

            });
          }

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
            Key: field.Key,
            Values: item
          });
        });

        $scope.model.value = modifiedValues;
      });

    }]
  );

})();
