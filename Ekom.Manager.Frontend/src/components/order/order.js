import React, { Component } from 'react';
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';

import s from './order.scss';

import statusList from '../../utilities/statusList';

export default class Orders extends Component {

  constructor(props) {
    super(props);
    this.state = {
      order: null,
    }
  }
  componentDidMount() {
    const { match } = this.props;
    
    const orderId = match.params.id;
    this.getOrder(orderId).then((res) => {
      this.setState({
        order: res,
      });
    })
  }
  getOrder(id) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getorderinfo?uniqueId=${id}`, {
      credentials: 'include',
    }).then(function (response) {
      return response.json();
    }).then(function (result) {
      return result;
    });
  }
  render() {
    const { order } = this.state;


    return (
      <main>

        <div className="page-content">

          {order != null ?
            <div className={s.container}>
              <h1>Ordernumber({order.OrderNumber})</h1>
              <div className="select__wrapper">
                <select name="" id="">
                  {statusList.map(status => {
                    if (status.id === order.OrderStatus) {
                      return <option selected value={status.id}>{status.value}</option>
                    } else {
                      return <option value={status.id}>{status.value}</option>
                    }
                  })}
                </select>
              </div>

              <div>Date/Time Placed: {order.CreateDate}</div>
              
              <div className="flex flex__justify--between">

                <div className="cell">
                  <h3>Billing</h3>
                  <p>
                    {order.CustomerInformation.Customer.Name}<br/>
                    {order.CustomerInformation.Customer.Email}<br/>
                    {order.CustomerInformation.Customer.ZipCode} - {order.CustomerInformation.Customer.Address}<br/>
                    {order.CustomerInformation.Customer.Country}<br/>
                  </p>
                </div>
                
                <div className="cell">
                  <h3>Shipping</h3>
                  <p>
                    {order.CustomerInformation.Shipping.Name}<br/>
                    {order.CustomerInformation.Shipping.Email}<br/>
                    {order.CustomerInformation.Shipping.ZipCode} - {order.CustomerInformation.Shipping.Address}<br/>
                    {order.CustomerInformation.Shipping.Country}<br/>
                  </p>
                </div>

              </div>

              <div className="flex">
                <div className="cell">
                  <h3>Payment</h3>
                  <span>{order.PaymentProvider.Title}</span>
                </div>
              </div>

              <div className="flex">
                <div className="cell">
                  <h3>Delivery</h3>
                  {order.OrderLines.map(orderline => 
                    <li>{orderline.Product.Properties.nodeName}</li>
                  )}
                </div>
              </div>
            </div>
            : ""
          }

        </div>

      </main>
    );
  }
}
