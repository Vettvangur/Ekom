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
    path
  } = props;

  console.log(location)
  console.log(path)
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


Navigation.defaultProps = {
  location: null,
};

Navigation.propTypes = {
  location: PropTypes.shape({
    pathname: PropTypes.string,
  }),
};


export default withRouter(Navigation);
