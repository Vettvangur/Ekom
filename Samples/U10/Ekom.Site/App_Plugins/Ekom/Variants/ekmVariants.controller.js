angular.module('umbraco').controller('Ekom.Variants', function ($element, $http, $scope, editorService, editorState, notificationsService) {
    var vm = this;

    vm.loading = true;
    vm.busy = false;
    vm.error = '';
    vm.status = '';
    vm.product = null;
    vm.activeLanguage = '';
    vm.expandedGroupIds = {};
    vm.selectedGroupIds = {};
    vm.selectedVariantIds = {};
    vm.deletedNodeIds = [];
    vm.groupSnapshots = {};
    vm.variantSnapshots = {};
    vm.drawer = null;

    var nextDraftId = -1;

    vm.load = function () {
        vm.loading = true;
        vm.error = '';

        $http.get('/ekom/backoffice/Variants/' + encodeURIComponent(editorState.current.id))
            .then(function (response) {
                vm.product = response.data;
                vm.activeLanguage = (vm.product.languages[0] || {}).isoCode || '';
                vm.expandedGroupIds = {};
                vm.selectedGroupIds = {};
                vm.selectedVariantIds = {};
                vm.deletedNodeIds = [];
                vm.drawer = null;
                if (vm.product.groups.length === 1) {
                    vm.expandedGroupIds[vm.product.groups[0].id] = true;
                }
                resetSnapshots();
                setBadge();
            })
            .catch(function (error) {
                vm.error = getErrorMessage(error, 'Could not load variants.');
                notificationsService.error('Variants', vm.error);
            })
            .finally(function () {
                vm.loading = false;
            });
    };

    vm.createGroup = function () {
        var id = nextDraftId--;
        var group = {
            id: id,
            key: '00000000-0000-0000-0000-000000000000',
            name: 'New variant group',
            title: '',
            titleValues: createEmptyTitleValues(),
            color: '',
            images: '',
            sortOrder: vm.product.groups.length,
            published: false,
            customFields: createCustomFields(vm.product.variantGroupFields),
            variants: []
        };

        vm.product.groups.push(group);
        vm.expandedGroupIds[id] = true;
        vm.drawer = { type: 'group', group: group, item: group };
    };

    vm.createVariant = function (group) {
        var id = nextDraftId--;
        var variant = {
            id: id,
            key: '00000000-0000-0000-0000-000000000000',
            name: 'New variant',
            title: '',
            titleValues: createEmptyTitleValues(),
            sku: '',
            images: '',
            priceValues: {},
            stockValues: createEmptyStockValues(),
            customFields: createCustomFields(vm.product.variantFields),
            sortOrder: group.variants.length,
            published: false
        };

        group.variants.push(variant);
        vm.expandedGroupIds[group.id] = true;
        vm.drawer = { type: 'variant', group: group, item: variant };
    };

    vm.saveAll = function () {
        if (!validateAllTitles()) {
            return;
        }

        if (!validateAllCustomFields()) {
            return;
        }

        if (!vm.hasChanges()) {
            vm.status = 'No changes to save.';
            notificationsService.info('Variants', 'No changes to save.');
            return;
        }

        saveVariantChanges();
    };

    $scope.$on('formSubmitting', function () {
        if (vm.product && vm.hasChanges()) {
            saveVariantChanges({ silentNoChanges: true });
        }
    });

    vm.deleteSelected = function () {
        var count = selectedCount();

        if (!count || !window.confirm('Delete ' + count + ' selected item' + (count === 1 ? '' : 's') + '?')) {
            return;
        }

        (vm.product.groups || []).forEach(function (group) {
            if (vm.selectedGroupIds[group.id] && !isDraft(group.id)) {
                addDeletedNodeId(group.id);
            }

            group.variants.forEach(function (variant) {
                if (vm.selectedVariantIds[variant.id] && !isDraft(variant.id)) {
                    addDeletedNodeId(variant.id);
                }
            });
        });

        vm.product.groups = (vm.product.groups || []).filter(function (group) {
            return !vm.selectedGroupIds[group.id];
        }).map(function (group) {
            group.variants = group.variants.filter(function (variant) {
                return !vm.selectedVariantIds[variant.id];
            });
            return group;
        });

        vm.selectedGroupIds = {};
        vm.selectedVariantIds = {};
        vm.drawer = null;
    };

    vm.toggleGroup = function (group) {
        vm.expandedGroupIds[group.id] = !vm.expandedGroupIds[group.id];
    };

    vm.editGroup = function (group) {
        vm.drawer = { type: 'group', group: group, item: group };
    };

    vm.editVariant = function (group, variant) {
        vm.drawer = { type: 'variant', group: group, item: variant };
    };

    vm.closeDrawer = function () {
        vm.drawer = null;
    };

    vm.saveDrawer = function () {
        if (!validateTitle(vm.drawer && vm.drawer.item) || !validateCustomFields(vm.drawer && vm.drawer.item && vm.drawer.item.customFields)) {
            return;
        }

        vm.drawer = null;
    };

    vm.deleteDrawerItem = function () {
        if (!vm.drawer) {
            return;
        }

        var isGroup = vm.drawer.type === 'group';
        var label = isGroup ? 'variant group' : 'variant';

        if (!window.confirm('Delete this ' + label + '?')) {
            return;
        }

        if (isGroup) {
            if (!isDraft(vm.drawer.item.id)) {
                addDeletedNodeId(vm.drawer.item.id);
            }

            vm.product.groups = (vm.product.groups || []).filter(function (group) {
                return group.id !== vm.drawer.item.id;
            });
        } else {
            if (!isDraft(vm.drawer.item.id)) {
                addDeletedNodeId(vm.drawer.item.id);
            }

            vm.drawer.group.variants = vm.drawer.group.variants.filter(function (variant) {
                return variant.id !== vm.drawer.item.id;
            });
        }

        vm.drawer = null;
    };

    vm.drawerTitle = function () {
        if (!vm.drawer) {
            return '';
        }

        return vm.getTitleValue(vm.drawer.item) || vm.drawer.item.name || (vm.drawer.type === 'group' ? 'New variant group' : 'New variant');
    };

    vm.drawerSubtitle = function () {
        if (!vm.drawer) {
            return '';
        }

        return vm.drawer.type === 'group' ? 'variant group' : (vm.getTitleValue(vm.drawer.group) || 'Group') + ' / variant';
    };

    vm.setLanguage = function (value) {
        vm.activeLanguage = value;
    };

    vm.getTitleValue = function (item) {
        ensureTitleValues(item);
        return item.titleValues[vm.activeLanguage] || firstValue(item.titleValues) || item.title || '';
    };

    vm.setTitleValue = function (item, value) {
        ensureTitleValues(item);
        item.titleValues[vm.activeLanguage] = value;
        item.title = firstValue(item.titleValues) || item.name;
    };

    vm.getPrice = getVariantPrice;

    vm.setPrice = function (variant, storeAlias, currency, value) {
        if (!variant.priceValues) {
            variant.priceValues = {};
        }

        var prices = variant.priceValues[storeAlias] || [];
        var price = prices.filter(function (item) { return getCurrency(item) === currency; })[0];
        var numericValue = Number(value) || 0;

        if (price) {
            price.Price = numericValue;
            price.price = numericValue;
        } else {
            prices.push({ Currency: currency, Price: numericValue });
        }

        variant.priceValues[storeAlias] = prices;
    };

    vm.getStockValue = function (variant, storeAlias) {
        var stock = (variant.stockValues || []).filter(function (item) { return item.storeAlias === storeAlias; })[0];
        return stock ? stock.value || 0 : 0;
    };

    vm.setCustomField = function (item, field, value) {
        ensureCustomFields(item);
        var customField = item.customFields.filter(function (current) { return current.alias === field.alias; })[0];

        if (customField) {
            customField.value = value;
        }
    };

    vm.setStockValue = function (variant, storeAlias, value) {
        if (!variant.stockValues) {
            variant.stockValues = [];
        }

        var stock = variant.stockValues.filter(function (item) { return item.storeAlias === storeAlias; })[0];
        if (!stock) {
            stock = { storeAlias: storeAlias, value: 0 };
            variant.stockValues.push(stock);
        }

        stock.value = Number(value) || 0;
    };

    vm.priceRows = function () {
        if (!vm.drawer || vm.drawer.type !== 'variant') {
            return [];
        }

        var rows = [];
        (vm.product.stores || []).forEach(function (store) {
            (store.currencies || []).forEach(function (currency) {
                rows.push({
                    storeAlias: store.alias || '',
                    storeTitle: store.title || store.alias || '',
                    currencyValue: currency.currencyValue || '',
                    currencyLabel: currency.isoCurrencySymbol || currency.currencyValue || '',
                    value: getVariantPrice(vm.drawer.item, store.alias || '', currency.currencyValue || '')
                });
            });
        });

        return rows;
    };

    vm.stockRows = function () {
        if (!vm.drawer || vm.drawer.type !== 'variant') {
            return [];
        }

        return (vm.product.stores || []).map(function (store) {
            var storeAlias = store.alias || '';
            return {
                storeAlias: storeAlias,
                storeTitle: store.title || storeAlias,
                value: vm.getStockValue(vm.drawer.item, storeAlias)
            };
        });
    };

    vm.variantImageHint = function () {
        if (!vm.drawer || vm.drawer.type !== 'variant') {
            return '';
        }

        var ownImages = splitImages(vm.drawer.item.images).length;
        var groupImages = splitImages(vm.drawer.group.images).length;
        return ownImages ? 'Own images override the group images.' : 'No own images — inherits ' + groupImages + ' image(s) from ' + (vm.getTitleValue(vm.drawer.group) || 'group') + '.';
    };

    vm.isGroupChanged = isGroupChanged;
    vm.isVariantChanged = isVariantChanged;
    vm.hasChanges = function () {
        return vm.deletedNodeIds.length > 0 || (vm.product && (vm.product.groups || []).some(function (group) {
            return isGroupChanged(group) || group.variants.some(isVariantChanged);
        }));
    };
    vm.selectedCount = selectedCount;
    vm.isDraft = isDraft;
    vm.imageCount = function (value) { return splitImages(value).length; };
    vm.mediaImages = function (item) { return splitImages(item && item.images); };
    vm.getThumbUrl = getThumbUrl;
    vm.getLanguageLabel = getLanguageLabel;
    vm.getFirstImageThumbUrl = getFirstImageThumbUrl;
    vm.getGroupThumbUrl = function (group) {
        var image = getFirstImage(group.images);

        if (!image) {
            for (var i = 0; i < group.variants.length; i++) {
                image = getFirstImage(group.variants[i].images);

                if (image) {
                    break;
                }
            }
        }

        return getThumbUrl(image);
    };
    vm.getDefaultPrice = function (variant) {
        var store = (vm.product.stores || [])[0];
        var currency = store && (store.currencies || [])[0];

        if (!store || !currency) {
            return '—';
        }

        var value = getVariantPrice(variant, store.alias || '', currency.currencyValue || '');
        var symbol = currency.currencySymbol || currency.isoCurrencySymbol || currency.currencyValue || '';
        return (formatNumber(value) + ' ' + symbol).trim();
    };
    vm.getTotalStock = function (variant) {
        return formatNumber((variant.stockValues || []).reduce(function (total, stock) {
            return total + (Number(stock.value) || 0);
        }, 0));
    };
    vm.openMediaPicker = function (item) {
        editorService.mediaPicker({
            multiPicker: true,
            selection: splitImages(item.images),
            submit: function (model) {
                item.images = normalizeMediaSelection(model.selection).join(',');
                editorService.close();
            },
            close: function () {
                editorService.close();
            }
        });
    };

    bindDragAndDrop();

    function saveVariantChanges(options) {
        if (!validateAllTitles()) {
            return Promise.resolve();
        }

        if (!validateAllCustomFields()) {
            return Promise.resolve();
        }

        var groups = (vm.product.groups || []).map(getChangedGroupForSave).filter(Boolean);

        return saveGroups(groups, options || {});
    }

    function saveGroups(groups, options) {
        options = options || {};

        if (!groups.length && !vm.deletedNodeIds.length) {
            if (!options.silentNoChanges) {
                vm.status = 'No changes to save.';
                notificationsService.info('Variants', 'No changes to save.');
            }

            return Promise.resolve();
        }

        return runAction('Saving changes...', function () {
            var deleteRequests = vm.deletedNodeIds.map(function (id) {
                return $http.delete('/ekom/backoffice/Variants/' + encodeURIComponent(id));
            });

            return Promise.all(deleteRequests).then(function () {
                if (!groups.length) {
                    return vm.load();
                }

                return $http.post('/ekom/backoffice/Variants/Save', {
                    productId: String(editorState.current.id),
                    publish: true,
                    groups: groups
                }).then(function (response) {
                    vm.product = response.data;
                    vm.selectedGroupIds = {};
                    vm.selectedVariantIds = {};
                    vm.deletedNodeIds = [];
                    vm.drawer = null;
                    vm.expandedGroupIds = {};
                    if (vm.product.groups.length === 1) {
                        vm.expandedGroupIds[vm.product.groups[0].id] = true;
                    }
                    resetSnapshots();
                    setBadge();
                });
            });
        });
    }

    function getChangedGroupForSave(group) {
        var variants = group.variants.filter(isVariantChanged).map(markChanged);

        if (!isGroupChanged(group) && !variants.length) {
            return null;
        }

        return angular.extend({}, group, { changed: isGroupChanged(group), variants: variants });
    }

    function markChanged(item) {
        return angular.extend({}, item, { changed: true });
    }

    function runAction(status, action) {
        vm.busy = true;
        vm.status = status;
        vm.error = '';

        return action()
            .then(function () {
                vm.status = 'Done.';
                notificationsService.success('Variants', 'Variant changes were saved.');
            })
            .catch(function (error) {
                vm.error = getErrorMessage(error, 'Action failed.');
                notificationsService.error('Variants', vm.error);
            })
            .finally(function () {
                vm.busy = false;
            });
    }

    function resetSnapshots() {
        vm.groupSnapshots = {};
        vm.variantSnapshots = {};
        (vm.product.groups || []).forEach(function (group) {
            vm.groupSnapshots[group.id] = snapshotGroup(group);
            group.variants.forEach(function (variant) {
                vm.variantSnapshots[variant.id] = snapshotVariant(variant);
            });
        });
    }

    function isGroupChanged(group) {
        return isDraft(group.id) || vm.groupSnapshots[group.id] !== snapshotGroup(group);
    }

    function isVariantChanged(variant) {
        return isDraft(variant.id) || vm.variantSnapshots[variant.id] !== snapshotVariant(variant);
    }

    function createEmptyTitleValues() {
        var values = {};
        (vm.product.languages || []).forEach(function (language) {
            values[language.isoCode || ''] = '';
        });
        return values;
    }

    function createCustomFields(fields) {
        return (fields || []).map(function (field) {
            return angular.extend({}, field, { value: '' });
        });
    }

    function ensureCustomFields(item) {
        if (!item.customFields) {
            item.customFields = [];
        }
    }

    function validateAllCustomFields() {
        var groups = vm.product ? vm.product.groups || [] : [];

        for (var i = 0; i < groups.length; i++) {
            if (!validateCustomFields(groups[i].customFields)) {
                return false;
            }

            for (var j = 0; j < groups[i].variants.length; j++) {
                if (!validateCustomFields(groups[i].variants[j].customFields)) {
                    return false;
                }
            }
        }

        return true;
    }

    function validateAllTitles() {
        var groups = vm.product ? vm.product.groups || [] : [];

        for (var i = 0; i < groups.length; i++) {
            if (!validateTitle(groups[i])) {
                return false;
            }

            for (var j = 0; j < groups[i].variants.length; j++) {
                if (!validateTitle(groups[i].variants[j])) {
                    return false;
                }
            }
        }

        return true;
    }

    function validateTitle(item) {
        if (hasAnyTitleValue(item && item.titleValues)) {
            return true;
        }

        notificationsService.error('Variants', 'Title is required.');
        return false;
    }

    function hasAnyTitleValue(values) {
        return Object.keys(values || {}).some(function (key) {
            return String(values[key] || '').trim();
        });
    }

    function validateCustomFields(fields) {
        var missing = (fields || []).filter(function (field) {
            return field.required && !String(field.value || '').trim();
        })[0];

        if (!missing) {
            return true;
        }

        notificationsService.error('Variants', missing.label + ' is required.');
        return false;
    }

    function createEmptyStockValues() {
        return (vm.product.stores || []).map(function (store) {
            return { storeAlias: store.alias || '', value: 0 };
        });
    }

    function ensureTitleValues(item) {
        if (!item.titleValues) {
            item.titleValues = {};
        }
    }

    function selectedCount() {
        return Object.keys(vm.selectedGroupIds).filter(function (key) { return vm.selectedGroupIds[key]; }).length
            + Object.keys(vm.selectedVariantIds).filter(function (key) { return vm.selectedVariantIds[key]; }).length;
    }

    function addDeletedNodeId(id) {
        if (vm.deletedNodeIds.indexOf(id) === -1) {
            vm.deletedNodeIds.push(id);
        }
    }

    function bindDragAndDrop() {
        var draggedGroupId = null;
        var draggedVariant = null;
        var draggedImageIndex = null;

        $element[0].addEventListener('dragstart', function (event) {
            var mediaCard = event.target.closest('[data-media-image-index]');
            if (mediaCard) {
                draggedImageIndex = Number(mediaCard.getAttribute('data-media-image-index'));
                event.stopPropagation();
                return;
            }

            var variantRow = event.target.closest('[data-drag-variant-id]');
            if (variantRow) {
                draggedVariant = {
                    groupId: Number(variantRow.getAttribute('data-drag-group-id')),
                    variantId: Number(variantRow.getAttribute('data-drag-variant-id'))
                };
                event.stopPropagation();
                return;
            }

            var groupCard = event.target.closest('.ekm-variants-group[data-drag-group-id]');
            if (groupCard) {
                draggedGroupId = Number(groupCard.getAttribute('data-drag-group-id'));
            }
        });

        $element[0].addEventListener('dragover', function (event) {
            if (draggedImageIndex !== null && event.target.closest('[data-media-image-index]')) {
                event.preventDefault();
                event.stopPropagation();
                return;
            }

            var variantRow = event.target.closest('[data-drag-variant-id]');
            if (variantRow && draggedVariant && draggedVariant.groupId === Number(variantRow.getAttribute('data-drag-group-id'))) {
                event.preventDefault();
                event.stopPropagation();
                return;
            }

            if (draggedGroupId !== null && event.target.closest('.ekm-variants-group[data-drag-group-id]')) {
                event.preventDefault();
            }
        });

        $element[0].addEventListener('drop', function (event) {
            var mediaCard = event.target.closest('[data-media-image-index]');
            if (mediaCard && draggedImageIndex !== null) {
                event.preventDefault();
                event.stopPropagation();
                $scope.$apply(function () {
                    reorderDrawerImage(draggedImageIndex, Number(mediaCard.getAttribute('data-media-image-index')));
                });
                draggedImageIndex = null;
                return;
            }

            var variantRow = event.target.closest('[data-drag-variant-id]');
            if (variantRow && draggedVariant) {
                event.preventDefault();
                event.stopPropagation();
                $scope.$apply(function () {
                    reorderVariant(draggedVariant, Number(variantRow.getAttribute('data-drag-group-id')), Number(variantRow.getAttribute('data-drag-variant-id')));
                });
                draggedVariant = null;
                return;
            }

            var groupCard = event.target.closest('.ekm-variants-group[data-drag-group-id]');
            if (groupCard && draggedGroupId !== null) {
                event.preventDefault();
                $scope.$apply(function () {
                    reorderGroup(draggedGroupId, Number(groupCard.getAttribute('data-drag-group-id')));
                });
                draggedGroupId = null;
            }
        });

        $element[0].addEventListener('dragend', function () {
            draggedGroupId = null;
            draggedVariant = null;
            draggedImageIndex = null;
        });
    }

    function reorderDrawerImage(sourceIndex, targetIndex) {
        if (!vm.drawer || sourceIndex === targetIndex) {
            return;
        }

        var images = splitImages(vm.drawer.item.images);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex >= images.length || targetIndex >= images.length) {
            return;
        }

        var image = images.splice(sourceIndex, 1)[0];
        images.splice(targetIndex, 0, image);
        vm.drawer.item.images = images.join(',');
    }

    function reorderGroup(sourceId, targetId) {
        if (sourceId === targetId) {
            return;
        }

        vm.product.groups = reorderById(vm.product.groups, sourceId, targetId);
        vm.product.groups.forEach(function (group, index) {
            group.sortOrder = index;
        });
    }

    function reorderVariant(source, targetGroupId, targetVariantId) {
        if (!source || source.groupId !== targetGroupId || source.variantId === targetVariantId) {
            return;
        }

        var group = (vm.product.groups || []).filter(function (item) { return item.id === source.groupId; })[0];
        if (!group) {
            return;
        }

        group.variants = reorderById(group.variants, source.variantId, targetVariantId);
        group.variants.forEach(function (variant, index) {
            variant.sortOrder = index;
        });
    }

    function reorderById(items, sourceId, targetId) {
        var next = items.slice();
        var sourceIndex = next.findIndex(function (item) { return item.id === sourceId; });
        var targetIndex = next.findIndex(function (item) { return item.id === targetId; });

        if (sourceIndex < 0 || targetIndex < 0) {
            return items;
        }

        var item = next.splice(sourceIndex, 1)[0];
        next.splice(targetIndex, 0, item);
        return next;
    }

    function setBadge() {
        var count = vm.product ? vm.product.variantCount : 0;
        $scope.model.badge = count > 0 ? { count: count, type: 'default' } : null;
    }

    function firstValue(values) {
        for (var key in values) {
            if (values[key]) {
                return values[key];
            }
        }

        return '';
    }

    function getLanguageLabel(language) {
        return String(language.isoCode || language.cultureName || '').split('-')[0].toUpperCase();
    }

    function getVariantPrice(variant, storeAlias, currency) {
        var prices = (variant.priceValues || {})[storeAlias] || [];
        var price = prices.filter(function (item) { return getCurrency(item) === currency; })[0];
        return price ? getPrice(price) : 0;
    }

    function getCurrency(price) {
        return price.Currency || price.currency || '';
    }

    function getPrice(price) {
        return price.Price || price.price || 0;
    }

    function formatNumber(value) {
        return new Intl.NumberFormat().format(value || 0);
    }

    function splitImages(value) {
        var rawValue = String(value || '').trim();

        if (!rawValue) {
            return [];
        }

        if (rawValue.indexOf('[') === 0) {
            try {
                return JSON.parse(rawValue).map(function (item) {
                    return String(item.mediaKey || item.key || '').trim();
                }).filter(Boolean);
            } catch (error) {
                return [];
            }
        }

        return rawValue.split(',').map(function (item) { return normalizeMediaIdentifier(item.trim()); }).filter(Boolean);
    }

    function normalizeMediaIdentifier(value) {
        var match = value.match(/umb:\/\/media\/(.+)$/i);
        return match ? match[1] : value;
    }

    function getFirstImageThumbUrl(value) {
        return getThumbUrl(getFirstImage(value));
    }

    function getFirstImage(value) {
        return splitImages(value)[0] || '';
    }

    function getThumbUrl(image) {
        if (!image) {
            return '';
        }

        return '/ekom/backoffice/Variants/Media/Thumbnail?mediaId=' + encodeURIComponent(image) + '&width=38&height=38';
    }

    function normalizeMediaSelection(selection) {
        return (selection || []).map(function (item) {
            if (typeof item === 'string') {
                return item;
            }

            return item.udi || item.key || item.id || '';
        }).filter(Boolean);
    }

    function isDraft(id) {
        return id <= 0;
    }

    function snapshotGroup(group) {
        return angular.toJson({ titleValues: group.titleValues, images: group.images, customFields: group.customFields, sortOrder: group.sortOrder });
    }

    function snapshotVariant(variant) {
        return angular.toJson({ titleValues: variant.titleValues, sku: variant.sku, images: variant.images, priceValues: variant.priceValues, stockValues: variant.stockValues, customFields: variant.customFields, sortOrder: variant.sortOrder });
    }

    function getErrorMessage(error, fallback) {
        if (error && error.data) {
            return typeof error.data === 'string' ? error.data : error.data.message || fallback;
        }

        return fallback;
    }

    vm.load();
});
