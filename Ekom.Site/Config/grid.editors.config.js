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
                    "name": "Caption",
                    "alias": "caption",
                    "propretyType": {},
                    "dataType": "0cc0eba1-9960-42c9-bf9b-60e150b429ae"
                },
                {
                    "name": "Products",
                    "alias": "products",
                    "propretyType": {},
                    "dataType": "cab8c907-f3cb-442c-af42-754469d269b1"
                }
            ]
        }
    },
    {
        "name": "Test",
        "alias": "test",
        "view": "/App_Plugins/LeBlender/editors/leblendereditor/LeBlendereditor.html",
        "icon": "icon-vcard",
        "render": "/App_Plugins/LeBlender/editors/leblendereditor/views/Base.cshtml",
        "config": {
            "editors": [
                {
                    "name": "testing",
                    "alias": "testing",
                    "propretyType": {},
                    "dataType": "75e484b5-66b9-4d86-b651-5ebb7a3c580b"
                },
                {
                    "name": "asdasdasd",
                    "alias": "asdasdasd",
                    "propretyType": {},
                    "dataType": "f5929201-bac5-4046-adde-cc73e717d6f6"
                },
                {
                    "name": "Color",
                    "alias": "color",
                    "propretyType": {},
                    "dataType": "0225af17-b302-49cb-9176-b9f35cab9c17"
                }
            ],
            "frontView": "/views/partials/leblender/FeaturedProducts.cshtml"
        }
    }
]