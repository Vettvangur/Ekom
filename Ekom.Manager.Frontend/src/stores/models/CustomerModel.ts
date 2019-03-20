import { observable } from 'mobx';

export default class CustomerModel {
  id = Math.random();
  @observable UniqueId: string;
  @observable Username: string;
  @observable Email: string;
  @observable StoreAlias: string;
  @observable OrdersCount: number;
  @observable OrderLines?: string[];

  store = null;

  constructor(store, UniqueId, Username, Email, StoreAlias, OrdersCount) {
    this.store = store;
    this.UniqueId = UniqueId;
    this.Username = Username;
    this.Email = Email;
    this.StoreAlias = StoreAlias;
    this.OrdersCount = OrdersCount;
  }
}