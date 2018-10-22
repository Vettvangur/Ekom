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
  query: string = "";
}

@inject('ordersStore')
@observer
export default class SearchForm extends React.Component<IProps, State> {
  constructor(props) {
    super(props);

    this.state = new State();
  }

  onKeyPressed = (e) =>  {
    if (e.keyCode === 13) {
      this.handleSubmit(e);
    }
  }

  handleInput = (e) => {
    this.setState({
      query: e.target.value,
    });
  }

  handleSubmit = (e) => {
    e.preventDefault();
    const {
      ordersStore,
    } = this.props;

    const {
      query,
    } = this.state;

    ordersStore.search(query)
  }

  render() {
    const {
      ordersStore,
    } = this.props;

    const { preset, startDate, endDate } = this.props.ordersStore;

    const {
      showDatePicker,
      query,
    } = this.state;


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
      <div className={s.parent}>
        <form
          className={s.form}
          onSubmit={(e) => {this.handleSubmit(e)}}
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
              initialStartDate={ordersStore.startDate}
              initialEndDate={ordersStore.endDate}
              closeDatePicker={ordersStore.closeDatePicker}
            />
          </div>
          <div className={`input-group ${s.searchInput}`}>
            <input
              type="text"
              placeholder="Search orders"
              value={query}
              onChange={(e) => { this.handleInput(e); }}
              onKeyDown={(e) => { this.onKeyPressed(e); }}
            />
            <i role="button" tabIndex={0} className="icon-magnifier" onKeyPress={() => {}} onClick={(e) => { this.handleSubmit(e); }} />
          </div>
        </form>
      </div>
    );
  }
}