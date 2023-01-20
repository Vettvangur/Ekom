import * as moment from 'moment';
import { observable, action } from 'mobx';

import CustomerModel from './models/CustomerModel';

export default class CustomersStore {
  @observable state = "pending"; // "pending" / "loading" / "done" / "error"
  @observable customers = [];

  
  // Search bar
  @observable startDate: moment.Moment;
  @observable endDate: moment.Moment;
  @observable searchString;
  @observable storeFilter;
  @observable preset = 'Last week'
  @observable presets = [];

  @action
  loadCustomers() {
    this.state = "loading";
    this.fetchCustomers().then(fetchedCustomers => {
      fetchedCustomers.forEach(json => this.updateCustomersFromServer(json))
      this.state = "done";
    })
  }

  @action
  fetchCustomers() {
    let filters = '';
    if (this.searchString.length > 0)
      filters += `&query=${this.searchString}`
    if (this.storeFilter.length > 0)
      filters += `&store=${this.storeFilter}`
    const url = `/umbraco/backoffice/ekom/managerapi/searchorders?start=${moment(this.startDate).format('YYYY-MM-DD')}&end=${moment(this.endDate).format('YYYY-MM-DD')}${filters}`
    return fetch(url).then(res => res.ok ? res.json() : Promise.reject());
  }

  @action
  updateCustomersFromServer(json) {
    let customer = this.customers.find(customer => customer.UniqueId === json.UniqueId);
    if (!customer) {
      customer = new CustomerModel(this, json.UniqueId, json.Username, json.Email, json.StoreAlias, json.OrdersCount);
    }
  }
}