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
    this.handleInputChange = this.handleInputChange.bind(this);
  }  

  componentDidMount() {
    const now = new Date();
    const start = now.toISOString().split('T')[0]
    console.log(start)
    const end = new Date(now.getFullYear(), now.getMonth(), now.getDate() + now.getDate()).toISOString().split('T')[0];
    this.setState({
      start: start,
      end: end
    });
    
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


  defaultFilter(filter, row) {
    return String(row[filter.id]).includes(filter.value)
  }
  render() {

    const {
      start,
      end,
      loading,
      defaultData,
      orders,
      pages,
      filtered
    } = this.state;

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
            accessor: 'OrderStatus',
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

        <form>
          <div className="input">
            <label htmlFor="startDate">Start date:</label>
            <input type="date" name="start" value={start} onChange={this.handleInputChange} />
          </div>
          <div className="input">
            <label htmlFor="endDate">Start date:</label>
            <input type="date" name="end" value={end} onChange={this.handleInputChange} />
          </div>
        </form>
          <ReactTable
              data={orders}
              filterable
              defaultFilterMethod={this.defaultFilter}
              columns={columns}
              defaultPageSize={2}
              loading={loading}
              className="-striped -highlight"
            />
        </div>
      </main>
    );
  }
}
