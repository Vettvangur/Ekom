import * as moment from 'moment';

import { observable, action } from 'mobx';

import { Resize, Filter, SortingRule } from 'react-table';

import { IOrdersData } from 'models/orders'

export default class OrdersStore {
  id = Math.random();
  @observable loading = true;
  @observable error = null;
  @observable showDatePicker
  @observable preset = 'Last week';
  @observable startDate: moment.Moment;
  @observable endDate: moment.Moment;
  @observable ordersData?: IOrdersData;
  @observable page: number;
  @observable sorted: SortingRule[];
  @observable pageSize: number;
  @observable expanded: any;
  @observable resized: Resize[];
  @observable filtered: Filter[];
  
  @observable showRefund: boolean;
  @observable isFetching = false;
  @observable isUpdating = false;
  @observable updateFailed = false;
  @observable fetchingFailed = false;

  constructor() {
    this.ordersData = {
      AverageAmount: "",
      Count: 0,
      GrandTotal: "",
      Orders: []
    };
    this.startDate = moment().subtract(1, 'week');
    this.endDate = moment();
    this.page = 0;
    this.pageSize = 10;
    this.showRefund = false;
  }
  

  @action
  setDates(start, end) {
    return new Promise((resolve, reject) => {
      this.startDate = start;
      this.endDate = end;
      resolve();
    })
  }

  @action
  shouldGetOrders() {
    if (this.ordersData && this.ordersData.Count === 0) {
      this.getOrders();
    }
    return false;
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
        console.log(res)
        this.ordersData = res;
        console.log(this.ordersData)
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
    this.isUpdating = true;
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/updatestatus?orderId=${orderId}&orderStatus=${orderStatus}`, 
      {
        credentials: 'include',
        method: 'POST',
      }
    )
    .then(() => {
      setTimeout(() => {
        this.isUpdating = false;
      }, 150000);
    },
    err => {
      if (err.status === 400
      || err.status === 404) {
        location.assign('/');
      }

      this.isUpdating = false;
    })
    .catch(err => {
      if (err.status === 400
      || err.status === 404) {
        location.assign('/');
      }

      this.isUpdating = false;
    })
  }

  @action
  search(query) {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/searchorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}&query=${query}`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    ).then(
      (res) => {
        this.ordersData = res;
        this.loading = false;
      },
      err => {
        if (err.status === 400
        || err.status === 404) {
          location.assign('/umbraco/backoffice/ekom/manager/orders');
        }

        this.loading = false;
        console.error(err);
      }
    );
  }
  
  @action
  closeDatePicker() {

  }

  @action
  handleSearchInput(e) {

  }

  @action
  handlePageSize(pageSize) {
    this.pageSize = pageSize;
  }
  
  @action
  onKeyPressed(e) {

  }

  @action
  onSortedChange(sorted) {
    this.sorted = sorted;
  }
  @action
  onPageChange(page) {
    this.page = page;
  }
  @action
  onPageSizeChange(pageSize, page) {
    this.pageSize = pageSize;
    this.page = page;
  }
  @action
  onExpandedChange(expanded) {
    this.expanded = expanded;
  }
  @action
  onResizedChange(resized) {
    this.resized = resized;
  }
  @action
  onFilteredChange(filtered) {
    this.filtered = filtered;
  }

  
}