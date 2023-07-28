(function () {
  "use strict";

  function controller($scope, $routeParams, notificationsService, resources, $location, $document, $filter) {
    $scope.loading = true;
    $scope.result = {};


    var currentDate = new Date(); // get the current date
    var januaryFirstCurrentYear = new Date(currentDate.getFullYear(), 0, 1); // get January 1st of the current year
    
    var dateFrom = januaryFirstCurrentYear.toISOString().substring(0, 10);
    var dateTo = currentDate.toISOString().substring(0, 10);

    $scope.orderCount = 0;
    $scope.grandTotal = 0;
    $scope.averageAmount = 0;
    $scope.page = 1;
    $scope.query = "";
    $scope.statusList = [];
    $scope.visibleDropdowns = {};
    $scope.labelDropdowns = {};
    $scope.orderStatus = 'CompletedOrders';
    $scope.dateFrom = dateFrom;
    $scope.dateTo = dateTo;
    $scope.selectedStore = {};
    $scope.stores = [];
    
    var path = $location.path(); // get the path
    var pathComponents = path.split('/'); // split the path into components
    var lastPathComponent = pathComponents[pathComponents.length - 1]; // get the last component

    lastPathComponent = lastPathComponent == 'ekommanager' ? 'orders' : lastPathComponent;

    $scope.location = lastPathComponent;

    var tabsElement = document.getElementById('ekmNavigationTabs')
     
    if (lastPathComponent === 'orders' || lastPathComponent === 'analytics') {

      var allTabs = tabsElement.querySelectorAll('uui-tab')

      for (var i = 0; i < allTabs.length; i++) {
        if (allTabs[i].innerText.trim().toLowerCase() === lastPathComponent.toLowerCase()) {

          allTabs[i].setAttribute('active', '');
        } else {
          allTabs[i].removeAttribute('active');
        }
      }
    }

    $scope.DatePickers = function () {
      var datePickers = document.getElementsByClassName('ekmManager__datepicker');

      for (var i = 0; i < datePickers.length; i++) {
        var datePicker = datePickers[i]

        datePicker.addEventListener('change', function (e) {

          if (e.target.name === 'dateFrom') {
            $scope.dateFrom = e.target.value;
          }
          if (e.target.name === 'dateTo') {
            $scope.dateTo = e.target.value;
          }

          $scope.GetData();

        });
      }
    };

    $scope.GetData = function () {

      resources.SearchOrders('?start=' + $scope.dateFrom + '&end=' + $scope.dateTo + '&orderStatus=' + $scope.orderStatus + '&page=' + $scope.page + '&pagesize=20&query=' + $scope.query + '&store=' + $scope.store.alias)
        .then(function (result) {

          $scope.loading = false;

          $scope.result = result.data;

          $scope.orderCount = $scope.result.count;
          $scope.grandTotal = $scope.result.grandTotal;
          $scope.averageAmount = $scope.result.averageAmount;

          setTimeout(function () {
            $scope.DatePickers();
          }, 50); 

        }, function errorCallback(data) {
          $scope.loading = false;
          notificationsService.error("Error", "Error on searching data.");
        })
    };

    $scope.GetStatusList = function () {

      resources.StatusList()
        .then(function (result) {

          $scope.statusList = result.data;

        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on getting status list.");
        })
    };

    $scope.GetStores = function () {

      resources.Stores()
        .then(function (result) {
          
          $scope.stores = result.data;

          $scope.store = $scope.stores[0];
          $scope.labelDropdowns['dropdownStores'] = $scope.store.alias;

          $scope.GetData();

        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on getting stores.");
        })
    };

    tabsElement.addEventListener('click', function (e) {
      var target = e.target;

      var text = target.innerText.toLowerCase();

      // Join the path components back together and set the new path
      $location.path('/ekommanager/' + text);
    })

    $scope.ViewOrder = function (order) {

      resources.OrderInfo(order.uniqueId)
        .then(function (result) {

          var orderInfo = result.data;
          
          var shippingAddress = {};
          var sameAsShipping = orderInfo.customerInformation.shipping;
          if (
            orderInfo.customerInformation.shipping.name === '' &&
            orderInfo.customerInformation.shipping.address === '') {
            shippingAddress = orderInfo.customerInformation.customer;
            sameAsShipping = true;
          }

          var model = {
            order: orderInfo,
            shippingAddress,
            sameAsShipping
          };

          console.log(model);

          $scope.overlay = {
            title: "View Order",
            view: "/App_Plugins/Ekom/Manager/views/overlays/ekmOrder.html",
            editModel: model,
            show: true,
            submit: function (submitModel) {
  
            },
            close: function (oldModel) {
              $scope.CloseModal();
            }
          };

        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on getting orderInfo.");
        })

    };

    $scope.CloseModal = function () {
      $scope.overlay.show = false;
      $scope.overlay = null;
      document.body.classList.remove('tabbing-active');
    };

    $scope.setPage = function (page) {
      $scope.page = page.toString().replace('...', '');
      $scope.GetData();
    };

    $scope.search = function (query) {
      $scope.query = query;
      $scope.GetData();
    };

    $scope.pageRange = function () {
      var rangeSize = 5;
      var ret = [];
      var start;

      if ($scope.page <= Math.floor(rangeSize / 2)) {
        start = 1;
      } else if (parseInt($scope.page) + Math.floor(rangeSize / 2) >= $scope.result.totalPages) {
        start = Math.max($scope.result.totalPages - rangeSize + 1, 1);
      } else {
        start = $scope.page - Math.floor(rangeSize / 2);
      }

      for (var i = 0; i < rangeSize; i++) {
        var pageNumber = start + i;
        if (pageNumber <= $scope.result.totalPages) {
          ret.push(pageNumber);
        }
      }

      // If last page is not already in the list, add it.
      if (ret[ret.length - 1] < $scope.result.totalPages) {
        ret.push('...' + $scope.result.totalPages);
      }

      // If first page is not already in the list, add it at the beginning.
      if (ret[0] > 1) {
        ret.unshift('...' + 1);
      }

      return ret;
    };

    $scope.analytics = function () {

      resources.Charts('?start=2000-01-01&end=2030-01-01&orderStatus=CompletedOrders')
        .then(function (result) {


          $scope.revenueChart = result.data.revenueChart;

          $scope.renderChart('chartRevenue', $scope.revenueChart.labels, $scope.revenueChart.points);

          $scope.ordersChart = result.data.ordersChart;

          $scope.renderChart('chartOrders', $scope.ordersChart.labels, $scope.ordersChart.points);

          $scope.avarageChart = result.data.avarageChart;

          $scope.renderChart('chartAvarage', $scope.avarageChart.labels, $scope.avarageChart.points);

        }, function errorCallback(data) {
          $scope.loading = false;
          notificationsService.error("Error", "Error on chart data.");
        });

      resources.MostPopularProducts()
        .then(function (result) {

          console.log(result.data);

        }, function errorCallback(data) {

          notificationsService.error("Error", "Error on most popular products data.");
        });
      
    };

    $scope.renderChart = function (chartId, labels, dataset1, dataset2) {

      var chartConfig = {
        data: {
          labels: labels,
          datasets: [{
            label: 'Dataset 1',
            data: dataset1,
            lineTension: 0,
            yAxisID: 'y-axis-1',
            backgroundColor: 'rgba(255, 255, 255, 0)',
            borderColor: 'rgba(0, 123, 255, 1)',
            borderWidth: 1
          }, {
            label: 'Dataset 2',
            data: dataset2,
            lineTension: 0,
            yAxisID: 'y-axis-2',
            backgroundColor: 'rgba(255, 255, 255, 0)',
            borderColor: 'rgba(255, 99, 132, 1)',
            borderWidth: 1
          }]
        },
        type: 'line',
        options: {
          elements: {
            point: {
              radius: 4,
              hitRadius: 2,
              hoverRadius: 6
            }
          },
          responsive: true,
          interaction: {
            mode: 'index',
            intersect: false,
          },
          tooltips: {
            callbacks: {
              label: function (tooltipItem, data) {
                if (parseInt(tooltipItem.yLabel) >= 1000) {
                  return tooltipItem.yLabel.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ".");
                } else {
                  return tooltipItem.yLabel;
                }
              }
            }
          },
          stacked: false,
          scales: {
            xAxes: [{
              type: 'time',
              time: {
                unit: 'month',
              },
              ticks: {
                source: 'auto',
              }
            }],
            yAxes: [{
              id: 'y-axis-1',
              type: 'linear',
              position: 'left',
              ticks: {
                beginAtZero: true,
                callback: function (value, index, values) {
                  if (parseInt(value) >= 1000) {
                    return value.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ".");
                  } else {
                    return value;
                  }
                }
              },

            }, {
              id: 'y-axis-2',
              type: 'linear',
              position: 'right',
              ticks: {
                beginAtZero: true
              }
            }]
          }
        }
      };

      var ctx = document.getElementById(chartId).getContext('2d');

      var myChart = new Chart(ctx, chartConfig);
    };

    $scope.toggleDropdown = function (dropdownId) {
      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];
    };

    $scope.isDropdownVisible = function (dropdownId) {
      return $scope.visibleDropdowns[dropdownId];
    };

    $scope.selectDropdown = function (dropdownId, status) {

      $scope.visibleDropdowns[dropdownId] = !$scope.visibleDropdowns[dropdownId];

      $scope.labelDropdowns[dropdownId] = status;

      if (dropdownId === 'dropdownStatusList') {
        $scope.orderStatus = status;
      }
      
      $scope.GetData();
    };

    $scope.labelDropdown = function (dropdownId, defaultText) {

      var label = $scope.labelDropdowns[dropdownId];

      label = label || defaultText;

      return label;

    };

    $document.on('click', function (event) {
      // Check if the click event target is outside of any dropdown
      var isClickInsideDropdown = false;
      var dropdownElements = $document[0].querySelectorAll('.dropdown-toggle');

      for (var i = 0; i < dropdownElements.length; i++) {
        if (dropdownElements[i].contains(event.target)) {
          isClickInsideDropdown = true;
          break;
        }
      }

      // Hide all dropdowns if the click is outside
      if (!isClickInsideDropdown) {
        $scope.$apply(function () {
          for (var dropdown in $scope.visibleDropdowns) {
            $scope.visibleDropdowns[dropdown] = false;
          }
        });
      }
    });


    angular.element(document).ready(function () {
      // Init Orders
      if ($scope.location === 'orders') {
        $scope.GetStores();
        $scope.GetStatusList();
      }

      // Init Analytics
      if ($scope.location === 'analytics') {
        setTimeout(function () {
          $scope.analytics();
        }, 250);
      }
    });

  }

  angular.module("umbraco").controller("Ekom.Manager.Dashboard", [
    "$scope",
    "$routeParams",
    "notificationsService",
    "Ekom.Manager.Resources",
    "$location",
    "$document", 
    "$filter",
    controller
  ]);
})();
