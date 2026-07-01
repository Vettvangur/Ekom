angular.module('umbraco').controller('Ekom.Coupon', function ($scope, $routeParams, editorState, $http, notificationsService) {
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

  $scope.openGenerateOverlay = function () {

    var model = {
      count: 10,
      numberAvailable: 1,
      prefix: '',
      randomLength: 8,
      characterSet: 'UppercaseAlphanumeric'
    };

    $scope.overlay = {
      title: "Generate Coupons",
      view: "/App_Plugins/Ekom/DataTypes/CouponEditor/ekmCouponGenerateOverlay.html",
      editModel: model,
      show: true,
      submit: function (submitModel) {

        if (!submitModel.editModel.count || submitModel.editModel.count <= 0) {
          notificationsService.error("Error", "Amount must be greater than zero");
          return;
        }

        if (submitModel.editModel.numberAvailable === '' || submitModel.editModel.numberAvailable < 0) {
          notificationsService.error("Error", "Usage Limit is required and can not be negative");
          return;
        }

        if (!submitModel.editModel.randomLength || submitModel.editModel.randomLength <= 0) {
          notificationsService.error("Error", "Random length must be greater than zero");
          return;
        }

        $http.post(Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/generate/discountId/' + key, submitModel.editModel)
          .then(function (result) {
            $scope.getCoupons(false);
            $scope.closeOverlay();

            notificationsService.success("Success", "Generated " + result.data.created + " coupon codes");
          }, function errorCallback() {
            notificationsService.error("Error", "Error on generating coupons.");
          });

      },
      close: function () {
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

  $scope.exportCoupons = function () {
    window.location = Umbraco.Sys.ServerVariables.ekom.backofficeApiEndpoint + 'coupon/export/discountId/' + key;
  };

  $scope.getCoupons(true);


});
