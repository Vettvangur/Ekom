import * as React from 'react';
import styled from 'styled-components';
import { inject, observer } from 'mobx-react';
import { RouterStore } from 'mobx-react-router';
import {
  Link,
  withRouter,
} from 'react-router-dom';
import classNames from 'classnames';


import Icon from 'components/Icon';
import { routes } from '../../../App';

import * as variables from 'styles/variablesJS';

// const StyledMenuItem: StyledFunction<IMenuActive | React.HTMLProps<HTMLDivElement>> =
//   styled.div;

//   const test = styled<IMenuActive, "div">("div")`
//   `;

const MenuWrapper = styled.div`
  width: 12.5rem;
  height: 100vh;
  padding-left: 30px;
  padding-top: 30px;
  padding-bottom: 30px;
`;

const MenuLogoWrapper = styled.div`
  margin-bottom: 80px;
`;

const MenuLogoLink = styled.a``;


const MenuLinks = styled.ul``;

const MenuItem = styled<IMenuActive, "div">("div")`
  color: ${variables.black};
  opacity: ${(props: any) => props.active ? 1 : .5};
  font-weight: 600;

  :not(:last-child) {
    margin-bottom: 10px;
  }
  
  &.menu-item-selected {
    a {
      display: flex;
      align-items: center;
      &::after {
        content: '';
        height: 0px;
        width:100%;
        border: 1px solid ${variables.red};
        margin-right: -20px;
        margin-left: 20px;
      }
    }
  }
  &:hover {
      opacity: ${(props: any) => !props.active && 1};
    a {
      color: ${variables.black};
      opacity: ${(props: any) => !props.active && 1};
    }
  }
`;

const MenuItemLink = styled(Link)`
  display:block;
  color: inherit;
  &::before {
      margin-right: 7px;
  }
`;

interface IMenuActive {
  active?: boolean;
}

interface IMenuProps {
  location: any;
  path: any;
  routing: RouterStore;
}
class State {
  selectedTopLevel = -1;
}

@inject('routing')
@observer
class Menu extends React.Component<IMenuProps, State> {
  public constructor(props: IMenuProps) {
    super(props);
    this.state = new State();
  }

  componentDidMount() {
    routes.map((mainRoute) => {
      mainRoute.routes.map((route, routeIndex) => {
        if (route.path === this.props.routing.location.pathname) {
          this.handleTopLevelChange(routeIndex);
        }
      })
    })
  }

  handleTopLevelChange = (routeIndex) => {
    this.setState((prevState) => ({
      selectedTopLevel: routeIndex
    }))
  }

  public render() {
    return (
      <MenuWrapper>
        <MenuLogoWrapper>
          <MenuLogoLink>
            <Icon name="vv-logo" iconSize={50} color={variables.red} />
          </MenuLogoLink>
        </MenuLogoWrapper>
        <MenuLinks>
          {routes.map((mainRoute, mainRouteIndex) => (
            <React.Fragment key={mainRouteIndex}>
              {mainRoute.routes.map((route, routeIndex) => route.showInMenu && (
                <MenuItem
                  key={routeIndex} 
                  className={classNames({
                    'fs-16': true,
                    'lh-21': true,
                    'menu-item-selected': this.state.selectedTopLevel === routeIndex
                  })} 
                  active={this.state.selectedTopLevel === routeIndex}
                >
                  <MenuItemLink to={route.path} onClick={() => this.handleTopLevelChange(routeIndex)} title={route.title}>
                    {route.title}
                  </MenuItemLink>
                </MenuItem>
              ))}
            </React.Fragment>
          ))}
        </MenuLinks>
      </MenuWrapper>
    );
  }
}



export default withRouter(Menu);
