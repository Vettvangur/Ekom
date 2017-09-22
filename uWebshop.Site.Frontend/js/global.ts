import Blazy from 'utilities/blazy';
import Menu from 'components/menu';
import Election from 'components/election';

var global = {
    init: function () {

        Blazy.init();
        Menu.init();
        Election.init();
    }
};


export default global.init();
