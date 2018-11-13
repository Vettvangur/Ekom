import * as moment from 'moment';

import { observable, action, ObservableMap } from 'mobx';

import OrderLine from 'stores/models/OrderLine';

import { IOrder } from 'models/orders';

export default class OrdersStore {
  @observable orders: IOrder[];
  //@observable orders: ObservableMap<number, OrderLine> = observable.map();

  @observable preset = 'Last week';
  @observable startDate: moment.Moment;
  @observable endDate: moment.Moment;
  

  constructor() {}

  @action
  fetchOrders() {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/getallorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    ).then(
      (res) => {
        res.orders.foreach()
        this.orders = res.Orders;
        this.grandTotal = res.grandTotal;
        this.averageAmount = res.AverageAmount;
        this.count = res.Count;
        this.loading = false;
      },
      err => {
        if (err.status === 400
        || err.status === 404) {
          location.assign('/');
        }

        this.loading = false;
        console.error(err);
      }
    );
  }


}