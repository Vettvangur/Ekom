angular.module('umbraco').controller('Ekom.Stock', function($scope, assetsService, contentEditingHelper, $routeParams, editorState, $http, notificationsService, contentResource) {
    $scope.stores = [];
    $scope.perStoreStock = false;
    $scope.stock = {};
    $scope.config = {};

    $scope.Init = function() {

        contentResource.getById($routeParams.id)
            .then(function(data) {
                $scope.content = data;

                $http.get('/umbraco/backoffice/ekom/api/GetConfig').then(function(result) {

                    $scope.config = result.data;

                    if ($scope.config.PerStoreStock) {

                        $scope.perStoreStock = true;

                        $http.get('/umbraco/backoffice/ekom/api/getAllStores').then(function(results) {

                            $scope.stores = results.data;

                            for (s = 0; $scope.stores.length > s; s += 1) {

                                var store = $scope.stores[s];

                                $scope.GetStock(store.Alias);
                            }

                        });

                    } else {
                        //$scope.GetStock('');
                    }

                });

            });

    };

    if ($routeParams.section !== 'settings') {
        $scope.Init();
    }

    $scope.GetStock = function(storeAlias) {

        if (storeAlias !== '') {
            $http.get('/umbraco/backoffice/ekom/api/GetStockByStore?id=' + $scope.content.key + "&storeAlias=" + storeAlias)
                .then(function(result) {

                    $scope.stock[storeAlias] = parseInt(result.data);

                });
        } else {
            $http.get('/umbraco/backoffice/ekom/api/GetStock?id=' + $scope.content.key).then(function(result) {

                $scope.stock = parseInt(result.data);
            });
        }

    };

    $scope.UpdateStock = function(storeAlias) {

        $scope.Update($scope.stock[storeAlias], storeAlias);

    };

    $scope.Update = function(stockValue, storeAlias) {

        $http.post('/umbraco/backoffice/ekom/api/setStock?id=' + $scope.content.key + "&stock=" + stockValue + "&storeAlias=" + storeAlias)
            .then(
                function() {
                    notificationsService.success("Success", "Stock has been updated");
                },
                function() {
                    notificationsService.error("Update Failed.", "Stock update failed");
                });
    };


    //$scope.storeList = [];
    //$scope.content = null;
    //$scope.stockValue = 0;
    //$scope.storeSelected = '';
    //$scope.config = null;
    //$scope.perStoreStock = false;

    //$scope.GetStores = function() {
    //    $http.get('/umbraco/backoffice/ekom/api/getAllStores').then(function(results) {

    //        $scope.storeList = results.data;

    //        if ($scope.storeList.length > 0) {
    //            $scope.storeSelected = $scope.storeList[0].Alias;
    //            $scope.GetStock($scope.storeList[0].Alias);
    //        }

    //    });
    //};

    //$scope.GetStock = function(storeAlias) {

    //    if (storeAlias !== '') {
    //        $http.get('/umbraco/backoffice/ekom/api/GetStockByStore?id=' + $scope.content.key + "&storeAlias=" + storeAlias)
    //            .then(function(result) {

    //                $scope.stockValue = parseInt(result.data);

    //            });
    //    } else {
    //        $http.get('/umbraco/backoffice/ekom/api/GetStock?id=' + $scope.content.key).then(function(result) {

    //            $scope.stockValue = parseInt(result.data);

    //        });
    //    }

    //};

    //$scope.UpdateStock = function() {

    //    $http.post('/umbraco/backoffice/ekom/api/setStock?id=' + $scope.content.key + "&stock=" + $scope.stockValue + "&storeAlias=" + $scope.storeSelected)
    //        .success(function() {
    //            notificationsService.success("Success", "Stock has been updated");
    //        })
    //        .error(function() {
    //            notificationsService.error("Update Failed.", "Stock update failed");
    //        });
    //};

    //$scope.UpdateStore = function() {
    //    $scope.GetStock($scope.storeSelected);
    //};

    //if ($routeParams.section !== 'settings') {

    //    contentResource.getById($routeParams.id)
    //        .then(function(data) {
    //            $scope.content = data;

    //            $http.get('/umbraco/backoffice/ekom/api/GetConfig').then(function(result) {

    //                $scope.config = result.data;

    //                if ($scope.config.PerStoreCache) {

    //                    $scope.perStoreStock = true;

    //                    $scope.GetStores();
    //                } else {
    //                    $scope.GetStock('');
    //                }

    //            });

    //        });
    //}


});
