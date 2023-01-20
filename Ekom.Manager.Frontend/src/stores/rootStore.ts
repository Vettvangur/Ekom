import { observable, action } from 'mobx';


interface IOption {
  value: (string | number);
  label: string;
}
class rootStore {
  @observable stores: IOption[] = [
    {
      value: 'Default',
      label: 'All stores'
    }
  ];
  
  @observable statusList: IOption[] = [
    {
      value: 'Default',
      label: 'Completed orders'
    }
  ];
  
  @observable activityLogWindowType: string;
  @observable activityLogWindowData?;
  @observable showActivityLogWindow: boolean;
  
  constructor() {
    this.getStores();
    this.getStatusList();
  }

  @action
  getStores() {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/getstorelist`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    )
    .then((res) => {
      res.forEach(store => this.stores.push(store))
    })
  }
  @action('Get Status List')
  getStatusList() {
    return fetch(
      `/umbraco/backoffice/ekom/managerapi/getstatuslist`, 
      {
      credentials: 'include',
      }
    )
    .then(res => res.ok
      ? res.json()
      : Promise.reject(res)
    )
    .then((res) => {
      res.forEach(status => this.statusList.push(status))
    })
  }
}

export default rootStore;
