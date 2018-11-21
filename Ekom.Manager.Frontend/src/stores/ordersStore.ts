import { observable, action, runInAction, flow } from 'mobx';

import { IOrderModel } from 'models/Ekom/order';


export default class OrdersStore {
  @observable state = "pending"; // "pending" / "loading" / "done" / "error"

  @observable orderState = "pending"; // "pending" / "loading" / "done" / "error"

  @observable order: IOrderModel;
  @observable orderId: string;


  getOrder = flow(function * (orderId: string) {
    this.state = "pending";
    try {
      this.orderState = "loading";
      const order = yield this.fetchOrder(orderId);
      if (!order) {
        this.orderState = "error"
        this.cancel();
      }
      this.order = order;
      const activityLog = yield this.fetchActivityLog(orderId);
      this.order.ActivityLog = activityLog;
      this.orderState = "done";
    } catch (error) {
      this.orderState = "error";
    }
  })

  @action 
  shouldFetchOrder(orderId: string) {
    if (this.order && this.order.UniqueId === orderId) {
      return;
    }
    this.getOrder(orderId);
  }

  @action
  fetchOrder(orderId: string) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getorderinfo?uniqueId=${orderId}`, {
      credentials: 'include',
    })
    .then(res => res.ok 
      ? Promise.resolve(res.json()) 
      : Promise.reject(res)
    )
    .then(result => result)
    .catch(err => {
      console.log(err);
      return err.ok;
    });
  }
  @action
  fetchActivityLog(orderId: string) {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getActivityLog?orderid=${orderId}`, {
      credentials: 'include',
    })
    .then(res => res.ok 
      ? Promise.resolve(res.json()) 
      : Promise.reject(res)
    )
    .then(json => json)
    .catch((err) => {
      console.log(err);
      this.order.ActivityLog = [];
    })
  }

  @action
  updateOrderStatus = (orderId: string, orderStatus, ShouldSendNotification?: boolean) => {
    this.doUpdateOrderStatus(orderId, orderStatus)
  }

  @action
  async doUpdateOrderStatus(orderId: string, orderStatus, ShouldSendNotification?: boolean) {
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