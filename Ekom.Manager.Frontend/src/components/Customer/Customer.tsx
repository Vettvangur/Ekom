import * as React from 'react';
import styled from 'styled-components';
import * as variables from 'styles/variablesJS';

const CustomerWrapper = styled.div`
  position: relative;
  height:100%;
  min-height: 100px;
  width: calc((100% / 4 ) - 40px);
  flex-basis: calc((100% / 4 ) - 40px);
  padding: 16px 20px;
  background-color: ${variables.white};
  box-shadow: inset 0 0 0 1px ${variables.primaryColor}32;
  box-sizing: border-box;
  border-radius: 3px;
  margin: 20px 20px;
  &::before {
    content: '';
    height:100%;
    position: absolute;
    width: 3px;
    background-color: ${variables.primaryColor};
    border-top: 1px solid ${variables.primaryColor};
    border-bottom: 1px solid ${variables.primaryColor};
    top:0;
    left:0;
  }
`;

const CustomerName = styled.h4`
  margin: 0;
`;
const CustomerEmail = styled.div`
  opacity: .7;
  margin-bottom: 12px;
`;

const CustomerOrderCount = styled.span``;

interface IProps {
  UniqueId: string;
  Username: string;
  Email: string;
  StoreAlias: string;
  OrdersCount: number;
}

const Customer: React.SFC<IProps> = (customer) => {
  return (
    <CustomerWrapper>
      <CustomerName className="fs-16 lh-21 semi-bold">{customer.Username}</CustomerName>
      <CustomerEmail className="fs-12 lh-16">{customer.Email}</CustomerEmail>
      <CustomerOrderCount className="fs-12 lh-16">{customer.OrdersCount} {customer.OrdersCount > 1 || customer.OrdersCount === 0 ? 'orders' : 'order'}</CustomerOrderCount>
    </CustomerWrapper>
  );
};

export default Customer