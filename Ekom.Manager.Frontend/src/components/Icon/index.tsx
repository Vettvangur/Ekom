import * as React from 'react';
import styled from 'styled-components';
import '../../../assets/icons/icons.svg';

// ========================
// Components
// ========================
const StyledIcon = styled.svg`
  width: ${(props: any) => (props.iconSize ? `${props.iconSize}` : '100%')};
  height: ${(props: any) =>
    props.iconHeight ? `${props.iconHeight}px` : props.iconSize ? props.iconSize : '100%'};
  transform: ${(props: any) => `${props.rotateDeg ? `rotate(${props.rotateDeg}deg)` : 'none'}`};
  use, path {
    ${(props: IIconProps) => props.color ? `fill: ${props.color};` : ''}
    ${(props: IIconProps) =>
    props.color ? `color: ${props.color};` : ''}
  }
  margin-right: ${(props: any) => props.marginRight && `${props.marginRight}px`};
  margin-left: ${(props: any) => props.marginLeft && `${props.marginLeft}px`};
`;

// ========================
// Interfaces
// ========================
interface IIconProps {
  name: string;
  color?: string;
  colorGradient?: boolean;
  iconSize?: string | number;
  iconHeight?: number;
  marginRight?: number;
  marginLeft?: number;
  onClick?: any;
  rotateDeg?: number;
  className?: string;
}

/**
 * The Icon component.
 *
 * TODO: Rename this component to SVG and rename the SVG component to something like SlashSVG.
 */
class Icon extends React.Component<IIconProps, any> {
  public static defaultProps: Partial<IIconProps> = {};

  public getIconSize(iconSize: string | number | undefined): string | undefined {
    if (typeof iconSize === 'number') {
      return `calc(${iconSize}rem / 16)`;
    }

    switch (iconSize) {
      case 'mini': {
        return '0.4rem';
      }
      case 'tiny': {
        return '0.5rem';
      }
      case 'smaller': {
        return '0.75rem';
      }
      case 'small': {
        return '0.875rem';
      }
      case 'medium': {
        return '1rem';
      }
      case 'large': {
        return '1.5rem';
      }
      case 'large-big': {
        return '2rem';
      }
      case 'big': {
        return '2.5rem';
      }
      case 'great': {
        return '3rem';
      }
      case 'huge': {
        return '4rem';
      }
      case 'massive': {
        return '8rem';
      }
      default: {
        return undefined;
      }
    }
  }

  public getColor(color: string | undefined) {
    switch (color) {
      default: {
        return {
          startColor: '#1F3457',
          endColor: '#2B436B',
          rotate: '23.33',
        };
      }
    }
  }

  public render() {

    let iconName = this.props.name;
    if (iconName.indexOf('icon-', 0) === 0) {
      iconName = iconName.replace('icon-', '');
    }

    return (
      <StyledIcon
        name={iconName}
        className={`icon icon-${iconName} ${iconName} ${this.props.className &&
          this.props.className}`}
        color={this.props.color}
        width="100%"
        height="100%"
        iconSize={this.getIconSize(this.props.iconSize)}
        iconHeight={this.props.iconHeight}
        marginRight={this.props.marginRight}
        marginLeft={this.props.marginLeft}
        rotateDeg={this.props.rotateDeg}
      >
        <use xlinkHref={`#icons_icon-${iconName}`} />
      </StyledIcon>
    );
  }
}

export default Icon;
