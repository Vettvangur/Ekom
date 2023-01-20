import { observable } from 'mobx';

export default class OrderModel {
  store;

  @observable customer = {
    name: "",
    email: "",
    address: "",
    zipcode: Number,
    city: "",
    Country: ""
  }
  @observable shipping = {
    name: "",
    email: "",
    address: "",
    zipcode: Number,
    city: "",
    Country: ""
  }

  @observable PaidDate;
  @observable PaymentProvider;

  
  @observable orderlines = {};



  constructor(store) {
    this.store = store;
  }

}