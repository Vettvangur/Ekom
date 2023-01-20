angular.module('umbraco').controller('Ekom.Coupon', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
  $scope.model.hideLabel = false;
  var key = editorState.current.key;

  $scope.Coupons = [];
  $scope.CouponCode = '';
  $scope.NumberAvailable = 1;

  $scope.Init = function () {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'GetCouponsForDiscount?discountId=' + key)
      .then(function (result) {

        $scope.Coupons = result;

        if ($scope.Coupons.length > 0) {
          $scope.Selected = $scope.Coupons[0].CouponCode;
        }

      });

  };

  $scope.Insert = function () {

    $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'InsertCoupon?discountId=' + key + '&couponCode=' + $scope.CouponCode + '&numberAvailable=' + $scope.NumberAvailable)
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
