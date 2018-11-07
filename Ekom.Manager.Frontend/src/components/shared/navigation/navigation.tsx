import * as React from 'react';
import {
  Link,
  withRouter,
} from 'react-router-dom';
import classNames from 'classnames';

import * as s from './navigation.scss';

interface IProps {
  location: any;
  path: any;
}


class Navigation extends React.Component<IProps, any> {
  public constructor(props) {
    super(props);
  }

  render() {
    const { location, path } = this.props
    return (
      <nav className={s.host}>
        <ul className={s.list}>
          <li className={location.pathname === `${path}/` ? s.active : ''}>
            <Link to={`${path}/`} className={classNames(s.link, 'icon-home3')} title="Dashboard">
              Dashboard
          </Link>
          </li>
          <li className={location.pathname === `${path}/orders` && path ? s.active : ''}>
            <Link to={`${path}/orders`} className={classNames(s.link, 'icon-clipboard-text')} title="Orders">
              Orders
          </Link>
          </li>
        </ul>
      </nav>
    );
  }
}



export default withRouter(Navigation);
