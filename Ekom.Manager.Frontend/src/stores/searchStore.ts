import * as moment from 'moment';

import { observable, action, flow } from 'mobx';
import { IOrders } from 'models/orders'

interface IOption {
  value: (string | number);
  label: string;
}
export default class SearchStore {
  @observable orders?: IOrders;
  @observable startDate: moment.Moment;
  @observable endDate: moment.Moment;
  @observable StatusFilter?: IOption;
  @observable searchString = '';
  @observable storeFilter: IOption;
  @observable state = "empty"; // "pending" / "done" / "error" / "empty"

  @observable statusList: IOption[];

  @observable preset = 'Last week'
  
  @observable presets = [];

  constructor() {
    this.orders = {
      AverageAmount: "",
      Count: 0,
      GrandTotal: "",
      Orders: []
    };
    this.storeFilter = {
      value: 'Default',
      label: 'All stores'
    }
    this.StatusFilter = {
      value: 'Default',
      label: 'Completed orders'
    };
    this.startDate = moment().subtract(1, 'week');
    this.endDate = moment();
    
    const today = moment();
    this.presets = [{
      text: 'Today',
      start: today,
      end: today,
    },
    {
      text: 'Last week',
      start: moment().subtract(1, 'week'),
      end: today,
    },
    {
      text: 'This month',
      start: moment().startOf('month'),
      end: today,
    },
    {
      text: 'Last month',
      start: moment().subtract(1, 'month').startOf('month'),
      end: moment().subtract(1, 'month').endOf('month'),
    },
    {
      text: 'Last Year',
      start: moment().subtract(1, 'year').startOf('year'),
      end:  moment().subtract(1, 'year').endOf('year'),
    },
    {
      text: 'This year',
      start: moment().startOf('year'),
      end: today,
    }];
  }

  @action
  public search = flow(function * (this: SearchStore) {
    this.state = "pending";
    try {
      const json = yield this.fetchSearchResults()
      if (json.Count <= 0)
        setTimeout(() => this.state = "empty", 1000)
      else
        setTimeout(() => this.state = "done", 1000)
      this.setOrders(json)
    } catch (error) {
      console.log("Failed to search", error)
      this.state = "error";
    }
  })
  async fetchSearchResults() {
    let filters = '';
    if (this.searchString.length > 0)
      filters += `&query=${this.searchString}`
    if (this.StatusFilter && this.StatusFilter.value !== "Default")
      filters += `&orderStatus=${this.StatusFilter.value}`
    if (this.storeFilter && this.storeFilter.value !== "Default")
      filters += `&store=${this.storeFilter.value}`

    const url = `/umbraco/backoffice/ekom/managerapi/searchorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}${filters}`
    return await fetch(url).then(res => res.ok ? res.json() : Promise.reject());
  }

  @action('Set Orders')
  setOrders(data) {
    this.orders = data;
  }
  @action('Check if Manager should search for Orders')
  shouldSearchForOrders = () => {
    if (this.orders && this.orders.Count > 0) {
      return;
    }
    this.search();
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
  setPreset(preset) {
    this.preset = preset;
  }

  @action
  setStoreFilter = (option: IOption) => {
    this.storeFilter = option;
  }
  @action
  setStatusFilter = (option: IOption) => {
    this.StatusFilter = option;
  }
  @action
  setSearchString = (e: React.ChangeEvent<HTMLInputElement>) => {
    this.searchString = e.target.value;
  }

  @action
  updateMobxStatus = (uniqueId, orderStatus) => {
    const mobxOrder = this.orders.Orders.filter(c => c.UniqueId === uniqueId)[0]
    mobxOrder.OrderStatusCol = orderStatus;
    mobxOrder.OrderStatus = orderStatus;
  }
}
