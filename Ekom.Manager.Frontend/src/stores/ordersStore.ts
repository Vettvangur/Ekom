import { observable, action, runInAction } from 'mobx';


export default class OrdersStore {
  @observable state = "pending"; // "pending" / "loading" / "done" / "error"

  @action
  async updateOrderStatus(orderId, orderStatus) {
    this.state = "pending";
    try {
      this.state = "loading";
      await this.handleUpdateStatus(orderId, orderStatus)
      runInAction(() => {
        setTimeout(() => {
          this.state = "done";
        },1500);
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