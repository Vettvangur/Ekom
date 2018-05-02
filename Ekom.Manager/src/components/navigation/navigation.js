import React, { Component } from 'react';
import { Link } from 'react-router-dom';

import s from './navigation.scss';

export default class Navigation extends Component {
    render() {

        return (
          <nav className={s.navigation}>
            <Link to={window.ekom.path + "/manager"} className={s.brand}>E</Link>
            <ul>
              <li><Link to={window.ekom.path + "/manager"} className={s.link}>Dashboard</Link></li>
              <li><Link to={window.ekom.path + "/manager/orders"} className={s.link}>Orders</Link></li>
            </ul>
          </nav>
        );
    }
}
