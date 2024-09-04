angular.module("umbraco").controller("Ekom.Cache", function ($scope, $http, $routeParams, notificationsService) {

  if ($routeParams.section !== 'content') { return; }

  $scope.loading = false;
  $scope.model.hideLabel = false;
  $scope.PopulateCache = function () {

    $scope.loading = true;

    $http({
      url: Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + "Cache",
      method: 'POST',
      dataType: 'json'
    }).then(function (data) {

      if (data) {
        notificationsService.success("Success", "Cache has been populated again.");
      } else {
        notificationsService.error("Error", "Error on cache update");
      }

      $scope.loading = false;

    });

  };
});
