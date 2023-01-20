import * as React from 'react';
import * as PropTypes from 'prop-types';
import { Route, Redirect } from 'react-router';

export const RedirectWithStatus = ({ from, to, status }) => (
  <Route
    render={({ staticContext }) => {
      // there is no `staticContext` on the client, so
      // we need to guard against that here
      if (staticContext) staticContext.statusCode = status;
      return <Redirect from={from} to={to} />;
    }}
  />
);

RedirectWithStatus['propTypes'] = {
  from: PropTypes.string,
  to: PropTypes.string,
  status: PropTypes.number,
};
