import * as React from 'react';

import createBrowserHistory from 'history/createBrowserHistory';
import { hot } from 'react-hot-loader'
import { Provider } from 'mobx-react';
import { RouterStore, syncHistoryWithStore } from 'mobx-react-router';
import {
  Router,
} from 'react-router';
import { renderRoutes } from 'react-router-config';

import OrdersStore from 'stores/ordersStore';
import TableStore from 'stores/tableStore';
import SearchStore from 'stores/searchStore';

import createRoutes from './routes';

const browserHistory = createBrowserHistory();
const routingStore = new RouterStore();

const ordersStore = new OrdersStore();
const searchStore = new SearchStore();
const tableStore = new TableStore();

const stores = {
  routing: routingStore,
  ordersStore: ordersStore,
  searchStore: searchStore,
  tableStore: tableStore,
}

export const routes = createRoutes();

const history = syncHistoryWithStore(browserHistory, routingStore);

const App = () => (
  <Provider {...stores}>
    <Router history={history}>
      {renderRoutes(routes)}
    </Router>
  </Provider>
)

export default hot(module)(App)