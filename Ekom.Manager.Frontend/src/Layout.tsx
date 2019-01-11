import * as React from 'react';

import styled from 'styled-components';
import { renderRoutes } from 'react-router-config';
import { observer, inject } from 'mobx-react';

import Menu from 'components/shared/Menu'
import ActivityLogWindow from 'components/ActivityLogWindow'

import RootStore from 'stores/rootStore';
import OrdersStore from 'stores/ordersStore';
import * as variables from 'styles/variablesJS';


const LayoutWrapper = styled.div`
  display:flex;
  background-color: ${variables.layoutColor};
`;

/**
 * width: calc(100vw - 200px); (200px | 12.5rem is the width of the menu.)
 */
const LayoutBody = styled.div`
  position:relative;
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

interface ILayout {
  ordersStore?: OrdersStore;
  rootStore?: RootStore;
  route: any;
}

@inject('rootStore', 'ordersStore')
@observer
class Layout extends React.Component<ILayout> {
  render() {
    const { route, rootStore } = this.props;
    return (
      <LayoutWrapper>
        <Menu />
        <LayoutBody>
          {renderRoutes(route.routes)}
        {rootStore.showActivityLogWindow && (
          <ActivityLogWindow data={rootStore.activityLogWindowData} type={rootStore.activityLogWindowType} />
        )}
        </LayoutBody>
      </LayoutWrapper>
    )
  }
}

export default Layout;