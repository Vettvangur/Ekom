import { observable } from 'mobx';

export default class OrderLine {
  @observable UniqueId: string;
  @observable ReferenceId: number;
  @observable OrderInfo: string;
  @observable OrderNumber: string;
  @observable OrderStatusCol: number;
  @observable OrderStatus: number;
  @observable CustomerEmail: string;
  @observable CustomerName: string;
  @observable CustomerId: number;
  @observable CustomerUsername: string | null;
  @observable ShippingCountry: string | null;
  @observable TotalAmount: number;
  @observable Currency: string;
  @observable StoreAlias: string;
  @observable CreateDate: string;
  @observable UpdateDate: string;
  @observable PaidDate: string;
}