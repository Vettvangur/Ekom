import { observable, action } from "mobx";
import OrderLine from './orderline';

import { IOrderLineNew } from 'models/orders'
const request = require('superagent');
const apiPath = '/umbraco/backoffice/ekom/managerapi';

class NewOrderStore {
  transportLayer;
  @observable orders: IOrderLineNew[] = [];
  @observable isLoading = true;

  constructor() {
    this.orders = [];
    this.loadOrders(null)
    
  }

  // @computed get 

  @action loadOrders(event: React.FormEvent<HTMLFormElement>) {
    this.fetchOrders(event).then(res => {
      if (res.ok) {
        res.body.Orders.forEach(order => this.updateOrderFromServer(order))
      }
      console.log(res)
    }).then(() => {
      console.log(this.orders)
    })
  }

  updateOrderFromServer(json) {
    let order = this.orders.find(order => order.uniqueId === json.UniqueId);
    if (!order) {
      order = new OrderLine(this);
      this.orders.push(order);
    }
    if (json.isDeleted) {
      this.removeOrder(order);
    } else {
      order.updateFromJson(json);
    }
  }
  removeOrder(order) {
      this.orders.splice(this.orders.indexOf(order), 1);
      order.dispose();
  }

  async fetchOrders(event: React.FormEvent<HTMLFormElement>) {
    const startDate = event && event.currentTarget['startDate'].value || '2018-01-01';
    const endDate = event && event.currentTarget['endDate'].value || '2018-12-31';
    const orderStatus = event && event.currentTarget['orderStatus'].value || null;
    const queryString = event && event.currentTarget['searchValue'].value || null;
    const store = event && event.currentTarget['searchValue'].value || null;
    return await request
      .get(`${apiPath}/searchorders`)
      .query({ 'start': startDate })
      .query(endDate && { 'end': endDate })
      .query(queryString && { 'query': queryString })
      .query(orderStatus && { 'orderStatus': orderStatus })
      .query(store && { 'store': store })

    // const url = `/umbraco/backoffice/ekom/managerapi/searchorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}${filters}`
    // return await fetch(url).then(res => res.ok ? res.json() : Promise.reject());
  }

}

export default NewOrderStore;