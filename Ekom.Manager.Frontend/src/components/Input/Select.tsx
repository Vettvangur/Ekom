import * as React from 'react';

import styled from 'styled-components';

import * as variables from 'styles/variablesJS';
import Icon from 'components/Icon';

const SelectWrapper = styled.div`
   
`;

export const Select = styled<any>("select")`
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
  defaultValue?: string;
  value?: string;
  className?: string;
  id?: string;
  name?: string;
  required?: boolean;
  onChange?: (args?) => void;
}



class SelectComponent extends React.Component<ISelectProps> {
  public render() {
    const {
      color,
      icon,
      backgroundColor,
      radius,
      options,
      ...props 
    } = this.props;
    return (
      <SelectWrapper className="fs-14 lh-18">
        <Select {...props} backgroundColor={backgroundColor} borderRadius={radius}>
          <Option value="-1"></Option>
          {options.map((option, index) => (
            <Option key={index} value={option.value}>{option.label}</Option>
          ))}
        </Select>
        { icon && icon.length > 0
          ? <Icon name={icon} iconSize={8} color={variables.primaryColor} />
          : <Icon name="ellipse" iconSize={8} color={variables.primaryColor} />
        }
      </SelectWrapper>
    )
  }
}

export default SelectComponent;