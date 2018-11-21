import * as React from 'react';
import styled from 'styled-components';

import * as variables from 'styles/variablesJS';

const StyledAnchor = styled.a`
  display: inline-flex;
  
  color: ${(props: IButtonProps) => (props.color ? props.color : props.primary ? variables.white : variables.primaryColor)};
  text-transform: ${(props: IButtonProps) => (props.uppercase ? 'uppercase' : 'none')};
  text-decoration: none;
  align-items: ${({ center }: IButtonProps) => center && 'center'};
  
  padding-top: ${({paddingTop}) => paddingTop ? `${paddingTop}px` : '0.9375rem'};
  padding-bottom: ${({paddingBottom}) => paddingBottom ? `${paddingBottom}px` : '0.9375rem'};
  padding-left: ${({paddingLeft}) => paddingLeft ? `${paddingLeft}px` : '1.5625rem'};
  padding-right: ${({paddingRight}) => paddingRight ? `${paddingRight}px` : '1.5625rem'};
  cursor: ${({disabled}: any) => disabled ? 'not-allowed' : 'cursor'};
`;

const StyledButton = styled(StyledAnchor)`
  border: 0;
  border-radius: ${(props: IButtonProps) => props.borderRadius && props.borderRadius};
  background-color: ${(props: IButtonProps) => {
    if (props.disabled) {
      return `rgba(${variables.secondaryColor}, .5)`
    }
    if (props.backgroundColor) {
      return props.backgroundColor;
    }
    if (props.white)
      return variables.white;
    if (props.primary)
      return variables.primaryColor;
    return variables.secondaryColor;
  }};
`;

interface IButtonProps {
  className?: string;
  type?: string;
  color?: string;
  href?: string;
  primary?: boolean;
  white?: boolean;
  xl?: boolean;
  disabled?: boolean;
  backgroundColor?: string;
  uppercase?: boolean;
  openInNewTab?: boolean;
  center?: boolean;
  borderRadius?: (string | number);
  paddingTop?: number;
  paddingBottom?: number;
  paddingLeft?: number;
  paddingRight?: number;
  iconPos?: 'left' | 'right' | '';
  onClick?: () => void;
  target?: '_blank' | '_self' | '';
}

class Button extends React.Component<IButtonProps> {
  public static defaultProps: Partial<IButtonProps> = {
    primary: false,
    white: false,
    xl: false,
    openInNewTab: false,
  };
  public constructor(props: IButtonProps) {
    super(props);
  }
  public render() {
    const { children, ...props } = this.props;

    if (this.props.href)
      return (
        <StyledAnchor 
          {...props}
        >
          {children}
        </StyledAnchor>
      )
    return (
      <StyledButton
        as="button"
        {...props}
      >
        {children}
      </StyledButton>
    )
  }
}

export default Button;