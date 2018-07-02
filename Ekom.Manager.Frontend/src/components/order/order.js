import React, { Component } from 'react';
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';

import statusList from '../../utilities/statusList';

export default class Orders extends Component {

  constructor(props) {
    super(props);
    this.state = {
      order: {},
    }
  }
  componentDidMount() {
    const { match } = this.props;
    
    const orderId = match.params.id;
    this.getOrder(orderId).then((res) => {
      this.setState({order: res});
    })
  }
  getOrder(id) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getorder?uniqueId=${id}`, {
      credentials: 'include',
    }).then(function (response) {
      return response.json();
    }).then(function (result) {
      return result;
    });
  }
  render() {
    const { order } = this.state;
    console.log(order)
    return (
      <main>

        <div className="page-content">

          <h1>Ordernumber({order.OrderNumber})</h1>
          <select name="" id="">
          {statusList.map(status => {
            if (status.id === order.OrderStatus) {
              return <option selected value={status.id}>{status.value}</option>
            } else {
              return <option value={status.id}>{status.value}</option>
            }
          })}
          </select>
          <div>Date/Time Placed: {order.CreateDate}</div>

          <div className="flex flex__justify--between">

            <div className="cell">
              <h3>Billing</h3>
              <p>
                {order.CustomerName}<br/>
                {order.CustomerEmail}<br/>
                {order.CustomerAddress}<br/>
                {order.ShippingCountry}<br/>
              </p>
            </div>
            <div className="cell">
              <h3>Shipping</h3>
            </div>

          </div>

          <div className="flex">
            <div className="cell">
              <h3>Payment</h3>
            </div>
          </div>

        </div>

      </main>
    );
  }
}
