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
  }  

  componentDidMount() {

    this.getOrders().then((orders) => {

      console.log(orders)
      this.setState({
        defaultData: orders,
        orders: orders,
        loading: false,
      });

    });
  }

  getOrders() {
    [
      "/umbraco/backoffice/ekom/managerapi/getorder?",
      "/umbraco/backoffice/ekom/managerapi/"
    ]
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

          <div data-query="AllOrders">All</div>
          <div data-query="ReadyForDispatch">Incomplete</div>

          <Link to="#" className={s.brand}>Orders</Link>
          <ul className={s.list}>
            <li className={s.link}>All</li>
            <li className={s.link}>test</li>
          </ul>
        </nav>

        <div className="page-content">
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
