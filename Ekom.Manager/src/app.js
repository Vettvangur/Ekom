import 'styles/app.scss';

// Expose React and ReactDOM globally, used by ReactJS.NET SSR.
import 'expose-loader?React!react';
import 'expose-loader?ReactDOM!react-dom';

import React from 'react';
import { BrowserRouter as Router } from 'react-router-dom';


const AppManager = props => (
    <Router>
        <h1>asdasd</h1>
    </Router>
);

ReactDOM.render(AppManager, document.getElementById('app'));

// We could also wrap this module with a new base file and require+expose this module from there
// Instead we shortcut and access webpack's internal way of globalizing modules
global['AppManager'] = AppManager;
