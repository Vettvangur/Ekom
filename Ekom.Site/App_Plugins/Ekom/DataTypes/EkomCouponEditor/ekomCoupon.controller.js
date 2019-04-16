﻿angular.module('umbraco').controller('Ekom.CouponEditor', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
 
  var key = editorState.current.key;

  $scope.Coupons = [];
  $scope.CouponCode = '';
  $scope.NumberAvailable = 1;

  $scope.Init = function () {

    $http.post('/umbraco/backoffice/ekom/api/GetCouponsForDiscount?discountId=' + key)
      .success(function (result) {

        $scope.Coupons = result;

        if ($scope.Coupons.length > 0) {
          $scope.Selected = $scope.Coupons[0].CouponCode;
        }

      });

  };

  $scope.Insert = function () {

    $http.post('/umbraco/backoffice/ekom/api/InsertCoupon?discountId=' + key + '&couponCode=' + $scope.CouponCode + '&numberAvailable=' + $scope.NumberAvailable) 
      .success(function () {

        $scope.Init();

      });

  };

  $scope.Remove = function (couponCode) {

    $http.post('/umbraco/backoffice/ekom/api/RemoveCoupon?discountId=' + key + '&couponCode=' + couponCode)
      .success(function () {

        $scope.Init();

      });

  };

  if ($routeParams.section !== 'settings') {

    $scope.Init();

  }


});
