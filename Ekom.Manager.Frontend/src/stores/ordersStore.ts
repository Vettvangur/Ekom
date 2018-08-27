import * as moment from 'moment';

import { observable, action, extendObservable } from 'mobx';

export default class OrdersStore {
  constructor() {}
  id = Math.random();
  @observable loading = true;
  @observable error = null;
  @observable showDatePicker
  @observable preset = 'Last week';
  @observable startDate = moment();
  @observable endDate = moment().subtract(1, 'year');
  @observable orders;

  @observable searchString = '';

  @action
  setDates(start, end) {
    return new Promise((resolve, reject) => {
      this.startDate = start;
      this.endDate = end;
      resolve();
    })
  }

  @action
  getOrders() {
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
        this.orders = res;
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

  @action
  setPreset(preset) {
    this.preset = preset;
  }

  @action
  updateStatus(orderId, orderStatus) {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/updatestatus?orderId=${orderId}&orderStatus=${orderStatus}`, 
      {
        credentials: 'include',
        method: 'POST',
      }
    )
  }

  @action
  search() {

  }
  
  @action
  closeDatePicker() {

  }

  @action
  handleSearchInput(e) {

  }
  
  @action
  onKeyPressed(e) {

  }
}