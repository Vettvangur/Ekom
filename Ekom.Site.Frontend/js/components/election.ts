import formToObject from 'utilities/formToObject';

var election = {

	init: function () {

		var form = document.getElementsByClassName("election")[0];

		if (form) {

			form.addEventListener("submit", function (e) {
				e.preventDefault();

				election.submit(form);
			});

		}
	},

	submit: function (form) {

		var button = form.getElementsByTagName("button")[0];

		button.disabled = true;

		var data = formToObject.formToObject(form);


		var xhr = new XMLHttpRequest();
		xhr.open(form.getAttribute("method"), form.getAttribute("action"));
		xhr.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
		xhr.responseType = 'json';


		var message = form.getElementsByClassName("message")[0];

		if (message) {
			message.remove();
		}

		xhr.onload = ev => {
			var resp = xhr.response;

			if (typeof resp === 'string') {
				resp = JSON.parse(resp);
			}

			if (xhr.status >= 200 && xhr.status < 300) {

				if (resp.success) {
					election.message(resp.message, form, 'success');
					button.disabled = false;
				} else {
					button.disabled = false;
					election.message(resp.message, form, 'error');
				}

			} else {
				button.disabled = false;
				election.message(resp.message, form, 'error');
			}

		};

		xhr.send(data);
	},

	message: function (message, form, status) {
		form.classList.add('hold');

		var m = document.createElement('div');
		m.classList.add('message');
		m.classList.add(status);
		m.innerHTML = '<strong>' + message + '</strong>';

		m.addEventListener('click', function () {
			election.reset(form);
		});

		form.insertAdjacentElement('beforeend', m);
	},

	reset: function (form) {

		var election = form.querySelector('input[name=Election]').value;

		form.reset();

		form.querySelector('input[name=Election]').value = election;

		form.classList.remove('hold');

		var message = form.getElementsByClassName("message")[0];

		if (message) {
			message.remove();
		}
	}

};

export default election;
