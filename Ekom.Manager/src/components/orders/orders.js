import React, { Component } from 'react';
import orderStore from 'stores/orderStore';

export default class Orders extends Component {

  constructor(props) {
    super(props);

    this.state = {
      orders: []
    };
  }  

  componentDidMount() {

    orderStore.getOrders().then((orders) => {

      console.log(orders);

      this.setState({
        orders: orders
      });

    });

  } 
  render() {

    return (
      <main>
        <h2>Orders</h2>
        <table className="table">
          <thead>
            <tr>
              <th>Ordernumber</th>
              <th>Status</th>
              <th>Email</th>
              <th>Name</th>
              <th>Country</th>
              <th>Created</th>
              <th>Paid</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {this.state.orders.map(order =>
              <tr key={order.UniqueId}>
                <td>{order.OrderNumber}</td>
                <td>{order.OrderStatusCol}</td>
                <td>{order.CustomerEmail}</td>
                <td>{order.CustomerName}</td>
                <td>{order.CustomerCountry}</td>
                <td>{order.CreateDate}</td>
                <td>{order.PaidDate}</td>
                <td>{order.TotalAmount}</td>
              </tr>
            )}
          </tbody>
        </table>  
      </main>
    );
  }
}
