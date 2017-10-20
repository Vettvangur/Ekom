import BLazy from 'blazy';

var blazy = {

    init: function () {

        var el = document.getElementsByClassName('blazy')[0];

        if (el) {

            new BLazy({
                selector: '.blazy',
                success: function (ele) {
                    ele.className += " loaded";

                    if (ele.tagName == "IMG") {
                        ele.parentElement.className += " loaded";
                    }
                },
                breakpoints: [{
                    width: 420 // max-width
                    , src: 'data-src-small'
                }
                    , {
                    width: 768 // max-width
                    , src: 'data-src-medium'
                }]
            });

        }
    }
};

export default blazy;