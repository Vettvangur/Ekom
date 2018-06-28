import 'styles/app.scss';

import ReactDOM from 'react-dom'
import React, { Fragment } from 'react';
import { BrowserRouter as Router, Route, Switch } from 'react-router-dom';

import Navigation from 'components/navigation';

import Dashboard from 'components/dashboard';
import Orders from 'components/orders';
import Page404 from 'components/page404';

const path = '/umbraco/backoffice/ekom';

window.ekom = {};

window.ekom.path = path;


ReactDOM.render( 
    <Fragment>
      <Router>
        <Fragment>
          <Navigation />
          <Switch>
            <Route exact path={path + "/manager"} component={Dashboard} />
            <Route exact path={path + "/manager/orders"} component={Orders} />
            <Route exact path={path + "/manager/order/:id"} component={Orders} />
            <Route component={Page404} />
          </Switch>
        </Fragment>
      </Router>
    </Fragment>
    ,
    document.getElementById('app')
)
