import * as React from 'react';
import styled from 'styled-components';
import { inject, observer } from 'mobx-react';
import { RouterStore } from 'mobx-react-router';
import {
  Link,
  withRouter,
} from 'react-router-dom';

import { routes } from '../../../App';

import * as variables from 'styles/variablesJS';



const MenuWrapper = styled.div`
  width: 12.5rem;
  height: 100vh;
`;

// const MenuLogo = styled.a``;

const MenuLinks = styled.ul``;

const MenuItem = styled<{ active: boolean }, "li">("li")`
  background-color: ${(props: any) => props.active && variables.secondaryColor};
  color: ${variables.black};
  opacity: ${(props: any) => props.active ? 1 : .5};
  font-weight: 600;
  font-size: 1rem;
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
  padding: 12px 20px;
  &::before {
      margin-right: 7px;
  }
`;

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
        <MenuLinks>
          {routes.map((mainRoute, mainRouteIndex) => (
            <React.Fragment key={mainRouteIndex}>
              {mainRoute.routes.map((route, routeIndex) => route.showInMenu && (
                <MenuItem key={routeIndex} active={this.state.selectedTopLevel === routeIndex}>
                  <MenuItemLink to={route.path} onClick={() => this.handleTopLevelChange(routeIndex)} className="icon-home3" title={route.title}>
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
