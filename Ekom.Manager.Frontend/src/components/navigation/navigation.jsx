import React from 'react';
import {
  Link,
  withRouter,
} from 'react-router-dom';
import PropTypes from 'prop-types';
import classNames from 'classnames';

import s from './navigation.scss';

const Navigation = (props) => {
  const {
    location,
  } = props;
  return (
    <nav className={s.navigation}>
      <ul className={s.list}>
        <li className={location.pathname === `${window.ekom.path}/manager` ? s.active : ''}>
          <Link to={`${window.ekom.path}/manager`} className={classNames(s.link, 'icon-home3')} title="Dashboard">
            Dashboard
          </Link>
        </li>
        <li className={location.pathname === `${window.ekom.path}/manager/orders` && `${window.ekom.path}/manager/order/` ? s.active : ''}>
          <Link to={`${window.ekom.path}/manager/orders`} className={classNames(s.link, 'icon-clipboard-text')} title="Orders">
            Orders
          </Link>
        </li>
      </ul>
    </nav>
  );
}


Navigation.defaultProps = {
  location: null,
};

Navigation.propTypes = {
  location: PropTypes.shape({
    pathname: PropTypes.string,
  }),
};


export default withRouter(Navigation);
