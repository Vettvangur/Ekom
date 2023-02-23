angular.module('umbraco').controller('Ekom.Coupon', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
  $scope.model.hideLabel = false;
  var key = editorState.current.key;

  $scope.coupons = [];
  $scope.couponCode = '';
  $scope.numberAvailable = 1;

  $scope.Init = function () {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'GetCouponsForDiscount?discountId=' + key)
      .then(function (result) {

        $scope.coupons = result;

        if ($scope.coupons.length > 0) {
          $scope.Selected = $scope.coupons[0].couponCode;
        }

      });

  };

  $scope.Insert = function () {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'InsertCoupon?discountId=' + key + '&couponCode=' + $scope.couponCode + '&numberAvailable=' + $scope.numberAvailable)
      .then(function () {

        $scope.Init();

      });

  };

  $scope.Remove = function (couponCode) {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'RemoveCoupon?discountId=' + key + '&couponCode=' + couponCode)
      .then(function () {

        $scope.Init();

      });

  };

  if ($routeParams.section !== 'settings') {

    $scope.Init();

  }


});
