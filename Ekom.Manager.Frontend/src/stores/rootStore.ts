import { observable, action } from 'mobx';

class rootStore {
  @observable stores: any;
  
  constructor() {
    this.getStores();
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
}

export default rootStore;