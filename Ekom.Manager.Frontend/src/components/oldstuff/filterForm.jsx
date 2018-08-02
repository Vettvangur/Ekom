import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import _ from "lodash";
import orderStore from 'stores/orderStore';
import ReactTable from 'react-table';

export default class Orders extends Component {

  constructor(props) {
    super(props);

    this.state = {
      start: Date(),
      end: Date(),
    };

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


  getStores() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getstores', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      console.log(json)
    })
  }

  getPaymentProviders() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getpaymentproviders', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      console.log(json)
    })
  }
  getShippingProviders() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getshippingproviders', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      console.log(json)
    })
  }

  getDiscounts() {
    return fetch('/umbraco/backoffice/ekom/managerapi/getdiscounts', {
      credentials: 'include',
    })
    .then(response => response.json())
    .then((json) => {
      console.log(json)
    })
  }

  render() {
    
    const { start, end } = this.state

    return (
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
            </select>
          </div>
          <div className="input">
            <label htmlFor="payment">Payment option:</label>
            <select name="payment">
              <option value="all">All payment options</option>
            </select>
          </div>
          <div className="input">
            <label htmlFor="shipping">Shipping option:</label>
            <select name="shipping">
              <option value="all">All shipping options</option>
            </select>
          </div>
          <div className="input">
            <select>
              <option value="all">All discounts</option>
            </select>
          </div>
          <div className="input">
            <label htmlFor="search">Search:</label>
            <input type="text" id="search" name="search" />
          </div>
          
          <button type="submit">Search</button>
        </form>
    );
  }
}
