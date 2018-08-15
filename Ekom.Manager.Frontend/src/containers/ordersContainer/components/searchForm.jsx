import React, { Component } from 'react';
import PropTypes from 'prop-types';
import moment from 'moment';

import DateRangePickerWrapper from './DateRangePickerWrapper';

import s from './searchForm.scss';

class SearchForm extends Component {
  constructor(props) {
    super(props);

    this.state = {
      startDate: null,
      endDate: null,
      showDatePicker: false,
      searchString: '',
      preset: '',
    };
    this.onDatesChange = this.onDatesChange.bind(this);
    this.closeDatePicker = this.closeDatePicker.bind(this);

    this.handleSearch = this.handleSearch.bind(this);
    this.handleSearchInput = this.handleSearchInput.bind(this);
    this.onKeyPressed = this.onKeyPressed.bind(this);
  }

  componentDidMount() {
    const { fetchOrders } = this.props;
    const today = moment();
    const lastWeek = moment().subtract(1, 'week');
    fetchOrders(lastWeek, today);
    this.setState({
      startDate: lastWeek,
      endDate: today,
      preset: 'Last week',
    });
  }

  onDatesChange(startDate, endDate, preset) {
    const { fetchOrders } = this.props;
    this.setState({
      startDate,
      endDate,
      preset,
    });

    if (startDate !== null && endDate !== null && startDate <= endDate) {
      fetchOrders(moment(startDate).format('YYYY-MM-DD'), moment(endDate).format('YYYY-MM-DD'));
    }
  }

  onKeyPressed(e) {
    const {
      startDate,
      endDate,
      searchString,
    } = this.state;

    const {
      searchOrders,
    } = this.props;

    if (e.keyCode === 13) {
      searchOrders(startDate, endDate, searchString);
    }
  }

  handleSearch() {
    const {
      startDate,
      endDate,
      searchString,
    } = this.state;

    const {
      searchOrders,
    } = this.props;

    searchOrders(startDate, endDate, searchString);
  }

  closeDatePicker() {
    const {
      startDate,
      endDate,
      preset,
    } = this.state;
    const { fetchOrders } = this.props;
    this.setState({
      showDatePicker: false,
    });

    if (startDate !== null && endDate !== null && preset === null) {
      fetchOrders(startDate, endDate);
    }
  }

  handleSearchInput(e) {
    this.setState({
      searchString: e.target.value,
    });
  }

  render() {
    const {
      showDatePicker,
      preset,
      startDate,
      endDate,
      searchString,
    } = this.state;
    const {
      handleSubmit,
    } = this.props;

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
    return (
      <div className={s.parent}>
        <form
          className={s.form}
          onSubmit={handleSubmit}
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
              showDatePicker={showDatePicker}
              initialStartDate={startDate}
              initialEndDate={endDate}
              onDatesChange={this.onDatesChange}
              closeDatePicker={this.closeDatePicker}
            />
          </div>
          <div className={`input-group ${s.searchInput}`}>
            <input
              type="text"
              placeholder="Search orders"
              value={searchString}
              onChange={(e) => { this.handleSearchInput(e); }}
              onKeyDown={(e) => { this.onKeyPressed(e); }}
            />
            <i role="button" tabIndex={0} type="button" className="icon-magnifier" onKeyPress={() => {}} onClick={(e) => { this.handleSearch(e); }} />
          </div>
        </form>
      </div>
    );
  }
}


SearchForm.defaultProps = {
  fetchOrders: null,
  handleSubmit: null,
  searchOrders: null,
};

SearchForm.propTypes = {
  fetchOrders: PropTypes.func,
  searchOrders: PropTypes.func,
  handleSubmit: PropTypes.func,
};


export default SearchForm;
