var menu = {

    init: function () {


        var hamburger = document.getElementsByClassName('hamburger')[0];

        if (hamburger) {

            hamburger.addEventListener('click', function (e) {
                e.preventDefault();

                this.classList.toggle('open');

                var nav = document.getElementsByTagName('nav')[0];

                if (nav) {
                    nav.classList.toggle('open');
                }

            });

        }

    }
};

export default menu;