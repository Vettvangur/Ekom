import * as React from 'react';

import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
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
  padding: 10px;
`;

interface IStoreFilterDropdownProps {
  searchStore?: SearchStore;
  destoryStoreFilterDropdown:() => void;
}


@inject('searchStore')
@observer
class StoreFilterDropdown extends React.Component<IStoreFilterDropdownProps> {
  private node: any;
  constructor(props: IStoreFilterDropdownProps) {
    super(props);
  }
  componentDidMount() {
    document.addEventListener('mousedown', this.handleClick, false);
  }
  componentWillUnmount() {
    document.removeEventListener('mousedown', this.handleClick, false);
  }
  handleClick = (e) => {
    if (this.node.contains(e.target)) {
      return;
    }
    this.props.destoryStoreFilterDropdown();
  }
  public render() {
    return (
      <StoreFilterDropdownWrapper ref={(ref: any) => this.node = ref}>
        <div
          style={{ cursor: 'pointer' }}
          onClick={() => this.props.searchStore.setStoreFilter()}
        >
          All stores
        </div>
        {this.props.searchStore.stores && this.props.searchStore.stores.map((store) => (
          <div style={{ cursor: 'pointer' }} key={store.Id}
            onClick={() => this.props.searchStore.setStoreFilter(store.Alias)}
          >
            {store.Alias}
          </div>
        ))}
      </StoreFilterDropdownWrapper>
    )
  }
}

export default StoreFilterDropdown;