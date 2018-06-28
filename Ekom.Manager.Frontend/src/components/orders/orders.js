import React, { Component } from 'react';
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';

export default class Orders extends Component {

  constructor(props) {
    super(props);

    this.state = {
      orders: [],
      loading: true,
    }
  }  

  componentDidMount() {

    this.getOrders().then((orders) => {

      console.log(orders);

      this.setState({
        orders: orders,
        loading: false,
      });

    });
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

  render() {

    const {
      loading,
      orders,
    } = this.state;

    var columns = [
      {
        Header: 'Orders',
        columns: [
          {
            Header: 'OrderNumber',
            accessor: 'OrderNumber',
            Cell: row => (
              <a href="#">
                {row.value}
              </a>
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
        {orders.length
        ? <ReactTable
            data={orders}
            filterable
            columns={columns}
            defaultPageSize={40}
            loading={loading}
            className="-striped -highlight"
          />
        : null}
      </main>
    );
  }
}
