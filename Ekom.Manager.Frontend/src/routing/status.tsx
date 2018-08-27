import * as React from 'react';
import { Route } from 'react-router';

export const Status = props => (
  <Route render={({ staticContext }) => {
    if (staticContext)
      staticContext['status'] = props.code;
    return props.children;
  }}/>
);
