import * as React from 'react';

import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import RootStore from 'stores/rootStore';
import SearchStore from 'stores/searchStore';
import * as variables from 'styles/variablesJS';


const StoreFilterDropdownWrapper = styled.div`
  position: absolute;
  top: 70px;
  left:0;
  width:100%;
  background-color: ${variables.gray};
  border: 1px solid ${variables.black}16;
  border-top: none;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
`;

const StoreItem = styled.div`
  padding: 5px 10px;
  width:100%;
`;

interface IStoreFilterDropdownProps {
  rootStore?: RootStore;
  searchStore?: SearchStore;
  destoryStoreFilterDropdown:() => void;
}


@inject('rootStore', 'searchStore')
@observer
class StoreFilterDropdown extends React.Component<IStoreFilterDropdownProps> {
  private node: any;
  constructor(props: IStoreFilterDropdownProps) {
    super(props);
  }
  componentDidMount() {
    document.addEventListener('mousedown', this.handleClickOutside, false);
  }
  componentWillUnmount() {
    document.removeEventListener('mousedown', this.handleClickOutside, false);
  }
  handleClickOutside = (e) => {
    if (this.node.contains(e.target)) {
      return;
    }
    this.props.destoryStoreFilterDropdown();
  }
  handleClick = (Alias?: string) => {
    this.props.searchStore.setStoreFilter(Alias)
    this.props.destoryStoreFilterDropdown();
  }
  public render() {
    return (
      <StoreFilterDropdownWrapper ref={(ref: any) => this.node = ref}>
        <StoreItem
          style={{ cursor: 'pointer' }}
          onClick={() => this.handleClick()}
        >
          All stores
        </StoreItem>
        {this.props.rootStore.stores && this.props.rootStore.stores.map((store) => (
          <StoreItem style={{ cursor: 'pointer' }} key={store.Id}
            onClick={() => this.handleClick(store.Alias)}
          >
            {store.Alias}
          </StoreItem>
        ))}
      </StoreFilterDropdownWrapper>
    )
  }
}

export default StoreFilterDropdown;