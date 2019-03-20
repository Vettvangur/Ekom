import * as React from 'react';
import * as ReactDOM from 'react-dom';

import App from './App';
import createBrowserHistory from 'history/createBrowserHistory';
import { Provider } from 'mobx-react';
import { RouterStore, syncHistoryWithStore } from 'mobx-react-router';
import {
  Router,
} from 'react-router';

import RootStore from 'stores/rootStore';
import OrdersStore from 'stores/ordersStore';
import TableStore from 'stores/tableStore';
import SearchStore from 'stores/searchStore';

const browserHistory = createBrowserHistory();
const routingStore = new RouterStore();

const rootStore = new RootStore();
const ordersStore = new OrdersStore();
const searchStore = new SearchStore();
const tableStore = new TableStore();

const stores = {
  routing: routingStore,
  rootStore: rootStore,
  ordersStore: ordersStore,
  searchStore: searchStore,
  tableStore: tableStore,
}

const history = syncHistoryWithStore(browserHistory, routingStore);

ReactDOM.render(
  <Provider {...stores}>
    <Router history={history}>
      <App />
    </Router>
  </Provider>,
  document.getElementById('app'),
);