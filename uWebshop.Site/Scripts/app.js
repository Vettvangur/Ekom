$(document).foundation()


$(document).ready(function () {


    console.log('init');

    $('.add-to-order').on('submit', function (e) {
        e.preventDefault();

        var $form = $(this);

        var data = $form.serialize();

        var jqxhr = $.post($form.attr('action'), data, function (resp) {

        })
        .done(function (resp) {

            if (resp.success) {
                alert('added to order! Total: ' + resp.orderInfo.ChargedAmount.Value);
            } else {
                alert('Error: ' + resp.error)
            }

        })
        .fail(function () {

        })
        .always(function () {

        });

    });

    $('.order-remove-line').on('click', function (e) {
        e.preventDefault();

        var $btn = $(this);

        var jqxhr = $.post('/umbraco/uWebshop/order/RemoveOrderLine?lineId=' + $btn.attr('data-lineId') + '&storeAlias=' + $btn.attr('data-store'), function (resp) {

        })
            .done(function (resp) {

                if (resp.success) {
                    alert('Removed from order!');
                } else {
                    alert('Error: ' + resp.error)
                }

            })
            .fail(function () {

            })
            .always(function () {

            });
    });
});