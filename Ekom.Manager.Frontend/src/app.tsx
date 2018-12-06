import * as React from 'react';
import { hot } from 'react-hot-loader'
import { renderRoutes } from 'react-router-config';
import createRoutes from './routes';

import 'styles/app.scss';

export const routes = createRoutes();

const App = () => (
  <>
    {renderRoutes(routes)}
  </>
)
export default hot(module)(App)