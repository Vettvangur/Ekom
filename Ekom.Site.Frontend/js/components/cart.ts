import formToObject from 'utilities/formToObject';

var cart = {

  init() {
    var me = this;

    var form = document.getElementsByClassName("product__form")[0];

    if (form) {

      form.addEventListener("submit", function(e) {
        e.preventDefault();

        me.submit(form);
      });

    }
  },

  submit(form) {
    var me = this;

    var button = form.getElementsByTagName("button")[0];

    button.disabled = true;

    var data = formToObject.formToObject(form);

    var xhr = new XMLHttpRequest();
    xhr.open(form.getAttribute("method"), form.getAttribute("action"));
    xhr.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
    xhr.responseType = 'json';

    xhr.onload = ev => {
      var resp = xhr.response;

      if (typeof resp === 'string') {
        resp = JSON.parse(resp);
      }

      if (xhr.status >= 200 && xhr.status < 300) {

        if (resp.success) {

          me.updateQty(resp.orderInfo.TotalQuantity);

        } else {
          alert('Error in order');
        }

      } else {
        alert('Error in service');
      }

    };

    xhr.send(data);
  },

  updateQty(qty) {
    var headerCart = document.getElementsByClassName('header__cart__qty')[0];

    if (headerCart) {
      headerCart.innerHTML = qty;
    }
  },

};

export default cart;
