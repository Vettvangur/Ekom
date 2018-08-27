import 'react-dates/initialize';
import React from 'react';
import PropTypes from 'prop-types';
import momentPropTypes from 'react-moment-proptypes';
import moment from 'moment';
import omit from 'lodash/omit';

import { withRouter } from 'react-router'
import { observer, inject } from 'mobx-react';

import { DateRangePicker } from 'react-dates';

import DateRangePickerPhrases from 'react-dates/lib/defaultPhrases';
import DateRangePickerShape from 'react-dates/lib/shapes/DateRangePickerShape';
import {
  START_DATE, END_DATE, HORIZONTAL_ORIENTATION, ANCHOR_LEFT,
} from 'react-dates/constants';
import isSameDay from 'react-dates/lib/utils/isSameDay';

import OrdersStore from 'stores/ordersStore';

const CustomNavBack = () => <i className="icon-fa-solid-900-1 DayPickerNavigation_leftButton__horizontalDefault" />;
const CustomNavNext = () => <i className="icon-fa-solid-900 DayPickerNavigation_rightButton__horizontalDefault" />;


const propTypes = {
  // example props for the demo
  autoFocus: PropTypes.bool,
  autoFocusEndDate: PropTypes.bool,
  stateDateWrapper: PropTypes.func,
  initialStartDate: momentPropTypes.momentObj,
  initialEndDate: momentPropTypes.momentObj,
  presets: PropTypes.arrayOf(PropTypes.shape({
    text: PropTypes.string,
    start: momentPropTypes.momentObj,
    end: momentPropTypes.momentObj,
  })),

};

const defaultProps = {
  // example props for the demo
  autoFocus: true,
  autoFocusEndDate: false,
  initialStartDate: null,
  initialEndDate: null,
  presets: [],

  // input related props
  startDateId: START_DATE,
  startDatePlaceholderText: 'Start Date',
  endDateId: END_DATE,
  endDatePlaceholderText: 'End Date',
  disabled: false,
  required: false,
  screenReaderInputMessage: '',
  showClearDates: false,
  showDefaultInputIcon: false,
  customInputIcon: null,
  customArrowIcon: null,
  customCloseIcon: null,
  block: false,
  small: false,
  regular: false,

  // calendar presentation and interaction related props
  renderMonthText: null,
  orientation: HORIZONTAL_ORIENTATION,
  anchorDirection: ANCHOR_LEFT,
  horizontalMargin: 0,
  withPortal: false,
  withFullScreenPortal: false,
  initialVisibleMonth: null,
  numberOfMonths: 2,
  keepOpenOnDateSelect: false,
  reopenPickerOnClearDates: true,
  isRTL: false,

  // navigation related props
  navPrev: <CustomNavBack />,
  navNext: <CustomNavNext />,
  onPrevMonthClick() {},
  onNextMonthClick() {},
  onClose() {},

  // day presentation and interaction related props
  renderCalendarDay: undefined,
  renderDayContents: null,
  minimumNights: 0,
  enableOutsideDays: false,
  isDayBlocked: () => false,
  isOutsideRange: day => false,
  isDayHighlighted: () => false,

  // internationalization
  displayFormat: () => moment.localeData().longDateFormat('L'),
  monthFormat: 'MMMM YYYY',
  phrases: DateRangePickerPhrases,

  stateDateWrapper: date => date,
  onDatesChange() {},
};

export default class DateRangePickerWrapper extends React.Component {
  constructor(props) {
    super(props);

    const focusedInput = null;
    

    this.state = {
      focusedInput,
      startDate: props.initialStartDate,
      endDate: props.initialEndDate,
    };

    this.onDatesChange = this.onDatesChange.bind(this);
    this.onFocusChange = this.onFocusChange.bind(this);
    this.renderDatePresets = this.renderDatePresets.bind(this);
  }

  componentDidUpdate(prevProps) {
    const { showDatePicker } = this.props;
    if (showDatePicker !== prevProps.showDatePicker) {
      if (showDatePicker) {
        this.setState({ focusedInput: START_DATE });
      }
    }
  }

  onDatesChange({ startDate, endDate, preset }) {
    const { onDatesChange,ordersStore } = this.props;
    ordersStore.setDates(startDate, endDate).then(() => {
      if (startDate !== null && endDate !== null && startDate <= endDate) {
      ordersStore.getOrders();
      }
    });
    ordersStore.setPreset(preset)
  }

  onFocusChange(focusedInput) {
    this.setState({ focusedInput });
  }

  renderDatePresets() {
    const { presets, preset } = this.props;

    return (
      <div className="PresetDateRangePicker_panel">
        {presets.map(({ text, start, end }) => {
          return (
            <button
              key={text}
              className={`PresetDateRangePicker_button__selected ${text === preset ? 'PresetDateRangePicker_buttonisSelected' : ''}`}
              type="button"
              onClick={() => {
                this.onDatesChange({ startDate: start, endDate: end, preset: text });
              }}
            >
              {text}
            </button>
          );
        })}
      </div>
    );
  }

  render() {
    const { focusedInput } = this.state;
    const {
      initialStartDate,
      initialEndDate,
      closeDatePicker,
    } = this.props;
    // autoFocus, autoFocusEndDate, initialStartDate and initialEndDate are helper props for the
    // example wrapper but are not props on the SingleDatePicker itself and
    // thus, have to be omitted.
    const props = omit(this.props, [
      'autoFocus',
      'autoFocusEndDate',
      'initialStartDate',
      'initialEndDate',
      'stateDateWrapper',
      'presets',
      'ordersStore',
      'closeDatePicker',
      'showDatePicker',
      'preset',
    ]);

    return (
      <DateRangePicker
        {...props}
        renderCalendarInfo={this.renderDatePresets}
        onDatesChange={this.onDatesChange}
        onFocusChange={this.onFocusChange}
        onClose={closeDatePicker}
        focusedInput={focusedInput}
        startDate={initialStartDate}
        endDate={initialEndDate}
        hideKeyboardShortcutsPanel
        noBorder
        daySize={40}

      />
    );
  }
}

DateRangePickerWrapper.propTypes = propTypes;
DateRangePickerWrapper.defaultProps = defaultProps;
