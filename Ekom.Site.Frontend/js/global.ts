import Blazy from 'utilities/blazy';
import Variants from 'components/variants';
import Cart from 'components/cart';

var global = {
	init: function () {

		Blazy.init();
		Variants.init();
		Cart.init();
	}
};


export default global.init();
