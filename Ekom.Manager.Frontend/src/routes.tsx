import Layout from './Layout'


import Dashboard from 'containers/Dashboard'
import Customers from 'containers/Customers'
import Orders from 'containers/orders'
import Order from 'containers/order'

const ekomManager = '/umbraco/backoffice/ekom/manager';

export const routes = [
  {
    component: Layout,
    routes: [
      {
        path: `${ekomManager}/`,
        exact: true,
        component: Dashboard,
        title: "Dashboard",
        showInMenu: true
      },
      {
        path: `${ekomManager}/orders`,
        component: Orders,
        title: "Orders",
        showInMenu: true,
      },
      {
        path: `${ekomManager}/customers`,
        exact: true,
        component: Customers,
        title: "Customers",
        showInMenu: true
      },
      { 
        path: `${ekomManager}/order/:id`,
        component: Order,
        title: "Order - :id",
      }
    ]
  },
]
const createRoutes = () => routes;

export default createRoutes