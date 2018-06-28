webpackHotUpdate("site",{

/***/ "./src/components/orders/orders.js":
/*!*****************************************!*\
  !*** ./src/components/orders/orders.js ***!
  \*****************************************/
/*! exports provided: default */
/***/ (function(module, __webpack_exports__, __webpack_require__) {

"use strict";
eval("__webpack_require__.r(__webpack_exports__);\n/* harmony export (binding) */ __webpack_require__.d(__webpack_exports__, \"default\", function() { return Orders; });\n/* harmony import */ var react__WEBPACK_IMPORTED_MODULE_0__ = __webpack_require__(/*! react */ \"./node_modules/react/index.js\");\n/* harmony import */ var react__WEBPACK_IMPORTED_MODULE_0___default = /*#__PURE__*/__webpack_require__.n(react__WEBPACK_IMPORTED_MODULE_0__);\n/* harmony import */ var lodash__WEBPACK_IMPORTED_MODULE_1__ = __webpack_require__(/*! lodash */ \"./node_modules/lodash/lodash.js\");\n/* harmony import */ var lodash__WEBPACK_IMPORTED_MODULE_1___default = /*#__PURE__*/__webpack_require__.n(lodash__WEBPACK_IMPORTED_MODULE_1__);\n/* harmony import */ var stores_orderStore__WEBPACK_IMPORTED_MODULE_2__ = __webpack_require__(/*! stores/orderStore */ \"./src/stores/orderStore.js\");\n/* harmony import */ var react_table__WEBPACK_IMPORTED_MODULE_3__ = __webpack_require__(/*! react-table */ \"./node_modules/react-table/es/index.js\");\nfunction _typeof(obj) { if (typeof Symbol === \"function\" && typeof Symbol.iterator === \"symbol\") { _typeof = function _typeof(obj) { return typeof obj; }; } else { _typeof = function _typeof(obj) { return obj && typeof Symbol === \"function\" && obj.constructor === Symbol && obj !== Symbol.prototype ? \"symbol\" : typeof obj; }; } return _typeof(obj); }\n\nfunction _classCallCheck(instance, Constructor) { if (!(instance instanceof Constructor)) { throw new TypeError(\"Cannot call a class as a function\"); } }\n\nfunction _inherits(subClass, superClass) { if (typeof superClass !== \"function\" && superClass !== null) { throw new TypeError(\"Super expression must either be null or a function\"); } _setPrototypeOf(subClass.prototype, superClass && superClass.prototype); if (superClass) _setPrototypeOf(subClass, superClass); }\n\nfunction _setPrototypeOf(o, p) { _setPrototypeOf = Object.setPrototypeOf || function _setPrototypeOf(o, p) { o.__proto__ = p; return o; }; return _setPrototypeOf(o, p); }\n\nfunction _defineProperties(target, props) { for (var i = 0; i < props.length; i++) { var descriptor = props[i]; descriptor.enumerable = descriptor.enumerable || false; descriptor.configurable = true; if (\"value\" in descriptor) descriptor.writable = true; Object.defineProperty(target, descriptor.key, descriptor); } }\n\nfunction _createClass(Constructor, protoProps, staticProps) { if (protoProps) _defineProperties(Constructor.prototype, protoProps); if (staticProps) _defineProperties(Constructor, staticProps); return Constructor; }\n\nfunction _possibleConstructorReturn(self, call) { if (call && (_typeof(call) === \"object\" || typeof call === \"function\")) { return call; } return _assertThisInitialized(self); }\n\nfunction _getPrototypeOf(o) { _getPrototypeOf = Object.getPrototypeOf || function _getPrototypeOf(o) { return o.__proto__; }; return _getPrototypeOf(o); }\n\nfunction _assertThisInitialized(self) { if (self === void 0) { throw new ReferenceError(\"this hasn't been initialised - super() hasn't been called\"); } return self; }\n\n\n\n\n\n\nvar Orders =\n/*#__PURE__*/\nfunction (_Component) {\n  function Orders(props) {\n    var _this;\n\n    _classCallCheck(this, Orders);\n\n    _this = _possibleConstructorReturn(this, _getPrototypeOf(Orders).call(this, props));\n    _this.state = {\n      defaultData: [],\n      orders: [],\n      pages: null,\n      loading: true,\n      filtered: []\n    };\n    _this.defaultFilter = _this.defaultFilter.bind(_assertThisInitialized(_assertThisInitialized(_this)));\n    return _this;\n  }\n\n  _createClass(Orders, [{\n    key: \"componentDidMount\",\n    value: function componentDidMount() {\n      var _this2 = this;\n\n      this.getOrders().then(function (orders) {\n        _this2.setState({\n          defaultData: orders,\n          orders: orders,\n          loading: false\n        });\n      });\n    }\n  }, {\n    key: \"getOrders\",\n    value: function getOrders() {\n      return fetch('/umbraco/backoffice/ekom/managerapi/getorders', {\n        credentials: 'include'\n      }).then(function (response) {\n        return response.json();\n      }).then(function (result) {\n        return result;\n      });\n    }\n  }, {\n    key: \"defaultFilter\",\n    value: function defaultFilter(filter, row) {\n      console.log(filter);\n      console.log(row);\n      return String(row[filter.id]).includes(filter.value);\n    }\n  }, {\n    key: \"render\",\n    value: function render() {\n      var _this$state = this.state,\n          loading = _this$state.loading,\n          defaultData = _this$state.defaultData,\n          orders = _this$state.orders,\n          pages = _this$state.pages,\n          filtered = _this$state.filtered;\n      var columns = [{\n        Header: 'Orders',\n        columns: [{\n          Header: 'Order Number',\n          accessor: 'OrderNumber'\n        }, {\n          Header: 'Status',\n          accessor: 'OrderStatus'\n        }, {\n          Header: 'Email',\n          accessor: 'CustomerEmail'\n        }, {\n          Header: 'Name',\n          accessor: 'CustomerName'\n        }, {\n          Header: 'Country',\n          accessor: 'CustomerCountry'\n        }, {\n          Header: 'Created',\n          accessor: 'CreateDate'\n        }, {\n          Header: 'Paid',\n          accessor: 'PaidDate'\n        }, {\n          Header: 'Total',\n          accessor: 'TotalAmount'\n        }]\n      }];\n      return react__WEBPACK_IMPORTED_MODULE_0___default.a.createElement(\"main\", null, react__WEBPACK_IMPORTED_MODULE_0___default.a.createElement(react_table__WEBPACK_IMPORTED_MODULE_3__[\"default\"], {\n        data: orders,\n        filterable: true,\n        defaultFilterMethod: this.defaultFilter,\n        columns: columns,\n        defaultPageSize: 2,\n        loading: loading,\n        className: \"-striped -highlight\"\n      }));\n    }\n  }]);\n\n  _inherits(Orders, _Component);\n\n  return Orders;\n}(react__WEBPACK_IMPORTED_MODULE_0__[\"Component\"]);\n\n//# sourceURL=[module]\n//# sourceMappingURL=data:application/json;charset=utf-8;base64,eyJ2ZXJzaW9uIjozLCJzb3VyY2VzIjpbIndlYnBhY2s6Ly8vLi9zcmMvY29tcG9uZW50cy9vcmRlcnMvb3JkZXJzLmpzPzA5OGYiXSwibmFtZXMiOlsiT3JkZXJzIiwicHJvcHMiLCJzdGF0ZSIsImRlZmF1bHREYXRhIiwib3JkZXJzIiwicGFnZXMiLCJsb2FkaW5nIiwiZmlsdGVyZWQiLCJkZWZhdWx0RmlsdGVyIiwiYmluZCIsImdldE9yZGVycyIsInRoZW4iLCJzZXRTdGF0ZSIsImZldGNoIiwiY3JlZGVudGlhbHMiLCJyZXNwb25zZSIsImpzb24iLCJyZXN1bHQiLCJmaWx0ZXIiLCJyb3ciLCJjb25zb2xlIiwibG9nIiwiU3RyaW5nIiwiaWQiLCJpbmNsdWRlcyIsInZhbHVlIiwiY29sdW1ucyIsIkhlYWRlciIsImFjY2Vzc29yIl0sIm1hcHBpbmdzIjoiOzs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7Ozs7OztBQUFBO0FBQ0E7QUFDQTtBQUNBOztJQUVxQkEsTTs7O0FBRW5CLGtCQUFZQyxLQUFaLEVBQW1CO0FBQUE7O0FBQUE7O0FBQ2pCLGdGQUFNQSxLQUFOO0FBRUEsVUFBS0MsS0FBTCxHQUFhO0FBQ1hDLG1CQUFhLEVBREY7QUFFWEMsY0FBUSxFQUZHO0FBR1hDLGFBQU8sSUFISTtBQUlYQyxlQUFTLElBSkU7QUFLWEMsZ0JBQVU7QUFMQyxLQUFiO0FBT0EsVUFBS0MsYUFBTCxHQUFxQixNQUFLQSxhQUFMLENBQW1CQyxJQUFuQix1REFBckI7QUFWaUI7QUFXbEI7Ozs7d0NBRW1CO0FBQUE7O0FBRWxCLFdBQUtDLFNBQUwsR0FBaUJDLElBQWpCLENBQXNCLFVBQUNQLE1BQUQsRUFBWTtBQUdoQyxlQUFLUSxRQUFMLENBQWM7QUFDWlQsdUJBQWFDLE1BREQ7QUFFWkEsa0JBQVFBLE1BRkk7QUFHWkUsbUJBQVM7QUFIRyxTQUFkO0FBTUQsT0FURDtBQVVEOzs7Z0NBRVc7QUFDVixhQUFPTyxNQUFNLCtDQUFOLEVBQXVEO0FBQzVEQyxxQkFBYTtBQUQrQyxPQUF2RCxFQUVKSCxJQUZJLENBRUMsVUFBVUksUUFBVixFQUFvQjtBQUMxQixlQUFPQSxTQUFTQyxJQUFULEVBQVA7QUFDRCxPQUpNLEVBSUpMLElBSkksQ0FJQyxVQUFVTSxNQUFWLEVBQWtCO0FBQ3hCLGVBQU9BLE1BQVA7QUFDRCxPQU5NLENBQVA7QUFPRDs7O2tDQUdhQyxNLEVBQVFDLEcsRUFBSztBQUN6QkMsY0FBUUMsR0FBUixDQUFZSCxNQUFaO0FBQ0FFLGNBQVFDLEdBQVIsQ0FBWUYsR0FBWjtBQUNBLGFBQU9HLE9BQU9ILElBQUlELE9BQU9LLEVBQVgsQ0FBUCxFQUF1QkMsUUFBdkIsQ0FBZ0NOLE9BQU9PLEtBQXZDLENBQVA7QUFDRDs7OzZCQUNRO0FBQUEsd0JBUUgsS0FBS3ZCLEtBUkY7QUFBQSxVQUdMSSxPQUhLLGVBR0xBLE9BSEs7QUFBQSxVQUlMSCxXQUpLLGVBSUxBLFdBSks7QUFBQSxVQUtMQyxNQUxLLGVBS0xBLE1BTEs7QUFBQSxVQU1MQyxLQU5LLGVBTUxBLEtBTks7QUFBQSxVQU9MRSxRQVBLLGVBT0xBLFFBUEs7QUFVUCxVQUFJbUIsVUFBVSxDQUNaO0FBQ0VDLGdCQUFRLFFBRFY7QUFFRUQsaUJBQVMsQ0FDUDtBQUNFQyxrQkFBUSxjQURWO0FBRUVDLG9CQUFVO0FBRlosU0FETyxFQUtQO0FBQ0VELGtCQUFRLFFBRFY7QUFFRUMsb0JBQVU7QUFGWixTQUxPLEVBU1A7QUFDRUQsa0JBQVEsT0FEVjtBQUVFQyxvQkFBVTtBQUZaLFNBVE8sRUFhUDtBQUNFRCxrQkFBUSxNQURWO0FBRUVDLG9CQUFVO0FBRlosU0FiTyxFQWlCUDtBQUNFRCxrQkFBUSxTQURWO0FBRUVDLG9CQUFVO0FBRlosU0FqQk8sRUFxQlA7QUFDRUQsa0JBQVEsU0FEVjtBQUVFQyxvQkFBVTtBQUZaLFNBckJPLEVBeUJQO0FBQ0VELGtCQUFRLE1BRFY7QUFFRUMsb0JBQVU7QUFGWixTQXpCTyxFQTZCUDtBQUNFRCxrQkFBUSxPQURWO0FBRUVDLG9CQUFVO0FBRlosU0E3Qk87QUFGWCxPQURZLENBQWQ7QUF3Q0EsYUFDRSx5RUFDRSwyREFBQyxtREFBRDtBQUNJLGNBQU14QixNQURWO0FBRUksd0JBRko7QUFHSSw2QkFBcUIsS0FBS0ksYUFIOUI7QUFJSSxpQkFBU2tCLE9BSmI7QUFLSSx5QkFBaUIsQ0FMckI7QUFNSSxpQkFBU3BCLE9BTmI7QUFPSSxtQkFBVTtBQVBkLFFBREYsQ0FERjtBQWFEOzs7Ozs7RUE1R2lDLCtDIiwiZmlsZSI6Ii4vc3JjL2NvbXBvbmVudHMvb3JkZXJzL29yZGVycy5qcy5qcyIsInNvdXJjZXNDb250ZW50IjpbImltcG9ydCBSZWFjdCwgeyBDb21wb25lbnQgfSBmcm9tICdyZWFjdCc7XHJcbmltcG9ydCBfIGZyb20gXCJsb2Rhc2hcIjtcclxuaW1wb3J0IG9yZGVyU3RvcmUgZnJvbSAnc3RvcmVzL29yZGVyU3RvcmUnO1xyXG5pbXBvcnQgUmVhY3RUYWJsZSBmcm9tICdyZWFjdC10YWJsZSc7XHJcblxyXG5leHBvcnQgZGVmYXVsdCBjbGFzcyBPcmRlcnMgZXh0ZW5kcyBDb21wb25lbnQge1xyXG5cclxuICBjb25zdHJ1Y3Rvcihwcm9wcykge1xyXG4gICAgc3VwZXIocHJvcHMpO1xyXG5cclxuICAgIHRoaXMuc3RhdGUgPSB7XHJcbiAgICAgIGRlZmF1bHREYXRhOiBbXSxcclxuICAgICAgb3JkZXJzOiBbXSxcclxuICAgICAgcGFnZXM6IG51bGwsXHJcbiAgICAgIGxvYWRpbmc6IHRydWUsXHJcbiAgICAgIGZpbHRlcmVkOiBbXSxcclxuICAgIH1cclxuICAgIHRoaXMuZGVmYXVsdEZpbHRlciA9IHRoaXMuZGVmYXVsdEZpbHRlci5iaW5kKHRoaXMpO1xyXG4gIH0gIFxyXG5cclxuICBjb21wb25lbnREaWRNb3VudCgpIHtcclxuXHJcbiAgICB0aGlzLmdldE9yZGVycygpLnRoZW4oKG9yZGVycykgPT4ge1xyXG5cclxuXHJcbiAgICAgIHRoaXMuc2V0U3RhdGUoe1xyXG4gICAgICAgIGRlZmF1bHREYXRhOiBvcmRlcnMsXHJcbiAgICAgICAgb3JkZXJzOiBvcmRlcnMsXHJcbiAgICAgICAgbG9hZGluZzogZmFsc2UsXHJcbiAgICAgIH0pO1xyXG5cclxuICAgIH0pO1xyXG4gIH1cclxuXHJcbiAgZ2V0T3JkZXJzKCkge1xyXG4gICAgcmV0dXJuIGZldGNoKCcvdW1icmFjby9iYWNrb2ZmaWNlL2Vrb20vbWFuYWdlcmFwaS9nZXRvcmRlcnMnLCB7XHJcbiAgICAgIGNyZWRlbnRpYWxzOiAnaW5jbHVkZScsXHJcbiAgICB9KS50aGVuKGZ1bmN0aW9uIChyZXNwb25zZSkge1xyXG4gICAgICByZXR1cm4gcmVzcG9uc2UuanNvbigpO1xyXG4gICAgfSkudGhlbihmdW5jdGlvbiAocmVzdWx0KSB7XHJcbiAgICAgIHJldHVybiByZXN1bHQ7XHJcbiAgICB9KTtcclxuICB9XHJcblxyXG5cclxuICBkZWZhdWx0RmlsdGVyKGZpbHRlciwgcm93KSB7XHJcbiAgICBjb25zb2xlLmxvZyhmaWx0ZXIpIFxyXG4gICAgY29uc29sZS5sb2cocm93KVxyXG4gICAgcmV0dXJuIFN0cmluZyhyb3dbZmlsdGVyLmlkXSkuaW5jbHVkZXMoZmlsdGVyLnZhbHVlKVxyXG4gIH1cclxuICByZW5kZXIoKSB7XHJcblxyXG4gICAgY29uc3Qge1xyXG4gICAgICBsb2FkaW5nLFxyXG4gICAgICBkZWZhdWx0RGF0YSxcclxuICAgICAgb3JkZXJzLFxyXG4gICAgICBwYWdlcyxcclxuICAgICAgZmlsdGVyZWRcclxuICAgIH0gPSB0aGlzLnN0YXRlO1xyXG5cclxuICAgIHZhciBjb2x1bW5zID0gW1xyXG4gICAgICB7XHJcbiAgICAgICAgSGVhZGVyOiAnT3JkZXJzJyxcclxuICAgICAgICBjb2x1bW5zOiBbXHJcbiAgICAgICAgICB7XHJcbiAgICAgICAgICAgIEhlYWRlcjogJ09yZGVyIE51bWJlcicsXHJcbiAgICAgICAgICAgIGFjY2Vzc29yOiAnT3JkZXJOdW1iZXInLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIHtcclxuICAgICAgICAgICAgSGVhZGVyOiAnU3RhdHVzJyxcclxuICAgICAgICAgICAgYWNjZXNzb3I6ICdPcmRlclN0YXR1cycsXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgICAge1xyXG4gICAgICAgICAgICBIZWFkZXI6ICdFbWFpbCcsXHJcbiAgICAgICAgICAgIGFjY2Vzc29yOiAnQ3VzdG9tZXJFbWFpbCcsXHJcbiAgICAgICAgICB9LFxyXG4gICAgICAgICAge1xyXG4gICAgICAgICAgICBIZWFkZXI6ICdOYW1lJyxcclxuICAgICAgICAgICAgYWNjZXNzb3I6ICdDdXN0b21lck5hbWUnLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIHtcclxuICAgICAgICAgICAgSGVhZGVyOiAnQ291bnRyeScsXHJcbiAgICAgICAgICAgIGFjY2Vzc29yOiAnQ3VzdG9tZXJDb3VudHJ5JyxcclxuICAgICAgICAgIH0sXHJcbiAgICAgICAgICB7XHJcbiAgICAgICAgICAgIEhlYWRlcjogJ0NyZWF0ZWQnLFxyXG4gICAgICAgICAgICBhY2Nlc3NvcjogJ0NyZWF0ZURhdGUnLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIHtcclxuICAgICAgICAgICAgSGVhZGVyOiAnUGFpZCcsXHJcbiAgICAgICAgICAgIGFjY2Vzc29yOiAnUGFpZERhdGUnLFxyXG4gICAgICAgICAgfSxcclxuICAgICAgICAgIHtcclxuICAgICAgICAgICAgSGVhZGVyOiAnVG90YWwnLFxyXG4gICAgICAgICAgICBhY2Nlc3NvcjogJ1RvdGFsQW1vdW50JyxcclxuICAgICAgICAgIH0sXHJcbiAgICAgICAgXSxcclxuICAgICAgfSxcclxuICAgIF07XHJcblxyXG4gICAgcmV0dXJuIChcclxuICAgICAgPG1haW4+XHJcbiAgICAgICAgPFJlYWN0VGFibGVcclxuICAgICAgICAgICAgZGF0YT17b3JkZXJzfVxyXG4gICAgICAgICAgICBmaWx0ZXJhYmxlXHJcbiAgICAgICAgICAgIGRlZmF1bHRGaWx0ZXJNZXRob2Q9e3RoaXMuZGVmYXVsdEZpbHRlcn1cclxuICAgICAgICAgICAgY29sdW1ucz17Y29sdW1uc31cclxuICAgICAgICAgICAgZGVmYXVsdFBhZ2VTaXplPXsyfVxyXG4gICAgICAgICAgICBsb2FkaW5nPXtsb2FkaW5nfVxyXG4gICAgICAgICAgICBjbGFzc05hbWU9XCItc3RyaXBlZCAtaGlnaGxpZ2h0XCJcclxuICAgICAgICAgIC8+XHJcbiAgICAgIDwvbWFpbj5cclxuICAgICk7XHJcbiAgfVxyXG59XHJcbiJdLCJzb3VyY2VSb290IjoiIn0=\n//# sourceURL=webpack-internal:///./src/components/orders/orders.js\n");

/***/ })

})