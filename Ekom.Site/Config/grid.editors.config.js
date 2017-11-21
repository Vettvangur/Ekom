[
    {
        "name": "Rich text editor",
        "alias": "rte",
        "view": "rte",
        "icon": "icon-article"
    },
    {
        "name": "Macro",
        "alias": "macro",
        "view": "macro",
        "icon": "icon-settings-alt"
    },
    {
        "name": "Featured Products",
        "alias": "featuredProducts",
        "view": "/App_Plugins/LeBlender/editors/leblendereditor/LeBlendereditor.html",
        "icon": "icon-thumbnail-list",
        "render": "/App_Plugins/LeBlender/editors/leblendereditor/views/Base.cshtml",
        "config": {
            "renderInGrid": "1",
            "frontView": "/views/partials/leblender/FeaturedProducts.cshtml",
            "editors": [
                {
                    "name": "Products",
                    "alias": "products",
                    "propretyType": {},
                    "dataType": "cab8c907-f3cb-442c-af42-754469d269b1"
                }
            ]
        }
    }
]