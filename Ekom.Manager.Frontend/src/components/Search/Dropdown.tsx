import * as React from 'react';
import { observer } from 'mobx-react';
import classNames from 'classnames';
import styled from 'styled-components';
import * as variables from 'styles/variablesJS';
import { Checkbox } from 'components/Input';
import Icon from 'components/Icon';


const StyledDropdown = styled<any, "div">("div")`
  position: relative;
  background-color: ${(props: any) => props.isOpen ? variables.gray : variables.ekomSecondaryColor};
  min-width: ${(props: any) => props.minWidth && `${props.minWidth}px`};
`;

const StyledDropdownMenu = styled<any, "div">("div")`
  position: absolute;
  top:100%;
  min-width:100%;
  z-index:1000;
  -webkit-overflow-scrolling: touch;
  &.dropdown__menu-searchComponent {
    background-color: ${variables.gray};
    padding: 20px 0;
    border: 1px solid ${variables.black}16;
    border-top: none;
  }
`;

const DropdownControl = styled<any, "button">("button")`
  border:0;
  background-color: transparent;
  width:100%;

  color: ${variables.primaryColor};
  > svg {
    margin-left: 5px;
  }
  &.dropdown__control-searchComponent {
    color: ${(props: any) => props.isOpen ? variables.black : variables.primaryColor};
    padding: 25px 30px;
  }
`;


const DropdownOption = styled<any, "button">("button")`
  border:0;
  background-color: ${(props: any) => props.isActive ? variables.primaryColor : variables.transparent};
  color: ${(props: any) => props.isActive ? variables.white : variables.black};
  font-weight: ${(props: any) => props.isActive ? 600 : 400};
  display:flex;
  width:100%;
  &:hover {
    color: ${(props: any) => props.isActive ? variables.white : variables.primaryColor};
  }
  &.dropdown__option-searchComponent {
    padding: 5px 30px;
    &:not(:last-child) {
      /* margin-bottom: 10px; */
    }
  }
`;


interface IDropdownProps {
  disabled: boolean;
  options: IDropDownOption[];
  useCapture: boolean;
  display: 'block' | 'flex' | 'inline-block';
  type: 'default' | 'searchComponentDropdown';
  placeholder: string;
  defaultValue?: IDropDownOption;
  withCheckbox?: boolean;
  minWidth?: number;
  onChange?: any;
  onFocus?: any;
}

interface IDropDownOption {
  value: ( string | number);
  label: string;
}
interface IDropdownState {
  isOpen: boolean;
  selected?: IDropDownOption;
  multiSelected?: IDropDownOption[];
}

@observer
class Dropdown extends React.Component<IDropdownProps, IDropdownState> {
  static defaultProps: IDropdownProps = {
    disabled: false,
    useCapture: true,
    display: 'block',
    type: 'default',
    placeholder: 'Select...',
    options: [],
  }
  private menuRef: any;
  constructor(props: IDropdownProps) {
    super(props);
    this.menuRef = React.createRef();
    this.state = {
      isOpen: false
    }
  }
  componentDidMount() {
    if (!this.menuRef)
      this.menuRef = React.createRef();
    this.addEventListeners();
    if (this.props.defaultValue) {
      this.setState({
        selected: this.props.defaultValue
      })
    }

  }
  componentWillUnmount() {
    this.removeEventListeners()
  }
  addEventListeners() {
    document.addEventListener('click', (ev: any) => this.handleDocumentClick(ev), false)
    document.addEventListener('touchend', (ev: any) => this.handleDocumentClick(ev), false)
  }
  removeEventListeners() {
    document.removeEventListener('click', (ev: any) => this.handleDocumentClick(ev), false)
    document.removeEventListener('touchend', (ev: any) => this.handleDocumentClick(ev), false)
  }

  handleDocumentClick = (ev: any) => {
    if (this.menuRef && this.menuRef.contains(ev.target)) {
      return;
    }
    if (this.state.isOpen)
      this.setState({
        isOpen: false
      })
  }
  handleMouseDown = (event) => {
    if (this.props.onFocus && typeof this.props.onFocus === 'function') {
      this.props.onFocus(this.state.isOpen)
    }
    if (event.type === 'mousedown' && event.button !== 0) return
    event.stopPropagation()
    event.preventDefault()

    if (!this.props.disabled) {
      this.setState({
        isOpen: !this.state.isOpen
      })
    }
  }
  
  setValue (value, label) {
    let newState = {
      selected: {
        value,
        label},
      isOpen: false
    }
    this.fireChangeEvent(newState)
    this.setState(newState)
  }
  
  setChecked (value?, label?) {
    /** in working progress */
  }
  fireChangeEvent = (newState) => {
    if (newState.selected !== this.state.selected && this.props.onChange) {
      this.props.onChange(newState.selected)
    }
  }
  buildMenu = () => {
    return (
      <StyledDropdownMenu
        className={classNames({
          'dropdown__menu-searchComponent': this.props.type === 'searchComponentDropdown'
        })}
      >
        {this.props.options.map((option) => {
          return (
            this.renderOption(option)
          )
        })}
      </StyledDropdownMenu>
    )
  }

  renderOption (option) {
    let value = option.value
    if (typeof value === 'undefined') {
      value = option.label || option
    }
    let label = option.label || option.value || option

    if (this.props.withCheckbox) {
      return (
        <DropdownOption isActive={this.state.selected === option}>
          <Checkbox label={value} checked/>
        </DropdownOption>
      )
    }
    return (
      <DropdownOption
        isActive={this.state.selected && this.state.selected.value === value}
        key={value}
        className={classNames({
          'dropdown__option-searchComponent': this.props.type === 'searchComponentDropdown'
        })}
        onMouseDown={this.setValue.bind(this, value, label)}
        onClick={this.setValue.bind(this, value, label)}>
        {label}
      </DropdownOption>
    )
  }

  public render() {

    return (
      <StyledDropdown
        ref={(ref: any) => this.menuRef = ref}
        minWidth={this.props.minWidth}
        isOpen={this.state.isOpen}
      >
        <DropdownControl
          onClick={this.handleMouseDown}
          isOpen={this.state.isOpen}
          className={classNames({
            'fs-16': true,
            'lh-21': true,
            'semi-bold': true,
            'dropdown__control': true,
            'dropdown__control-searchComponent': this.props.type === 'searchComponentDropdown'
          })}
        >
          {this.state.selected ? this.state.selected.label : this.props.placeholder} 
          <Icon name="down-dir" iconSize={8} color={this.state.isOpen ? variables.black : variables.primaryColor} />
        </DropdownControl>
        {this.state.isOpen && this.buildMenu()}
      </StyledDropdown>
    )
  }
}

export default (Dropdown)