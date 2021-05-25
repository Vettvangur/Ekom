angular.module("umbraco").controller("Ekom.Cache", function ($scope, assetsService, $http) {
  $scope.loading = false;
  $scope.PopulateCache = function () {

    $scope.loading = true;

    $http({
      url: '/umbraco/backoffice/ekom/api/populateCache',
      method: 'POST',
      dataType: 'json'
    }).success(function (data) {

      if (data.success) {
        notificationsService.success("Success", "Cache has been populated again.");
      } else {
        notificationsService.error("Error", "Error on cache update");
      }

      $scope.loading = false;

    }).error(function (data) {
      $scope.loading = false;

      notificationsService.error("Error", "Error on sending data.");
    });

  };
});
