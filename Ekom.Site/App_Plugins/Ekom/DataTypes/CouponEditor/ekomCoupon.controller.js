angular.module('umbraco').controller('Ekom.Coupon', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
 
  var key = editorState.current.key;

  $scope.Coupons = [];
  $scope.CouponCode = '';
  $scope.NumberAvailable = 1;

  $scope.Init = function () {

      $http.post('/umbraco/backoffice/ekom/api/GetCouponsForDiscount?discountId=' + key)
        .then(
            function(result) {

                console.log(result.data);

                $scope.Coupons = result.data;

                if ($scope.Coupons.length > 0) {
                    $scope.Selected = $scope.Coupons[0].CouponCode;
                }
            },
            function() {

            });

  };

  $scope.Insert = function () {

      $http.post('/umbraco/backoffice/ekom/api/InsertCoupon?discountId=' + key + '&couponCode=' + $scope.CouponCode + '&numberAvailable=' + $scope.NumberAvailable)
          .then(
              function() {
                  $scope.Init();
              },
              function() {

              });

  };

  $scope.Remove = function (couponCode) {

      $http.post('/umbraco/backoffice/ekom/api/RemoveCoupon?discountId=' + key + '&couponCode=' + couponCode)
          .then(
              function() {
                  $scope.Init();
              },
              function() {

              });

  };

  if ($routeParams.section !== 'settings') {

    $scope.Init();

  }


});
