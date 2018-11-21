import * as moment from 'moment';

import { observable, action, runInAction } from 'mobx';
import { IOrders } from 'models/orders'

export default class SearchStore {
  @observable orders?: IOrders;
  @observable startDate: moment.Moment;
  @observable endDate: moment.Moment;
  @observable searchString = '';
  @observable storeFilter = '';
  @observable state = "pending"; // "pending" / "done" / "error"

  @observable preset = 'Last week'
  
  @observable presets = [];

  @observable stores?: any;

  constructor() {
    this.getStores();
    this.orders = {
      AverageAmount: "",
      Count: 0,
      GrandTotal: "",
      Orders: []
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
  async search() {
    this.state = "pending";
    try {
      const searchResponse = await this.fetchSearchResults()
      runInAction(() => {
        this.state = "done";
        this.orders = searchResponse;
      })
    } catch (error) {
      runInAction(() => {
        this.state = "error";
      })
    }
  }
  @action
  fetchSearchResults() {
    let filters = '';
    if (this.searchString.length > 0)
      filters += `&query=${this.searchString}`
    if (this.storeFilter.length > 0)
      filters += `&store=${this.storeFilter}`
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/searchorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}${filters}`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    )
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
  getStores() {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/getstores`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    )
    .then((res) => {
      this.stores = res;
    })
  }
  @action
  setStoreFilter = (store?: string) => {
    if (store)
      this.storeFilter = store;
    else 
      this.storeFilter = "";
  }
  @action
  setSearchString = (e: React.ChangeEvent<HTMLInputElement>) => {
    this.searchString = e.target.value;
  }
}