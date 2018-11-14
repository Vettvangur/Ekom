import 'react-dates/initialize';
import * as React from 'react';
//import momentPropTypes from 'react-moment-proptypes';
import * as moment from 'moment';
import * as omit from 'lodash/omit';

import { observer, inject } from 'mobx-react';

import { DateRangePicker } from 'react-dates';

import DateRangePickerPhrases from 'react-dates/lib/defaultPhrases';
import {
  START_DATE, END_DATE, HORIZONTAL_ORIENTATION, ANCHOR_LEFT,
} from 'react-dates/constants';

import SearchStore from 'stores/searchStore';
import TableStore from 'stores/tableStore';

export function CustomNavBack() {
  return (
    <i className="icon-fa-solid-900-1 DayPickerNavigation_leftButton__horizontalDefault" />
  )
}
const CustomNavNext = () => <i className="icon-fa-solid-900 DayPickerNavigation_rightButton__horizontalDefault" />;

type IProps = {
  showDatePicker: boolean
  searchStore?: SearchStore
  tableStore?: TableStore
  presets: any
  preset: string
  initialStartDate: any
  initialEndDate: any
  closeDatePicker: any
} & Partial<DefaultProps>
type DefaultProps = Readonly<typeof defaultProps>

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
  keepOpenOnDateSelect: true,
  reopenPickerOnClearDates: true,
  isRTL: false,

  // navigation related props
  onPrevMonthClick() { },
  onNextMonthClick() { },
  onClose() { },

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
  onDatesChange() { },
};

class State {
  focusedInput: any;
  startDate: any;
  endDate: any;
}

@inject('searchStore', 'tableStore')
@observer
class DateRangePickerWrapper extends React.Component<IProps, State> {
  static defaultProps = defaultProps
  constructor(props: IProps) {
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
  componentDidMount() {
    if (this.props.autoFocusEndDate) {
      this.setState({
        focusedInput: END_DATE
      })
    } else {
      this.setState({ focusedInput: START_DATE });
    }
  }
  onDatesChange({ startDate, endDate, preset }) {
    const { searchStore } = this.props;
    searchStore.setDates(startDate, endDate)
    /*
    ordersStore.setDates(startDate, endDate).then(() => {
      if (startDate !== null && endDate !== null && startDate <= endDate) {
        ordersStore.getOrders();
      }
    });
    */
   searchStore.setPreset(preset)
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
    
    const props = omit(this.props, [
      'autoFocus',
      'autoFocusEndDate',
      'initialStartDate',
      'initialEndDate',
      'className',
      'searchStore',
      'tableStore',
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
        navPrev={<CustomNavBack />}
        navNext={<CustomNavNext />}
        startDate={initialStartDate}
        endDate={initialEndDate}
        keepOpenOnDateSelect={true}
        hideKeyboardShortcutsPanel
        noBorder
        daySize={40}

      />
    );
  }
}



export default DateRangePickerWrapper;