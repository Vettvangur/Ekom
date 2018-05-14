export default {
  
  getOrders: function () {
    return fetch('/umbraco/ekom/managerapi/getorders').then(function (response) {
      return response.json();
    }).then(function (result) {
      return result;
    });
  }
}
