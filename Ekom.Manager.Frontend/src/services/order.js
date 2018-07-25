import createRestApiClient from '../utilities/createRestApiClient';

//import promisePolyfill from 'es6-promise';
//import 'isomorphic-fetch';
require('es6-promise').Promise;

const apikey = ''
const accessKey = ''

export default () => {
  const client = createRestApiClient().withConfig({ 
    baseURL: '/umbraco/backoffice/ekom/managerapi',
    headers: {
      'Content-Type': 'application/json'
    },
  });
  return {
    updateStatus: (orderId, orderStatus) => client.request({
      method: 'post',
      responseType: 'json',
      url: `/updatestatus?orderId=${orderId}&orderStatus=${orderStatus}`
    }),
  }
}