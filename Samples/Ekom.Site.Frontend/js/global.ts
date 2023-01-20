import Blazy from 'utilities/blazy';
import Variants from 'components/variants';
import Cart from 'components/cart';
import Currencies from 'components/currencies';

var global = {
	init: function () {

		Blazy.init();
		Variants.init();
    Cart.init();
    Currencies.init();
	}
};


export default global.init();
