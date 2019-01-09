import styled, { StyledFunction } from 'styled-components';

interface IContainer {
  paddingTop?: number;
  paddingLeft?: number;
  paddingBottom?: number;
  paddingRight?: number;
}


const styledContainer: StyledFunction<any | React.HTMLProps<HTMLDivElement>> =
  styled.div;

export const Container = styledContainer`
  padding-top: ${(props: IContainer) => props.paddingTop ? `${props.paddingTop}px` : 0};
  padding-left: ${(props: IContainer) => props.paddingLeft ? `${props.paddingLeft}px` : `30px`};
  padding-bottom: ${(props: IContainer) => props.paddingBottom ? `${props.paddingBottom}px` : 0};
  padding-right: ${(props: IContainer) => props.paddingRight ? `${props.paddingRight}px` : `30px`};
`