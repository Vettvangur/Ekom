import { observable, action } from "mobx";
// import uuid from 'node-uuid';

class OrderLine {
  
  id = null;
  @observable uniqueId: string;
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

  store = null;
  constructor(store) {
    this.store  = store;
  }
  @action
  public updateFromJson = (json) => {
      // make sure our changes aren't sent back to the server
      this.uniqueId = json.UniqueId
      this.ReferenceId = json.ReferenceId
      this.OrderInfo = json.OrderInfo
      this.OrderNumber = json.OrderNumber
      this.OrderStatusCol = json.OrderStatusCol
      this.OrderStatus = json.OrderStatus
      this.CustomerEmail = json.CustomerEmail
      this.CustomerName = json.CustomerName
      this.CustomerId = json.CustomerId
      this.CustomerUsername = json.CustomerUsername
      this.ShippingCountry = json.ShippingCountry
      this.TotalAmount = json.TotalAmount
      this.StoreAlias = json.StoreAlias
      this.CreateDate = json.CreateDate
      this.UpdateDate = json.UpdateDate
      this.PaidDate = json.PaidDate
  }
}

export default OrderLine