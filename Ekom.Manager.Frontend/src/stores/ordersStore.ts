import { observable, action, runInAction, flow } from 'mobx';

import { IOrderModel } from 'models/Ekom/order';

import { IActivityLog } from 'models/Ekom/activityLog';


export default class OrdersStore {
  @observable state = "pending"; // "pending" / "loading" / "done" / "error"

  @observable orderState = "pending"; // "pending" / "loading" / "done" / "error"

  @observable order: IOrderModel;
  @observable orderId: string;

  @observable latestUserActivityLog: IActivityLog[] = [];
  @observable latestActivityLog: IActivityLog[] = [];


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
  

  getActivityLog(orderId) {
    this.fetchActivityLog(orderId).then(activityLog => this.order.ActivityLog = activityLog);
  }

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

  @action('Gets latest activity log that was worked in')
  fetchLatestActivityLogs = () => {
    return fetch(`/umbraco/backoffice/ekom/managerapi/getLatestActivityLogs`, {
      credentials: 'include',
    })
    .then(res => res.ok 
      ? Promise.resolve(res.json()) 
      : Promise.reject(res)
    )
    .then(json => this.latestActivityLog = json)
    .catch((err) => {
      this.latestActivityLog = []
      console.log(err);
    })
  }
  @action('Gets latest activity log that was worked on by user')
  fetchLatestActivityLogByUser = (userName: string) => {
    return fetch(`/umbraco/backoffice/ekom/managerapi/GetLatestActivityLogsByUser?username=${userName}`, {
      credentials: 'include',
    })
    .then(res => res.ok 
      ? Promise.resolve(res.json()) 
      : Promise.reject(res)
    )
    .then(json => {
      console.log("j", json)
      this.latestUserActivityLog = json
    })
    .catch((err) => {
      this.latestUserActivityLog = []
      console.log(err);
    })
  }

  @action
  updateOrderStatus = (orderId: string, orderStatus, ShouldSendNotification?: boolean) => {
    this.order.OrderStatus = orderStatus;
    this.doUpdateOrderStatus(orderId, orderStatus,ShouldSendNotification)
  }

  @action
  async doUpdateOrderStatus(orderId: string, orderStatus, ShouldSendNotification?: boolean) {
    this.state = "pending";
    try {
      this.state = "loading";
      await this.handleUpdateStatus(orderId, orderStatus, ShouldSendNotification)
      runInAction(() => {
        setTimeout(() => {
          this.state = "done";
          this.getActivityLog(orderId)
        }, 1500);
      })
    } catch (error) {
      runInAction(() => {
        this.state = "error";
      })
    }
  }
  @action
  handleUpdateStatus(orderId, orderStatus, ShouldSendNotification?: boolean) {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/updatestatus?orderId=${orderId}&orderStatus=${orderStatus}&notification=${ShouldSendNotification ? 'true' : 'false'}`,
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