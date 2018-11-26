import * as React from 'react';

import styled from 'styled-components';
import { renderRoutes } from 'react-router-config';

import Menu from 'components/shared/Menu'

import * as variables from 'styles/variablesJS';


const LayoutWrapper = styled.div`
  display:flex;
  background-color: ${variables.layoutColor};
`;

/**
 * width: calc(100vw - 200px); (200px | 12.5rem is the width of the menu.)
 */
const LayoutBody = styled.div`
  flex: 1 1 0;
  box-shadow: -2.01285px -2.01285px 15.0964px rgba(0, 0, 0, 0.05);
  background-color: ${variables.white};
  width: calc(100vw - 12.5rem);
  overflow: auto;
  height: 100vh;
  overflow-x: hidden;
  @media screen and (max-width: 620px) {
    margin-top: 60px;
    padding-bottom:60px;
  }
`;

const Layout = ({ route }) => (
  <LayoutWrapper>
    <Menu />
    <LayoutBody>
      {renderRoutes(route.routes)}
    </LayoutBody>
  </LayoutWrapper>
)

export default Layout;