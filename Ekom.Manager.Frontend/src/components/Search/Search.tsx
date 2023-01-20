import * as React from 'react';
import classNames from 'classnames';
import styled, { StyledFunction } from 'styled-components';

import { observer, inject } from 'mobx-react';

import SearchStore from 'stores/searchStore';
import RootStore from 'stores/rootStore';

import DateRangePickerWrapper from 'components/orders/DateRangePickerWrapper';

import Dropdown from './Dropdown';


import * as variables from 'styles/variablesJS';
import Icon from 'components/Icon';


const styledButtonFilterWrapper: StyledFunction<any | React.HTMLProps<HTMLDivElement>> =
  styled.div;

const StyledSearch = styled.div`
  display:flex;
  flex-wrap: wrap;
`;

const StyledSearchInputWrapper = styled.div`
  background-color: ${variables.ekomSecondaryColor};
  flex: 1 1 0;
  position: relative;
  display:flex;
  min-width: 250px;
  @media screen and (max-width: 860px) {
    min-width:100%;
    
  }
  @media screen and (min-width: 750px) {
    &::after {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      right:0;
      position: absolute;
      opacity: .07;
    }
  }
`;

const SearchInput = styled.input`
  padding: 22px 20px;
  background-color: inherit;
  border:0;
  color: ${variables.black};
`;

const StyledButtonFilterWrapper = styledButtonFilterWrapper`
  position: relative;
  background-color: ${(props: any) => props.active ? variables.gray : variables.ekomSecondaryColor};
  @media screen and (max-width: 1000px) {
    &:last-child {
      flex:auto;
    }
  }
  @media screen and (max-width: 860px) {
    flex:auto;
  }
  &:not(:last-child) {
    &::after {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      right:0;
      position: absolute;
      opacity: .07;
      z-index:1;
    }
  }
  &.active {
    > button {
      outline: 0;
      color: ${variables.black};
    }
    &::after {
      content: null;
    }
  }
`;
const StyledButtonFilter = styled.button`
  height: 100%;
  color: ${variables.primaryColor};
  background-color: inherit;
  border:0;
  padding: 0px 30px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index:1;
  &:focus {
    outline:none;
  }
  > svg {
    margin-left: 5px;
  }
`

const StoreFilterDropDownWrapper = styled.div`
  position: absolute;
  top:70px;
  left:0;
  width: 100%;
  background-color: ${variables.secondaryColor}80;
    /* &::before {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      left:-5px;
      position: absolute;
      opacity: .07;
    }
    &::after {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      right:0;
      position: absolute;
      opacity: .07;
    } */
`;

const StyledSearchIconButton = styled.button`
  background-color: ${variables.ekomSecondaryColor};
  outline: 0;
  border: 0;
  margin-right: 32px;
  height:100%;
`;


const DatePicker = styled.div`
  position:absolute;
  right:30px;
`;

// interface IActive {
//   active?: boolean;
// }
interface ISearchProps {
  searchStore?: SearchStore;
  rootStore?: RootStore;
  customers?: boolean;
}

class State {
  showDatePicker = false;
  showStoreFilter = false;
  autoFocusEndDate = false;
}

@inject('searchStore', 'rootStore')
@observer
class Search extends React.Component<ISearchProps, State> {
  constructor(props: ISearchProps) {
    super(props);
    this.state = new State();
  }

  public renderStoreFilter = () => {
    return (
      <StoreFilterDropDownWrapper>sad</StoreFilterDropDownWrapper>
    )
  }
  public destroyDatePicker = () => {
    this.setState({
      autoFocusEndDate: false,
      showDatePicker: false,
    });
  }
  public destoryStoreFilterDropdown = () => {
    this.setState({
      showStoreFilter: false
    })
  }

  public handleSearch = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.keyCode === 13)
      this.performSearch()
  }
  public performSearch = () => {
    this.props.searchStore.search()
  }
  public openStoreFilter = (e) => {
    e.preventDefault();
    this.setState(prevState => ({
      showStoreFilter: !prevState.showStoreFilter
    }))
  }
  public renderMonthFilter = () => {
    const { searchStore } = this.props;

    return (
      <DatePicker
        className={classNames({
          'singleDateInputButton': (this.state.showDatePicker && searchStore.endDate === null) || this.state.showDatePicker && searchStore.preset && searchStore.preset.length > 0
        })}
      >
        <DateRangePickerWrapper
          anchorDirection="right"
          presets={searchStore.presets}
          preset={searchStore.preset}
          autoFocusEndDate={this.state.autoFocusEndDate}
          showDatePicker={this.state.showDatePicker}
          initialStartDate={searchStore.startDate}
          initialEndDate={searchStore.endDate}
          closeDatePicker={this.destroyDatePicker}
        />
      </DatePicker>
    )
  }
  public render() {
    return (
      <StyledSearch>
        <StyledSearchInputWrapper>
          <SearchInput
            className="fs-20 semi-bold"
            placeholder={this.props.customers ? "Search customers..." : "Search orders..."}
            onKeyDown={this.handleSearch}
            onChange={this.props.searchStore.setSearchString}
          />
          <StyledSearchIconButton className="fs-16 semi-bold" onClick={this.performSearch}>
            <Icon name="search" iconSize={20} color={variables.primaryColor} />
          </StyledSearchIconButton>
        </StyledSearchInputWrapper>
        <StyledButtonFilterWrapper>
          <Dropdown
            minWidth={145}
            placeholder={this.props.searchStore.StatusFilter.label}
            type="searchComponentDropdown"
            defaultValue={this.props.searchStore.StatusFilter}
            onChange={this.props.searchStore.setStatusFilter}
            options={this.props.rootStore.statusList}
          />
        </StyledButtonFilterWrapper>
        <StyledButtonFilterWrapper>
          <Dropdown
            minWidth={145}
            placeholder={this.props.searchStore.storeFilter.label}
            type="searchComponentDropdown"
            defaultValue={this.props.searchStore.storeFilter}

            onChange={this.props.searchStore.setStoreFilter}
            options={this.props.rootStore.stores}
          />
        </StyledButtonFilterWrapper>
        {/* <StyledButtonFilterWrapper
          className={classNames({
            'active': this.state.showStoreFilter
          })}
          active={this.state.showStoreFilter}
        >
          <StyledButtonFilter onClick={this.openStoreFilter} className="fs-16 semi-bold">
            {this.props.searchStore.storeFilter.length > 0 ? this.props.searchStore.storeFilter : 'All stores'}
            <Icon name="down-dir" iconSize={8} color={this.state.showStoreFilter ? variables.black : variables.primaryColor} />
          </StyledButtonFilter>
          {this.state.showStoreFilter && (
            <StoreFilterDropdown destoryStoreFilterDropdown={this.destoryStoreFilterDropdown} />
          )}
        </StyledButtonFilterWrapper> */}
        {this.props.searchStore.preset && this.props.searchStore.preset.length > 0 ? (
          <StyledButtonFilterWrapper>
            <StyledButtonFilter className="fs-16 semi-bold"
              type="button"
              onClick={(e) => {
                e.preventDefault();
                if (this.state.showDatePicker) {
                  this.setState({ showDatePicker: false });
                } else {
                  this.setState({ showDatePicker: true });
                }
              }}
            >
              {this.props.searchStore.preset}
              <Icon name="down-dir" iconSize={8} color={this.state.showDatePicker ? variables.black : variables.primaryColor} />
            </StyledButtonFilter>
          </StyledButtonFilterWrapper>
        )
          : (
            <>
              {this.props.searchStore.startDate && (
                <StyledButtonFilterWrapper>
                  <StyledButtonFilter className="fs-16 semi-bold"
                    type="button"
                    onClick={(e) => {
                      e.preventDefault();
                      if (this.state.showDatePicker) {
                        this.destroyDatePicker();
                      } else {
                        this.setState({ showDatePicker: true });
                      }
                    }}
                  >
                    {this.props.searchStore.startDate.format("DD-MM-YYYY").toString()}
                    <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
                  </StyledButtonFilter>
                </StyledButtonFilterWrapper>
              )}
              {this.props.searchStore.endDate && (
                <StyledButtonFilterWrapper>
                  <StyledButtonFilter className="fs-16 semi-bold"
                    type="button"
                    onClick={(e) => {
                      e.preventDefault();
                      if (this.state.showDatePicker) {
                        this.destroyDatePicker();
                      } else {
                        this.setState({ showDatePicker: true, autoFocusEndDate: true });
                      }
                    }}
                  >
                    {this.props.searchStore.endDate.format("DD-MM-YYYY").toString()}
                    <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
                  </StyledButtonFilter>
                </StyledButtonFilterWrapper>
              )}
            </>
          )
        }
        <StyledButtonFilterWrapper>
          <StyledButtonFilter className="fs-16 semi-bold">
            Advanced Filters
            <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
          </StyledButtonFilter>
        </StyledButtonFilterWrapper>
        {this.state.showDatePicker && (
          this.renderMonthFilter()
        )}
      </StyledSearch>
    )
  }
}

export default Search;