import * as React from 'react';
import styled, { StyledFunction } from 'styled-components';
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

const styledMenuWrapper: StyledFunction<any | React.HTMLProps<HTMLDivElement>> =
  styled.div;
  
const styledMenuItem: StyledFunction<any | React.HTMLProps<HTMLDivElement>> =
  styled.div;

const StyledMenuWrapper = styledMenuWrapper`
  width: 12.5rem;
  height: 100vh;
  padding-left: 30px;
  padding-top: 30px;
  padding-bottom: 30px;
  @media screen and (max-width: 620px) {
    margin-top:60px;
    display: ${(props: any) => props.mobileMenuOpen ? 'block' : 'none'};
    width: 100vw;
    transform: translateX(-100vw);
    transition: transform .7s;
    transition-delay: transform 1s;
    &.open {
      transform: translateX(0);
    }
  }
`;

const MenuLogoWrapper = styled.div`
  margin-bottom: 80px;
`;

const MenuLogoLink = styled.a``;


const MenuLinks = styled.ul``;

const MenuItem = styledMenuItem`
  color: ${variables.black};
  opacity: ${(props: any) => props.active ? 1 : .5};
  font-weight: 600;

  :not(:last-child) {
    margin-bottom: 10px;
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
  &.menu-item-selected {
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
  &::before {
      margin-right: 7px;
  }
`;

const UmbracoItemLink = styled.a`
  display:block;
  color: inherit;
  &.menu-item-selected {
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
  &::before {
      margin-right: 7px;
  }
`;


const MobileMenuHeader = styled.div`
  display:none;
  position:fixed;
  top:0;
  left:0;
  width:100vw;
  z-index:1;
  @media screen and (max-width: 620px) {
    display: block;
    height:60px;
  }

`;

const FirstLevelMenu = styled.div``;
// const SubMenu = styled.ul`
//   margin-left:10px;
// `;

// interface IMenuActive {
//   active?: boolean;
//   mobileMenuOpen?: boolean;
// }

interface IMenuProps {
  location: any;
  path: any;
  routing: RouterStore;
}
class State {
  selectedTopLevel = -1;
  selectedSecondLevel = -1;
  mobileMenuOpen = false;
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
        // if (route.routes)
        //   route.routes.map((subRoute, subIndex) => {
        //     if (subRoute.path === this.props.routing.location.pathname) {
        //       this.handleSecondLevelChange(subIndex);
        //     }
        //   });
      })
    })
  }
  openMobileMenu = () => {
    this.setState(prevState => ({
      mobileMenuOpen: !prevState.mobileMenuOpen
    }))
  }

  handleTopLevelChange = (routeIndex) => {
    this.setState((prevState) => ({
      selectedSecondLevel: -1,
      selectedTopLevel: routeIndex
    }))
  }

  handleSecondLevelChange = (routeIndex) => {
    this.setState((prevState) => ({
      selectedTopLevel: -1,
      selectedSecondLevel: routeIndex
    }))
  }

  public render() {
    return (
      <>
        <MobileMenuHeader>
          <button className={classNames({ 'hamburger': true, 'hamburger--collapse': true, 'hamburger--collapse is-active': this.state.mobileMenuOpen })} type="button" onClick={this.openMobileMenu}>
            <span className="hamburger-box">
              <span className="hamburger-inner"></span>
            </span>
          </button>
        </MobileMenuHeader>
        <StyledMenuWrapper
          className={classNames({
            'open': this.state.mobileMenuOpen
          })}
          mobileMenuOpen={this.state.mobileMenuOpen}
        >
          <MenuLogoWrapper>
            <MenuLogoLink>
              <Icon name="vv-logo" iconSize={50} color={variables.red} />
            </MenuLogoLink>
          </MenuLogoWrapper>
          <MenuLinks>
            {routes.map((mainRoute, mainRouteIndex) => (
              <React.Fragment key={mainRouteIndex}>
                {mainRoute.routes.map((route, routeIndex) => route.showInMenu && (
                  <FirstLevelMenu key={routeIndex}>
                    <MenuItem
                      key={routeIndex}
                      active={this.state.selectedTopLevel === routeIndex}
                    >
                      <MenuItemLink
                        className={classNames({
                          'fs-16': true,
                          'lh-21': true,
                          'menu-item-selected': this.state.selectedTopLevel === routeIndex
                        })}
                        to={route.path} onClick={() => this.handleTopLevelChange(routeIndex)} title={route.title}>
                        {route.title}
                      </MenuItemLink>
                    </MenuItem>
                    {/* <SubMenu>
                      {route.routes && route.routes.map((subRoute, subIndex) => subRoute.showInMenu && (
                        <MenuItem
                          key={subIndex}
                          active={this.state.selectedSecondLevel === subIndex}
                        >
                          <MenuItemLink
                            className={classNames({
                              'fs-16': true,
                              'lh-21': true,
                              'menu-item-selected': this.state.selectedSecondLevel === subIndex
                            })}
                            to={subRoute.path} onClick={() => this.handleSecondLevelChange(subIndex)} title={subRoute.title}>
                            {subRoute.title}
                          </MenuItemLink>
                        </MenuItem>
                      ))}
                    </SubMenu> */}
                  </FirstLevelMenu>
                ))}
              </React.Fragment>
            ))}
            <br/><br/><br/>
            <UmbracoItemLink href="/umbraco">Back to Umbraco</UmbracoItemLink>
          </MenuLinks>
        </StyledMenuWrapper>
      </>
    );
  }
}



export default withRouter(Menu);
