angular.module("umbraco").controller("Ekom.Country", function ($scope, $routeParams, $http) {

  if ($routeParams.section !== 'content') { return; }

  $http.get(Umbraco.Sys.ServerVariables.ekom.apiEndpoint + 'GetCountries').then(function (res) {
    $scope.model.hideLabel = false;
    $scope.ItemArray = res.data;

    $scope.combined = function (country) {
      return country.Name + " (" + country.code + ")";
    };

    if ($scope.model.value != '') {
      for (i = 0; $scope.ItemArray.length > i; i += 1) {
        if ($scope.ItemArray[i].Code == $scope.model.value) {
          $scope.selectedOption = $scope.ItemArray[i];
        }
      }
    }
    else {
      $scope.selectedOption = $scope.ItemArray[0];
    }

    for (i = 0; $scope.ItemArray.length > i; i += 1) {
      if ($scope.ItemArray[i].LanguageId == $scope.model.value) {
        $scope.selectedOption = $scope.ItemArray[i];
      }
    }

    $scope.update = function () {
      $scope.model.value = $scope.selectedOption.code;
    };

    $scope.$on("formSubmitting", function () {
      $scope.model.value = $scope.selectedOption.code;
    });
  });
});
