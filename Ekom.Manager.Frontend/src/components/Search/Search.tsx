import * as React from 'react';
import * as moment from 'moment';
import styled from 'styled-components';

import { observer, inject } from 'mobx-react';

import OrdersStore from 'stores/ordersStore';

import DateRangePickerWrapper from 'components/orders/DateRangePickerWrapper';

import * as variables from 'styles/variablesJS';

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

const StyledInput = styled.input`
  padding: 22px 20px;
  background-color: inherit;
  border:0;
`;

const StyledButtonFilter = styled.button`
  background-color: ${variables.ekomSecondaryColor};
  border:0;
  padding: 0px 30px;
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
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

interface ISearchProps {
  ordersStore?: OrdersStore;
}

class State {
  showDatePicker = false;
}

@inject('ordersStore')
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
  public renderMonthFilter = () => {
    const { ordersStore } = this.props;
    
    const today = moment();
    const presets = [{
      text: 'Today',
      start: today,
      end: today,
    },
    {
      text: 'Last week',
      start: moment().subtract(1, 'week'),
      end: today,
    },
    {
      text: 'This month',
      start: moment().startOf('month'),
      end: today,
    },
    {
      text: 'Last month',
      start: moment().subtract(1, 'month').startOf('month'),
      end: moment().subtract(1, 'month').endOf('month'),
    },
    {
      text: 'Last Year',
      start: moment().subtract(1, 'year').startOf('year'),
      end:  moment().subtract(1, 'year').endOf('year'),
    },
    {
      text: 'This year',
      start: moment().startOf('year'),
      end: today,
    }];
    return (
      
      <DateRangePickerWrapper
        anchorDirection="right"
        presets={presets}
        preset={ordersStore.preset}
        ordersStore={ordersStore}
        showDatePicker={this.state.showDatePicker}
        initialStartDate={ordersStore.startDate}
        initialEndDate={ordersStore.endDate}
        closeDatePicker={ordersStore.closeDatePicker}
      />
    )
  }
  public render() {
    return (
      <StyledSearch>
        <StyledSearchInputWrapper>
          <StyledInput placeholder="Search orders..."/>
        </StyledSearchInputWrapper>
        <StyledButtonFilter>
          All stores
        </StyledButtonFilter>
        <StyledButtonFilter
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
          This Month
        </StyledButtonFilter>
        <StyledButtonFilter>
          Advanced Filters
        </StyledButtonFilter>
        {this.renderMonthFilter()}
      </StyledSearch>
    )
  }
}

export default Search;