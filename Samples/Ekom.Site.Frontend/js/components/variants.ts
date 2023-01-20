var variants = {

	init: function () {

		let el = document.getElementsByClassName('product__variant-groups')[0]

		if (el) {

			let groups = el.getElementsByTagName('button');

			for (let i = 0; i < groups.length; i++) {

				let group = groups[i];

				group.addEventListener('click', function (e) {

					for (let a = 0; a < groups.length; a++) {
						groups[a].classList.remove('product__groups-item--is-active');
					}

					group.classList.add('product__groups-item--is-active');

					let key = group.getAttribute('data-key');

					let variantEls = document.querySelectorAll('.product__variant');

					for (let b = 0; b < variantEls.length; b++) {
						variantEls[b].classList.remove('product__variant--is-active');
					}

					let variantEl = document.querySelector('.product__variant[data-key="' + key + '"]');

					if (variantEl) {
						variantEl.classList.add('product__variant--is-active');

						let dropdowns = document.querySelectorAll('.product__variant select');

						for (let c = 0; c < dropdowns.length; c++) {
							dropdowns[c].removeAttribute('name');
						}

						let dropdown = variantEl.querySelector('select');

						dropdown.setAttribute('name', dropdown.getAttribute('data-name'));

            var firstItem = dropdown.options[dropdown.options.selectedIndex];

            if (firstItem) {
             document.getElementsByClassName('product__price')[0].innerHTML = firstItem.getAttribute('data-price');
            }

					}

				});
			}

		}

	}
};

export default variants;
