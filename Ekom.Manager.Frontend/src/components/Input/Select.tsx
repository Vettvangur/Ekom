import * as React from 'react';
import { observer } from 'mobx-react';

import styled, { StyledFunction } from 'styled-components';

import * as variables from 'styles/variablesJS';
import Icon from 'components/Icon';


const styledSelect: StyledFunction<any | React.HTMLProps<HTMLSelectElement>> =
  styled.select;

const SelectWrapper = styled.div`
  position: relative;
  display:flex;
  align-items:center;
  svg {
    position:absolute;
    right: 7px;
    pointer-events: none;
  }
   
`;

export const Select = styledSelect`
  background-color: ${(props: any) => props.backgroundColor ? props.backgroundColor : variables.defaultSelect};
  color: ${(props: any) => props.color ? props.color : 'rgba(44, 56, 44, 0.9)'};
  position: relative;
  display: inline-block;
  width: 100%;
  cursor: pointer;
  padding-left: 10px;
  padding-right: 24px;
  padding-top: 2px;
  padding-bottom: 2px;
  outline: 0;
  border: 1px solid #e5e5e5;
  border-radius: ${(props: any) => props.borderRadius ? `${props.borderRadius}px` : '50px'};
  -webkit-appearance: none;
`;
const Option = styled.option``;


// const Ellipsis = styled.div`
//   border-radius: 8px;
//   width: 8px;
//   height: 8px;
//   border-color: ${variables.primaryColor};
// `;

interface IOption {
  value: (number | string);
  label: string;
}

interface ISelectProps {
  options: IOption[];
  icon?: string;
  backgroundColor?: string;
  color?: string;
  radius?: number;
  defaultValue?: any;
  value?: string;
  className?: string;
  id?: string;
  name?: string;
  required?: boolean;
  onChange?: (args?) => void;
}



@observer
class SelectComponent extends React.Component<ISelectProps> {
  public render() {
    const {
      color,
      icon,
      backgroundColor,
      radius,
      options,
      defaultValue,
      ...props
    } = this.props;
    console.log(this.props.defaultValue)
    return (
      <SelectWrapper>
        <Select className="fs-14 lh-18" {...props} backgroundColor={backgroundColor} borderRadius={radius} >
          <Option value="-1"></Option>
          {options.map((option, index) => {
            if (this.props.defaultValue && option.value === this.props.defaultValue) {
              return (
                <Option key={index} value={option.value} selected >{option.label}</Option>
              )
            }
            return (
              <Option key={index} value={option.value}>{option.label}</Option>
            )
          })}
        </Select>
        {icon && icon.length > 0
          ? <Icon name={icon} iconSize={8} color={variables.primaryColor} />
          : <Icon name="oli-lokbra" iconSize={8} color={variables.primaryColor} />
        }
      </SelectWrapper>
    )
  }
}

export default SelectComponent;