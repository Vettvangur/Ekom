import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import classNames from 'classnames';

import s from './navigation.scss';

export default class Navigation extends Component {
    render() {

        return (
          <nav className={s.navigation}>
            <ul className={s.list}>
              <li><Link to={window.ekom.path + "/manager"} className={classNames(s.link, "icon-home3")} title="Dashboard">Dashboard</Link></li>
              <li><Link to={window.ekom.path + "/manager/orders"} className={classNames(s.link, "icon-clipboard-text")} title="Orders">Orders</Link></li>
            </ul>
          </nav>
        );
    }
}
