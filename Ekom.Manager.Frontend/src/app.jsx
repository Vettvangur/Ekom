import ReactDOM from 'react-dom';
import React, {
  Fragment,
} from 'react';
import {
  BrowserRouter as Router,
  Route,
  Switch,
} from 'react-router-dom';

import Navigation from 'components/navigation';
import {
  Dashboard,
  Orders,
  Order,
  Page404,
} from 'pages';

import 'styles/app.scss';

const path = '/umbraco/backoffice/ekom';

window.ekom = {};

window.ekom.path = path;


ReactDOM.render(
  <Fragment>
    <Router>
      <Fragment>
        <Navigation />
        <Switch>
          <Route exact path={`${path}/manager`} component={Dashboard} />
          <Route exact path={`${path}/manager/orders`} component={Orders} />
          <Route exact path={`${path}/manager/order/:id`} component={Order} />
          <Route component={Page404} />
        </Switch>
      </Fragment>
    </Router>
  </Fragment>,
  document.getElementById('app'),
);
