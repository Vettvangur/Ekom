angular.module('umbraco').controller('Ekom.Coupon', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
  $scope.model.hideLabel = false;
  var key = editorState.current.key;

  $scope.coupons = [];
  $scope.couponCode = '';
  $scope.numberAvailable = 1;

  $scope.Init = function () {

    $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/discountId/' + key)
      .then(function (result) {

        $scope.coupons = result.data;

        if ($scope.coupons.length > 0) {
          $scope.Selected = $scope.coupons[0].couponCode;
        }

      });

  };

  $scope.Insert = function () {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/' + $scope.couponCode + '/NumberAvailable/' + $scope.numberAvailable + '/discountId/' + key)
      .then(function () {

        $scope.Init();

      });

  };

  $scope.Remove = function (couponCode) {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/' + couponCode + '/discountId/' + key)
      .then(function () {

        $scope.Init();

      });

  };

  if ($routeParams.section !== 'settings') {

    $scope.Init();

  }


});
