import * as React from 'react';
import classNames from 'classnames';
import styled from 'styled-components';

import { observer, inject } from 'mobx-react';

import SearchStore from 'stores/searchStore';

import DateRangePickerWrapper from 'components/orders/DateRangePickerWrapper';

import * as variables from 'styles/variablesJS';
import Icon from 'components/Icon';

const StyledSearch = styled.div`
  display:flex;
  flex-wrap: wrap;
  height: 70px;
`;

const StyledSearchInputWrapper = styled.div`
  background-color: ${variables.ekomSecondaryColor};
  flex: 1 1 0;
  position: relative;
  min-width: 720px;
  &:not(:last-child) {
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

const StyledButtonFilterWrapper = styled.div`
  position: relative;
`;
const StyledButtonFilter = styled.button`
  height: 100%;
  color: ${variables.primaryColor};
  min-width: 9.375rem;
  background-color: ${variables.ekomSecondaryColor};
  border:0;
  padding: 0px 30px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  > svg {
    margin-left: 5px;
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
    }
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


const StoreFilterDropdownWrapper = styled.div`
  position: absolute;
  top: 70px;
  left:0;
  width:100%;
  background-color: ${variables.white};
  border: 1px solid black;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 10px;
`;

interface ISearchProps {
  searchStore?: SearchStore;
}

class State {
  showDatePicker = false;
  showStoreFilter = false;
  autoFocusEndDate = false;
}

@inject('searchStore')
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

  public handleSearch = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.keyCode === 13)
      this.props.searchStore.search()
  }
  public openStoreFilter = () => {
    this.setState(prevState => ({
      showStoreFilter: !prevState.showStoreFilter
    }))
  }
  public renderMonthFilter = () => {
    const { searchStore } = this.props;

    return (
      <div
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
      </div>
    )
  }
  public render() {
    return (
      <StyledSearch>
        <StyledSearchInputWrapper>
          <SearchInput
            className="fs-20 semi-bold"
            placeholder="Search orders..."
            onKeyDown={this.handleSearch}
          />
        </StyledSearchInputWrapper>
        <StyledButtonFilterWrapper>
          <StyledButtonFilter onClick={this.openStoreFilter} className="fs-16 semi-bold">
            {this.props.searchStore.storeFilter.length > 0 ? this.props.searchStore.storeFilter : 'All stores'}
            <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
          </StyledButtonFilter>
          {this.state.showStoreFilter && (
            <StoreFilterDropdownWrapper>
              <div
                onClick={() => this.props.searchStore.setStoreFilter()}
              >
                All stores
              </div>
              {this.props.searchStore.stores && this.props.searchStore.stores.map((store) => (
                <div key={store.Id}
                  onClick={() => this.props.searchStore.setStoreFilter(store.Alias)}
                >
                  {store.Alias}
                </div>
              ))}
            </StoreFilterDropdownWrapper>
          )}
        </StyledButtonFilterWrapper>
        {this.props.searchStore.preset && this.props.searchStore.preset.length > 0 ? (
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
            <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
          </StyledButtonFilter>
        )
          : (
            <>
              {this.props.searchStore.startDate && (
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
              )}
              {this.props.searchStore.endDate && (
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
              )}
            </>
          )
        }
        <StyledButtonFilter className="fs-16 semi-bold">
          Advanced Filters
          <Icon name="down-dir" iconSize={8} color={variables.primaryColor} />
        </StyledButtonFilter>
        {this.state.showDatePicker && (
          this.renderMonthFilter()
        )}
      </StyledSearch>
    )
  }
}

export default Search;