import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import _ from "lodash";
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';

import s from './orders.scss';
export default class Orders extends Component {

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
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - now.getDate()).toISOString().split('T')[0];
    console.log(start)
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
    
    this.getOrders(start, end).then((orders) => {

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
  handleInputChange(event) {
    const target = event.target;
    const value = target.type === 'checkbox' ? target.checked : target.value;
    const name = target.name;

    console.log(target)

    this.setState({
      [name]: value
    });
  }
  handleSubmit(event) {
    event.preventDefault();
    const { start, end } = this.props;
    this.setState({loading: true});
    this.getOrders(start, end).then((orders) => {

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

  updateStatus(event) {
    console.log(event)
  }
  render() {
    console.log(this.state)

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

    const statusList = [
      {
        id: 0,
        value: "Cancelled"
      },
      {
        id: 1,
        value: "Closed"
      },
      {
        id: 2,
        value: "Payment failed"
      },
      {
        id: 3,
        value: "Incomplete"
      },
      {
        id: 4,
        value: "Offline payment"
      },
      {
        id: 5,
        value: "Pending"
      },
      {
        id: 6,
        value: "Ready for dispatch"
      },
      {
        id: 7,
        value: "Ready for dispatch when in stock"
      },
      {
        id: 8,
        value: "Dispatched"
      },
      {
        id: 9,
        value: "Waiting for payment"
      },
      {
        id: 10,
        value: "Waiting for payment provider"
      },
      {
        id: 11,
        value: "Returned"
      },
      {
        id: 12,
        value: "Wishlist"
      },
    ]

    var columns = [
      {
        Header: 'Orders',
        columns: [
          {
            Header: 'Order Number',
            accessor: 'OrderNumber',
            Cell: row => (
              <Link to={`/manager/order/${row.value}`}>{row.value}</Link>
            )
          },
          {
            Header: 'Status',
            id: 'status',
            accessor: d => d.OrderStatus,
            filterMethod: (filter, row) => {
              if (filter.value === "all") {
                return true;
              }
            },
            Filter: ({ filter, onChange }) =>
              <select
                onChange={event => onChange(event.target.value)}
                style={{ width: "100%" }}
                value={filter ? filter.value : "all"}
              >
                <option value="all">Show All</option>
                {statusList.map(status => {
                    return <option key={status.id} value={status.id}>{status.value}</option>
                })}
              </select>,
            Cell: row => (
              <select 
                onChange={event => this.updateStatus(event)}
              >
                {statusList.map(status => {
                  if (row.value === status.id) {
                    return <option key={status.id} selected value={status.id}>{status.value}</option>
                  } else {
                     return <option key={status.id} value={status.id}>{status.value}</option>
                  }
                })}
              </select>
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
        ],
      },
    ];

    return (
      <main>
        <nav className={s.navigation}>

          <Link to="#" className={s.brand}>Orders</Link>
          <ul className={s.list}>
            <li className={s.link}>All orders</li>
            <li className={s.link}>Incomplete</li>
          </ul>
        </nav>

        <div className="page-content">

        <form onSubmit={this.handleSubmit}>
          <div className="input">
            <label htmlFor="startDate">Start date:</label>
            <input type="date" name="start" value={start} onChange={this.handleInputChange} />
          </div>
          <div className="input">
            <label htmlFor="endDate">Start date:</label>
            <input type="date" name="end" value={end} onChange={this.handleInputChange} />
          </div>
          <div className="input">
            <label htmlFor="store">Store:</label>
            <select name="store">
              <option value="all">All stores</option>
              {stores.map(store => {
                return <option key={store.id} value={store.Id}>{store.Title}</option>
              })}
            </select>
          </div>
          <div className="input">
            <label htmlFor="payment">Payment option:</label>
            <select name="payment">
              <option value="all">All payment options</option>
              {paymentProviders.map(provider => {
                return <option key={provider.id} value={provider.Id}>{provider.Title}</option>
              })}
            </select>
          </div>
          <div className="input">
            <label htmlFor="shipping">Shipping option:</label>
            <select name="shipping">
              <option value="all">All shipping options</option>
              {shippingProviders.map(provider => {
                return <option key={provider.id} value={provider.Id}>{provider.Properties.nodeName}</option>
              })}
            </select>
          </div>
          <div className="input">
            <label htmlFor="discounts">Discounts:</label>
            <select name="discounts">
              <option value="all">All discounts</option>
              {discounts.map(discount => {
                return <option key={discount.id} value={discount.Id}>{discount.Properties.nodeName}</option>
              })}
            </select>
          </div>
          <div className="input">
            <label htmlFor="search">Search:</label>
            <input type="text" id="search" name="search" />
          </div>
          
          <button type="submit">Search</button>
        </form>
          <ReactTable
              data={orders}
              filterable
              defaultFilterMethod={this.defaultFilter}
              columns={columns}
              defaultPageSize={10}
              loading={loading}
              className="-striped -highlight"
            />
        </div>
      </main>
    );
  }
}
