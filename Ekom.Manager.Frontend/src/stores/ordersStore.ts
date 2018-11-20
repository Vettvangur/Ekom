import { observable, action, runInAction } from 'mobx';

import { IOrderModel } from 'models/Ekom/order';

export default class OrdersStore {
  @observable state = "pending"; // "pending" / "loading" / "done" / "error"

  @observable order: IOrderModel;
  @observable orderId: string;

  @action 
  shouldFetchOrder(orderId: string) {
    if (this.order && this.order.UniqueId === orderId) {
      return;
    }
    this.fetchOrder(orderId);
  }
  @action
  fetchOrder(orderId: string) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getorderinfo?uniqueId=${orderId}`, {
      credentials: 'include',
    })
    .then(response => response.json())
    .then(result => {
      this.order = result;
    });
  }
  @action
  fetchActivityLog(orderId: string) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getOrderActivityLog?orderid=${orderId}`, {
      credentials: 'include',
    })
    .then(response => response.json())
    .then(result => {
      this.order = result;
    });
  }


  @action
  async updateOrderStatus(orderId: string, orderStatus, ShouldSendNotification?: boolean) {
    this.state = "pending";
    try {
      this.state = "loading";
      await this.handleUpdateStatus(orderId, orderStatus)
      runInAction(() => {
        setTimeout(() => {
          this.state = "done";
        }, 1500);
      })
    } catch (error) {
      runInAction(() => {
        this.state = "error";
      })
    }
  }
  @action
  handleUpdateStatus(orderId, orderStatus) {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/updatestatus?orderId=${orderId}&orderStatus=${orderStatus}`,
      {
        credentials: 'include',
        method: 'POST',
      }
    )
      .then(() => {
        Promise.resolve()
      })
      .catch((err) => Promise.reject(err))
  }
}