var currencies = {

  init() {

    var forms = document.getElementsByClassName("order__changeCurrency-form");
    var me = this;

    for (let i = 0; i < forms.length; i++) {

      let form = forms[i];

      form.addEventListener("submit", function (e) {
        e.preventDefault();

        me.submit(form);
      });

    }

  },

  submit(form) {

    var data = new FormData(form);

    var xhr = new XMLHttpRequest();
    xhr.open(form.getAttribute("method"), form.getAttribute("action"));

    xhr.onload = function () {

      var resp = JSON.parse(xhr.response);

      if (xhr.status >= 200 && xhr.status < 300) {

        if (resp.success) {

          window.location.reload();

        } else {

          alert('error');

        }

      } else {
        alert('error');
      }

    };

    xhr.send(data);

  }

};

export default currencies;
