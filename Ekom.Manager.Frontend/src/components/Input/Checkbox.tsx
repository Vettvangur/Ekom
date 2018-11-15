import * as React from 'react';

import styled from 'styled-components';

//import * as variables from 'styles/variablesJS';

const CheckboxWrapper = styled.div``;

const CheckboxLabel = styled.label`
`;

const CheckboxInput = styled.input`
  
  -webkit-box-sizing: border-box;
  box-sizing: border-box;
  padding: 0;
  &:not(:checked), &:checked {
    position: absolute;
    opacity: 0;
    pointer-events: none;
    width: auto;
  }
  +span {
    font-size: 12px;
  }
  +span:not(.lever):before, ${this}:not(.filled-in)+span:not(.lever):after {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    width: 16px;
    height: 16px;
    z-index: 0;
    margin-top: 3px;
    -webkit-transition: .2s;
    transition: .2s;
    background: #FFFFFF;
    border: 1px solid #E0E0E0;
    box-sizing: border-box;
    border-radius: 2px;
  }
  +span:not(.lever) {
    position: relative;
    padding-left: 25px;
    cursor: pointer;
    display: inline-block;
    height: 25px;
    line-height: 25px;
    font-size: 12px;
    -webkit-user-select: none;
    -moz-user-select: none;
    -ms-user-select: none;
    user-select: none;
  }
  :not(.filled-in)+span:not(.lever):after {
    border: 0;
    -webkit-transform: scale(0);
    transform: scale(0);
  }
  :checked+span:not(.lever)::before {
    background: transparent;
    top: -4px;
    left: -5px;
    width: 12px;
    height: 22px;
    border-top: 2px solid transparent;
    border-left: 2px solid transparent;
    border-right: 2px solid #62B8D9;
    border-bottom: 2px solid #62B8D9;
    -webkit-transform: rotate(40deg);
    transform: rotate(40deg);
    -webkit-backface-visibility: hidden;
    backface-visibility: hidden;
    -webkit-transform-origin: 100% 100%;
    transform-origin: 100% 100%;
    transition: all .2s ease-in-out;
  }
`;
interface ICheckboxProps {
  className?: string;
  label?: string;
  checked?: boolean;
  required?: boolean;
  onChange?: (args?) => void;
}



class Checkbox extends React.Component<ICheckboxProps> {
  public static defaultProps: Partial<ICheckboxProps> = {
    checked: false,
    required: false,
  };
  public render() {
    const { label, checked, required, onChange } = this.props;
    return (
      <CheckboxWrapper>
        <CheckboxLabel>
          <CheckboxInput
            type="checkbox"
            checked={checked}
            required={required}
            onChange={onChange}
          />
          <span>{label}</span>
        </CheckboxLabel>
      </CheckboxWrapper>
    )
  }
}

export default Checkbox;