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
        <ul>
          {this.state.orders.map(order =>
            <li key={order.UniqueId}>{order.OrderNumber}</li>
          )}
        </ul>  
      </main>
    );
  }
}
