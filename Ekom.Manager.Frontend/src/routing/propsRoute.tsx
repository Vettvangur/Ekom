import * as React from 'react';
import * as PropTypes from 'prop-types';
import { CSSTransition } from 'react-transition-group';
import { Route } from 'react-router';

const renderMergedProps = (Component, ...rest) => {
  const finalProps = Object.assign({}, ...rest);
  return <Component {...finalProps} />;
};

export const PropsRoute = ({ component, ...rest }) => (
  <Route
    {...rest}
    render={routeProps => (
      <CSSTransition
        classNames="he"
        timeout={{
          enter: 1000,
          exit: 1000,
        }}
        {...rest}>
        {renderMergedProps(component, routeProps, rest)}
      </CSSTransition>
    )}
  />
);

PropsRoute['propTypes'] = {
  component: PropTypes.any,
};
