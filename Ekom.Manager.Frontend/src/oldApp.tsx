import * as React from 'react';
import { hot } from 'react-hot-loader'

import styled from 'styled-components';

import {
  BrowserRouter as Router,
  Route,
  Switch,
} from 'react-router-dom';
import { observer, Provider } from 'mobx-react';
import './models/window';

import { NotFound } from './routing';

import Menu from 'components/shared/Menu'

import Dashboard from 'components/dashboard/dashboard'
import Orders from 'containers/orders'
import Order from 'containers/order'

import OrdersStore from 'stores/ordersStore';


const AppBodyWrapper = styled.div`
  display:flex;
`;

const AppBody = styled.div`
  overflow-x: hidden;
  overflow-y: auto;
  flex: 1 1 0;
`;

interface IProps {
  adventuresApi: string;
  upsellApi: string;
  language?: string;
  currency?: string;
}

@observer
class App extends React.Component<IProps> {

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
          <AppBodyWrapper>
            <Menu path={match.url} />
            <AppBody>
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
            </AppBody>
          </AppBodyWrapper>
        </Provider>
      </Router>
    );
  }
}
export default hot(module)(App)