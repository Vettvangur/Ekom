import * as React from 'react';
import styled from 'styled-components';
import * as variables from 'styles/variablesJS';


const StyledCustomerCard = styled.div`
  position: relative;
  height:100%;
  min-height: 100px;
  /* width: calc((100% / 4 ) - 40px);
  flex-basis: calc((100% / 4 ) - 40px); */
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
  /* @media screen and (max-width: 1100px) {
    width: calc((100% / 3 ) - 40px);
    flex-basis: calc((100% / 3 ) - 40px);
  }
  @media screen and (max-width: 768px) {
    width: calc((100% / 6 ) - 40px);
    flex-basis: calc((100% / 2 ) - 40px);
  } */
`;

const StyledCustomerName = styled.h4`
  margin: 0;
`;
const StyledCustomerEmail = styled.div`
  opacity: .7;
  margin-bottom: 12px;
`;

const CustomerOrderCount = styled.span``;

interface ICustomerCardProps {
  UniqueId: string;
  Username: string;
  Email: string;
  StoreAlias: string;
  OrdersCount: number;
  UrlToPage?: string;
}

const Customer: React.SFC<ICustomerCardProps> = (customer) => {
  return (
    <StyledCustomerCard className="col-xs-1 col-sm-2 col-md-3 col-lg-4 col-xl-6">
      <StyledCustomerName className="fs-16 lh-21 semi-bold">{customer.Username}</StyledCustomerName>
      <StyledCustomerEmail className="fs-12 lh-16">{customer.Email}</StyledCustomerEmail>
      <CustomerOrderCount className="fs-12 lh-16">{customer.OrdersCount} {customer.OrdersCount > 1 || customer.OrdersCount === 0 ? 'orders' : 'order'}</CustomerOrderCount>
    </StyledCustomerCard>
  );
};

export default Customer