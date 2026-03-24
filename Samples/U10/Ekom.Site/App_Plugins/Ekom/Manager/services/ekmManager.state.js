(function () {
  "use strict";

  function factory() {
    var currentDate = new Date();
    var januaryFirstCurrentYear = new Date(currentDate.getFullYear(), 0, 1);

    var state = {
      filters: {
        dateFrom: januaryFirstCurrentYear.toISOString().substring(0, 10),
        dateTo: currentDate.toISOString().substring(0, 10),
        orderStatus: "CompletedOrders",
        store: "",
        paymentProvider: "",
        productSku: "",
        query: ""
      },
      statusList: [],
      paymentProviders: [],
      currentOrderStatus: null
    };

    return {
      getState: function () {
        return state;
      },
      setStore: function (store) {
        state.filters.store = store || "";
      },
      setPaymentProvider: function (paymentProvider) {
        state.filters.paymentProvider = paymentProvider || "";
      },
      setProductSku: function (productSku) {
        state.filters.productSku = productSku || "";
      },
      clearPaymentProvider: function () {
        state.filters.paymentProvider = "";
      },
      setStatusList: function (statusList) {
        state.statusList = statusList || [];
      },
      setPaymentProviders: function (paymentProviders) {
        state.paymentProviders = paymentProviders || [];
      },
      setCurrentOrderStatus: function (orderStatus) {
        state.currentOrderStatus = orderStatus || null;
      }
    };
  }

  angular.module("umbraco").factory("Ekom.Manager.State", [factory]);
})();
