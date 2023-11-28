angular.module('umbraco').controller('Ekom.Coupon', function ($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
  $scope.model.hideLabel = true;

  if ($routeParams.section !== 'content') { return; }

  var key = editorState.current.key;
  $scope.create = editorState.current.id === 0;
  $scope.coupons = [];
  $scope.couponCode = '';
  $scope.query = '';
  $scope.page = 1;
  $scope.pageSize = 10;
  $scope.totalPages = 0;
  $scope.openOverlay = function () {

    var model = {
      couponCode: '',
      numberAvailable: 1
    };

    $scope.overlay = {
      title: "Add Coupon Code",
      view: "/App_Plugins/Ekom/DataTypes/CouponEditor/ekmCouponAddOverlay.html",
      editModel: model,
      show: true,
      submit: function (submitModel) {

        if (submitModel.editModel.couponCode === '' || submitModel.editModel.numberAvailable === '') {
          notificationsService.error("Error", "Coupon Code and Usage Limit are required fields");
        }

        $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/' + submitModel.editModel.couponCode + '/NumberAvailable/' + submitModel.editModel.numberAvailable + '/discountId/' + key)
          .then(function () {
            
            $scope.getCoupons();

            $scope.closeOverlay();

            notificationsService.success("Success", "Coupon Code added successfully");

          }, function errorCallback(data) {

            if (data.status === 409) {
              notificationsService.error("Error", "Coupon Code already exists on this or another discount.");
            } else {
              notificationsService.error("Error", "Error on creating coupon.");
            }
            
          });

      },
      close: function (oldModel) {
        $scope.closeOverlay();
      }
    };
    
  };

  $scope.closeOverlay = function () {
    $scope.overlay.show = false;
    $scope.overlay = null;
    document.body.classList.remove('tabbing-active');
  };

  $scope.getCoupons = function (enablePagination) {

    $http.get(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/discountId/' + key + '?query=' + $scope.query + '&page=' + $scope.page + '&pageSize=' + $scope.pageSize)
      .then(function (result) {

        $scope.coupons = result.data.item1;
        $scope.totalPages = result.data.item2;

        var pagination = document.getElementById('ekmCouponPagination');

        if (pagination && enablePagination) {
          pagination.addEventListener('change', function (a, b) {

            $scope.page = a.target.current;
            
            $scope.getCoupons(false);
          });
        }

      });
      
  };

  $scope.search = function (query) {
    $scope.query = query;
    $scope.getCoupons(false);
  };

  $scope.delete = function (couponCode) {

    $http.delete(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/' + couponCode + '/discountId/' + key)
      .then(function () {

        $scope.getCoupons(false);

        notificationsService.success("Success", "Coupon Code removed");

      });

  };

  $scope.getCoupons(true);


});
