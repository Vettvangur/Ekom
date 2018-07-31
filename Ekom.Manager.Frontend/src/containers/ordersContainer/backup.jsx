import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import _ from "lodash";
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';
import statusList from '../../utilities/statusList';
import { orderService } from '../../services';

import SearchForm from 'containers/ordersContainer/components/searchForm';


const path = '/umbraco/backoffice/ekom';
export default class OrdersContainer extends Component {

  constructor(props) {
    super(props);

    this.state = {
      start: Date(),
      end: Date(),
      stores: [],
      discounts: [],
      paymentProviders: [],
      shippingProviders: [],
      loading: true,
      defaultData: [],
      orders: [],
      page: 0,
      pageSize: 10,
      expanded: {},
      resized: [],
      filtered: []
    }
    this.defaultFilter = this.defaultFilter.bind(this);
    this.getOrderStatus = this.getOrderStatus.bind(this);
    this.updateStatus = this.updateStatus.bind(this);
    this.handleInputChange = this.handleInputChange.bind(this);
    this.handleSubmit = this.handleSubmit.bind(this);
  }  

  componentDidMount() {
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 30).toISOString().split('T')[0];
    const end = now.toISOString().split('T')[0];
    this.setState({
      start: start,
      end: end
    });

    this.getDiscounts().then((res) => {
      this.setState({discounts: res})
    })

    this.getShippingProviders().then((res) => {
      this.setState({shippingProviders: res})
    })
    this.getPaymentProviders().then((res) => {
      this.setState({paymentProviders: res})
    })
    this.getStores().then((res) => {
      this.setState({stores: res})
    })
    
    this.getAllOrders(start, end).then((orders) => {
      this.setState({
        start: start,
        end: end,
        defaultData: orders,
        orders: orders,
        loading: false,
      });

    });
  }
  handleInputChange(event) {
    const target = event.target;
    const value = target.type === 'checkbox' ? target.checked : target.value;
    const name = target.name;


    this.setState({
      [name]: value
    });
  }
  handleSubmit(event) {
    event.preventDefault();
    console.log("tt")
    const { start, end } = this.state
    console.log(start)
    console.log(end)
    this.setState({loading: true});
    this.getAllOrders(start, end).then((orders) => {
      console.log(orders)
      this.setState({
        start: start,
        end: end,
        defaultData: orders,
        orders: orders,
        loading: false,
      });

    });
  }



  getStores() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getstores', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      return json
    })
  }

  getPaymentProviders() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getpaymentproviders', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      return json
    })
  }
  getShippingProviders() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getshippingproviders', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      return json
    })
  }

  getDiscounts() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getdiscounts', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      return json
    })
  }

  getAllOrders(start, end) {
    console.log(`/umbraco/backoffice/ekom/managerapi/getallorders?start=${start}&end=${end}`)
    return fetch(`/umbraco/backoffice/ekom/managerapi/getallorders?start=${start}&end=${end}`, {
      credentials: 'include',
    }).then(function (response) {
      return response.json();
    }).then(function (result) {
      return result;
    });
  }

  getIncompleteOrders(start, end) {

  }
  getAbandonedBaskets(start, end) {

  }
  getOrdersWaitingForPayment(start, end) {

  }

  getOrders() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getorders', {
      credentials: 'include',
    }).then(function (response) {
      return response.json();
    }).then(function (result) {
      return result;
    });
  }

  getOrderStatus(OrderStatus) {
    if (OrderStatus === 0) {
      return "Cancelled"
    } 
    if (OrderStatus === 1) {
      return "Closed"
    }
    if (OrderStatus === 2) {
      return "Payment failed"
    }
    if (OrderStatus === 3) {
      return "Incomplete"
    }
    if (OrderStatus === 4) {
      return "Offline payment"
    }
    if (OrderStatus === 5) {
      return "Pending"
    }
    if (OrderStatus === 6) {
      return "Ready for dispatch"
    }
    if (OrderStatus === 7) {
      return "Ready for dispatch when in stock"
    }
    if (OrderStatus === 8) {
      return "Dispatched"
    }
    if (OrderStatus === 9) {
      return "Waiting for payment"
    }
    if (OrderStatus === 10) {
      return "Waiting for payment provider"
    }
    if (OrderStatus === 11) {
      return "Returned"
    }
    if (OrderStatus === 12) {
      return "Wishlist"
    }
  }

  defaultFilter(filter, row) {
    return String(row[filter.id]).includes(filter.value)
  }

  updateStatus(event, UniqueId) {
    const orderId = UniqueId;
    const orderStatus = event.target.value;
    orderService().updateStatus(orderId, orderStatus).then((res) => {
      console.log(res);
    })
    .catch(err => {
      console.log("error: " + err)
    })
  }
  render() {

    const {
      start,
      end,
      shippingProviders,
      paymentProviders,
      stores,
      discounts,
      loading,
      defaultData,
      orders,
      pages,
      filtered
    } = this.state;

    var columns = [
          {
            Header: 'Order Number',
            id: 'orderNumber',
            accessor: d => {
              return {
                UniqueId: d.UniqueId,
                OrderNumber: d.OrderNumber,
              }
            },
            filterMethod: (filter, row) => {
              console.log(filter)
              console.log(row)
              if (String(row.orderNumber.OrderNumber.toLowerCase()).includes(filter.value.toLowerCase())) {
                return row
              }
              if (String(row.orderNumber.UniqueId.toLowerCase()).includes(filter.value.toLowerCase())) {
                return row
              }
            },
            Cell: row => (
              <Link to={`${path}/manager/order/${row.value.UniqueId}`}>{row.value.OrderNumber}</Link>
            )
          },
          {
            Header: 'Status',
            id: 'status',
            accessor: d => {
              return {
                UniqueId: d.UniqueId,
                OrderStatus: d.OrderStatus
              }
            },
            filterMethod: (filter, row) => {
              if (filter.value === "all") {
                return true;
              }
              if (filter.value == "0") {
                if (row.status.OrderStatus == 0) {
                  return row
                }
              }
              if (filter.value == "1") {
                if (row.status.OrderStatus == 1) {
                  return row
                }
              }
              if (filter.value == "2") {
                if (row.status.OrderStatus == 2) {
                  return row
                }
              }
              if (filter.value == "3") {
                if (row.status.OrderStatus == 3) {
                  return row
                }
              }
              if (filter.value == "4") {
                if (row.status.OrderStatus == 4) {
                  return row
                }
              }
              if (filter.value == "5") {
                if (row.status.OrderStatus == 5) {
                  return row
                }
              }
              if (filter.value == "6") {
                if (row.status.OrderStatus == 6) {
                  return row
                }
              }
              if (filter.value == "7") {
                if (row.status.OrderStatus == 7) {
                  return row
                }
              }
              if (filter.value == "8") {
                if (row.status.OrderStatus == 8) {
                  return row
                }
              }
              if (filter.value == "9") {
                if (row.status.OrderStatus == 9) {
                  return row
                }
              }
              if (filter.value == "10") {
                if (row.status.OrderStatus == 10) {
                  return row
                }
              }
              if (filter.value == "11") {
                if (row.status.OrderStatus == 11) {
                  return row
                }
              }
              if (filter.value == "12") {
                if (row.status.OrderStatus == 12) {
                  return row
                }
              }
            },
            Filter: ({ filter, onChange }) =>
              <React.Fragment>
              <div className="rt-resizable-header-content"></div>
              <div className="rt-th-filter">
              <select
                onChange={event => onChange(event.target.value)}
                style={{ width: "100%" }}
                value={filter ? filter.value : "all"}
              >
                <option value="all">Show All</option>
                {statusList.map(status => {
                    return <option key={status.id} value={status.id}>{status.value}</option>
                })}
              </select>
              </div>
              </React.Fragment>,
            Cell: row => (
              <div className="select__wrapper">
              <select 
                onChange={event => this.updateStatus(event, row.value.UniqueId)}
              >
                {statusList.map(status => {
                  if (row.value.OrderStatus === status.id) {
                    return <option key={status.id} selected value={status.id}>{status.value}</option>
                  } else {
                     return <option key={status.id} value={status.id}>{status.value}</option>
                  }
                })}
              </select>
              </div>
            )
          },
          {
            Header: 'Email',
            accessor: 'CustomerEmail',
          },
          {
            Header: 'Name',
            accessor: 'CustomerName',
          },
          {
            Header: 'Country',
            accessor: 'CustomerCountry',
            id: 'country',
            filterMethod: (filter, row) => {
              if (filter.value === "all") {
                return true;
              }
              if (filter.value === "Iceland") {
                return row[filter.CustomerCountry] == "IS";
              }
            },
            Filter: ({ filter, onChange }) =>
            <div className="select__wrapper">
              <select
                onChange={event => onChange(event.target.value)}
                style={{ width: "100%" }}
                value={filter ? filter.value : "all"}
              >
                <option value="all">Show All countries</option>
                <option value="0">Albania</option>
                <option value="1">Andorra</option>
                <option value="2">Armenia</option>
                <option value="3">Algeria</option>
                <option value="4">Argentina</option>
                <option value="5">Australia</option>
                <option value="6">Austria</option>
                <option value="7">Azerbaijan</option>
                <option value="8">Belarus</option>
                <option value="9">Belgium</option>
                <option value="10">Bosnia And Herzegovina</option>
                <option value="11">Brazil</option>
                <option value="12">Bulgaria</option>
                <option value="13">Canada</option>
                <option value="14">Chile</option>
                <option value="15">China</option>
                <option value="16">Croatia (Local Name: Hrvatska)</option>
                <option value="17">Cuba</option>
                <option value="18">Cyprus</option>
                <option value="19">Czech Republic</option>
                <option value="20">Denmark</option>
                <option value="21">Egypt</option>
                <option value="22">Estonia</option>
                <option value="23">Finland</option>
                <option value="24">France</option>
                <option value="25">Georgia</option>
                <option value="26">Germany</option>
                <option value="27">Greece</option>
                <option value="28">Greenland</option>
                <option value="29">Haiti</option>
                <option value="30">Hong Kong</option>
                <option value="31">Hungary</option>
                <option value="32">Iceland</option>
                <option value="33">India</option>
                <option value="34">Indonesia</option>
                <option value="35">Iran, Islamic Republic Of</option>
                <option value="36">Iraq</option>
                <option value="37">Ireland</option>
                <option value="38">Israel</option>
                <option value="39">Italy</option>
                <option value="40">Jamaica</option>
                <option value="41">Japan</option>
                <option value="42">Jordan</option>
                <option value="43">Latvia</option>
                <option value="44">Liechtenstein</option>
                <option value="45">Lithuania</option>
                <option value="46">Luxembourg</option>
                <option value="47">Macedonia, The Former Yugoslav Republic Of</option>
                <option value="48">Malaysia</option>
                <option value="49">Malta</option>
                <option value="50">Mexico</option>
                <option value="51">Moldova, Republic Of</option>
                <option value="52">Monaco</option>
                <option value="53">Montenegro</option>
                <option value="54">Morocco</option>
                <option value="55">Namibia</option>
                <option value="56">Nepal</option>
                <option value="57">Netherlands</option>
                <option value="58">Netherlands Antilles</option>
                <option value="59">New Zealand</option>
                <option value="60">Norway</option>
                <option value="61">Paraguay</option>
                <option value="62">Peru</option>
                <option value="63">Philippines</option>
                <option value="64">Poland</option>
                <option value="65">Portugal</option>
                <option value="66">Qatar</option>
                <option value="67">Romania</option>
                <option value="68">Russian Federation</option>
                <option value="69">San Marino</option>
                <option value="70">Saudi Arabia</option>
                <option value="71">Singapore</option>
                <option value="72">Slovakia (Slovak Republic)</option>
                <option value="73">Slovenia</option>
                <option value="74">South Africa</option>
                <option value="75">Spain</option>
                <option value="76">Sudan</option>
                <option value="77">Suriname</option>
                <option value="78">Sweden</option>
                <option value="79">Switzerland</option>
                <option value="80">Taiwan, Province Of China</option>
                <option value="81">Thailand</option>
                <option value="82">Tunisia</option>
                <option value="83">Turkey</option>
                <option value="84">Ukraine</option>
                <option value="85">United Arab Emirates</option>
                <option value="86">United Kingdom</option>
                <option value="87">United States</option>
                <option value="88">Venezuela</option>
                <option value="89">Viet Nam</option>
              </select>
            </div>
          },
          {
            Header: 'Created',
            accessor: 'CreateDate',
          },
          {
            Header: 'Paid',
            accessor: 'PaidDate',
          },
          {
            Header: 'Total',
            accessor: 'TotalAmount',
          },
    ];

    return (
      <div className="content">
        <SearchForm handleSubmit={this.handleSubmit} handleInputChange={this.handleInputChange} />

        <ReactTable
            data={orders}
            
            defaultFilterMethod={this.defaultFilter}
            columns={columns}
            defaultFilterMethod={(filter, row) =>
              String(row[filter.id]) === filter.value}
            defaultPageSize={10}
            loading={loading}
            showPagination={false}
            className="-highlight bg-white CustomReactTable"         
            getTheadProps={(state, rowInfo, column) => {
              return {
                style: {
                  boxShadow: "0 2px 0px 0 rgba(0,0,0,0.05)",
                }
              }
            }}
            getTheadTrProps={(state, rowInfo, column) => {
              return {
                style: {
                  textAlign: "left",
                  fontSize: "14px",
                  fontWeight: 600,
                  color: "rgba(44, 56, 44, 0.7)",
                }
              }
            }}
            getTheadThProps={(state, rowInfo, column, instance) => {
              return {
                style: {
                  borderRight: "none"
                },
                onMouseOver: (e) => {
                  console.log("A Td Element was clicked!");
                  console.log("it produced this event:", e);
                  console.log("It was in this column:", column);
                  console.log("It was in this row:", rowInfo);
                  console.log("It was in this table instance:", instance);
                }
            }
            }}
            getTdProps={(state, rowInfo, column) => {
              return {
                style: {
                  borderRight: "none"
                }
              }
            }}
          >
          
          {(state, makeTable, instance) => {
            console.log(state)
            console.log(instance)
            return (
              <React.Fragment>
              <div
               className="ReactTable -highlight bg-white CustomReactTable"
              >

                <pre>
                  <code>
                    state.allVisibleColumns ==={" "}
                    {JSON.stringify(state.allVisibleColumns, null, 4)}
                  </code>
                </pre>
                <div className="rt-table">
                  <div className="rt-thead -header">
                    <div 
                      className="rt-tr"
                      style={{
                        textAlign: "left",
                        fontSize: "14px",
                        fontWeight: 600,
                        color: "rgba(44, 56, 44, 0.7)",
                      }}
                    >
                      {state.allVisibleColumns.map(column => {
                        return (
                          <div 
                            className="rt-th"
                            style={{
                              borderRight: "none"
                            }}>
                            <div className="rt-resizable-header-content">{column.Header}</div>
                            <div></div>
                          </div>
                        )
                      })}
                    </div>
                  </div>
                </div>
              </div>
              {makeTable()}
              </React.Fragment>
            );
          }}
          </ReactTable>
      </div>
    );
  }
}
