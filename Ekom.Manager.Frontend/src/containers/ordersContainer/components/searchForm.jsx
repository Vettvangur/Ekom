import React, { Component } from 'react';
import moment from 'moment';
import omit from 'lodash/omit';

import DateRangePickerWrapper from './DateRangePickerWrapper';

import s from './searchForm.scss';

const defaultProps = {
  stateDateWrapper: date => date,
}
class SearchForm extends Component {

  constructor(props) {
    super(props);

    this.state = {
      startDate: null,
      endDate: null,
      customPick: 0,
      showDatePicker: false,
      preset: "",
    }
    this.onDatesChange = this.onDatesChange.bind(this);
    this.closeDatePicker = this.closeDatePicker.bind(this);
  }
  componentDidMount() {
    const today = moment();
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth()).toISOString().split('T')[0];
    const end = now.toISOString().split('T')[0];
    this.props.fetchOrders(today, today);
    this.setState({
      startDate: today,
      endDate: today,
      preset: "Today"
    });
  }
  closeDatePicker() {
    this.setState({
      showDatePicker: false
    })
    
    if (this.state.startDate !== null && this.state.endDate !== null && this.state.preset === null) {
      this.props.fetchOrders(this.state.startDate, this.state.endDate);
    }
  }
  onDatesChange(startDate, endDate, preset) {
    const { stateDateWrapper, fetchOrders } = this.props;
    this.setState({
      startDate: startDate,
      endDate: endDate,
      preset
    });
    
    if (startDate !== null && endDate !== null && startDate <= endDate) {
      this.props.fetchOrders(moment(startDate).format("YYYY-MM-DD"), moment(endDate).format("YYYY-MM-DD"));
    }
  }
  render() {
    const {showDatePicker, preset, startDate, endDate} = this.state;
    const today = moment();
    const tomorrow = moment().add(1, 'day');
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
    const props = omit(this.props, [
      'stateDateWrapper',
    ]);
    return (
      <div className={s.parent}>
        <form className={s.form} onSubmit={this.props.handleSubmit}>
          <div className={s.selectDate}>
            <button
              className={s.datebtn}
              onClick={(e) => {
                e.preventDefault();
                if(this.state.showDatePicker) {
                  this.setState({showDatePicker: false})
                } else {
                  this.setState({showDatePicker: true})
                }
              }}>
              <span>{preset ? preset : <span>{startDate ? startDate.toISOString().split('T')[0] : ""} {endDate ? <span>| {endDate.toISOString().split('T')[0]}</span> : ""}</span>} <i  className="icon-down-dir" /></span>
            </button>
            <DateRangePickerWrapper presets={presets} preset={preset} showDatePicker={showDatePicker} initialStartDate={startDate} initialEndDate={endDate} onDatesChange={this.onDatesChange} closeDatePicker={this.closeDatePicker} />
          </div>
          <div className={"input-group " + s.searchInput}>
            <input 
              type="text" 
              placeholder="Search orders"
            />
            <i className="icon-magnifier"></i>
          </div>
        </form>   
      </div>
    );
  }
}

export default SearchForm;