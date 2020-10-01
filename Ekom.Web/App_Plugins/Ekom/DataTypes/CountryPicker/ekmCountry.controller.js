angular.module("umbraco").controller("Ekom.Country", function ($scope, assetsService, $routeParams, $http, editorState) {

  $http.get('/Umbraco/Ekom/Api/GetCountries').then(function (res) {

    $scope.ItemArray = res.data;

    $scope.combined = function (country) {
      return country.Name + " (" + country.Code + ")";
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
      $scope.model.value = $scope.selectedOption.Code;
    };

    $scope.$on("formSubmitting", function () {
      $scope.model.value = $scope.selectedOption.Code;
    });
  });
});
