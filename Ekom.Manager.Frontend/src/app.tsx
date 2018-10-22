import * as React from 'react';

import {
  BrowserRouter as Router,
  Route,
  Switch,
} from 'react-router-dom';
import { observer, Provider } from 'mobx-react';
import './models/window';
import 'es6-object-assign';

import { NotFound } from './routing';

import Navigation from 'components/shared/navigation'

import Dashboard from 'components/dashboard/dashboard'
import Orders from 'containers/orders'
import Order from 'containers/order'

import OrdersStore from 'stores/ordersStore';

interface IProps {
  adventuresApi: string;
  upsellApi: string;
  language?: string;
  currency?: string;
}

@observer
export default class App extends React.Component<IProps> {

  public static defaultProps: Partial<IProps> = {
    language: 'en',
    currency: 'ISK',
  };

  ordersStore: OrdersStore;

  constructor(props: IProps) {
    super(props);

    this.ordersStore = new OrdersStore();
  }

  componentDidMount() {

    this.ordersStore.getOrders()
  }

  render() {
    const { 
      language,
    } = this.props;
    const {
      loading,
      error,
    } = this.ordersStore;

    var match = {
      url: '/umbraco/backoffice/ekom/manager'
    };
    return loading ? 'loading' : error || (
      /* 
        We use an always rendering route to extract location from it's render.
        Location is required by the react-router Switch component.
       */
      <Router>
      <Provider language={language} ordersStore={this.ordersStore}>
        <React.Fragment>
          <Navigation path={match.url} />
          <Switch>
            <Route
              exact
              path={match.url + '/'}
              component={Dashboard}
            />
            <Route
              exact
              path={match.url + '/orders'}
              component={Orders}
            />
            <Route
              exact
              path={match.url + '/order/:id'}
              component={Order}
            />

            <Route component={NotFound} />
          </Switch>
        </React.Fragment>
      </Provider>
      </Router>
    );
  }
}
