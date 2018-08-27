import * as React from 'react';
import * as moment from 'moment';

import { observer, inject } from 'mobx-react';

import OrdersStore from 'stores/ordersStore';
import DateRangePickerWrapper from 'components/orders/DateRangePickerWrapper';

import * as s from 'components/orders/searchForm/searchForm.scss';

interface IProps {
  ordersStore?: OrdersStore;
}

class State {
  showDatePicker = false;
}

@inject('ordersStore')
@observer
export default class SearchForm extends React.Component<IProps, State> {
  constructor(props) {
    super(props);

    this.state = new State();
    this.onDatesChange = this.onDatesChange.bind(this);
  }

  componentDidMount() {
  }

  onDatesChange(startDate, endDate, preset) {
  }

  handleSubmit() {

  }

  render() {
    const {
      ordersStore,
    } = this.props;

    const { preset, startDate, endDate, searchString } = this.props.ordersStore;

    const { showDatePicker } = this.state;


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
      start: moment().subtract(1, 'month'),
      end: today,
    },
    {
      text: 'Last month',
      start: moment().subtract(2, 'month'),
      end: moment().subtract(1, 'month'),
    },
    {
      text: 'Last Year',
      start: moment().subtract(1, 'year'),
      end: today,
    },
    {
      text: 'This year',
      start: moment().subtract(1, 'year'),
      end: today,
    }];

    console.log(ordersStore.startDate)
    return (
      <div className={s.parent}>
        <form
          className={s.form}
          onSubmit={this.handleSubmit}
        >
          <div className={s.selectDate}>
            <button
              type="button"
              className={s.datebtn}
              onClick={(e) => {
                e.preventDefault();
                if (showDatePicker) {
                  this.setState({ showDatePicker: false });
                } else {
                  this.setState({ showDatePicker: true });
                }
              }}
            >
              <span>
                {preset || (
                <span>
                  {startDate ? startDate.toISOString().split('T')[0] : ''}
                  {' '}
                  {endDate ? (
                    <span>
|
                      {' '}
                      {endDate.toISOString().split('T')[0]}
                    </span>
                  ) : ''}
                </span>
                )}
                {' '}
                <i className="icon-down-dir" />
              </span>
            </button>
            <DateRangePickerWrapper
              presets={presets}
              preset={preset}
              ordersStore={ordersStore}
              showDatePicker={showDatePicker}
              onDatesChange={this.onDatesChange}
              initialStartDate={ordersStore.startDate}
              initialEndDate={ordersStore.endDate}
              closeDatePicker={ordersStore.closeDatePicker}
            />
          </div>
          <div className={`input-group ${s.searchInput}`}>
            <input
              type="text"
              placeholder="Search orders"
              value={searchString}
              onChange={(e) => { ordersStore.handleSearchInput(e); }}
              onKeyDown={(e) => { ordersStore.onKeyPressed(e); }}
            />
            <i role="button" tabIndex={0} className="icon-magnifier" onKeyPress={() => {}} onClick={(e) => { ordersStore.search(); }} />
          </div>
        </form>
      </div>
    );
  }
}