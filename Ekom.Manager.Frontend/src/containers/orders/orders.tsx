import * as React from 'react';

import styled from 'styled-components';

import { observer, inject } from 'mobx-react';

import SavingLoader from 'components/order/savingLoader';
import Search from 'components/Search';
import Table from 'components/Table';
import SearchStore from 'stores/searchStore';
import OrdersStore from 'stores/ordersStore';

const OrdersWrapper = styled.div`
  height: 100%;
  display:flex; 
  flex-direction: column;
`;

const OrdersHeader = styled.div`
  margin: 1.875rem 1.875rem 6.25rem 1.875rem;
`;
const OrdersInformationWrapper = styled.div`
  border: 1px solid #4B8DA616;
  border-top:0;
  border-radius: 0px 0px 3px 3px;
  display:flex;
  justify-content: space-between;
  padding-top:17px;
  padding-bottom: 17px;
  padding-left: 20px;
  padding-right: 30px;
`;

const OrdersInformation = styled.div`
  display: flex;
`;
const OrdersInformationColumn = styled.div`
  &:not(:last-child) {
    margin-right: 2.5rem;
  }
`
const OrdersCSV = styled.a``;

interface IProps {
  searchStore?: SearchStore;
  ordersStore?: OrdersStore;
}

@inject('searchStore', 'ordersStore')
@observer
export default class Orders extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  componentDidMount() {
    this.props.searchStore.search();
  }
  public render() {
    return (
      <OrdersWrapper>
        {this.props.ordersStore.state === "loading" && (
          <SavingLoader />
        )}
        <OrdersHeader>
          <Search />
          <OrdersInformationWrapper>
            <OrdersInformation className="fs-16 lh-16">
              <OrdersInformationColumn>
                Orders: <b>{this.props.searchStore.orders.Count ? this.props.searchStore.orders.Count : 0}</b>
              </OrdersInformationColumn>
              <OrdersInformationColumn>
                Grand total: <b>{this.props.searchStore.orders.GrandTotal ? this.props.searchStore.orders.GrandTotal : 0}</b>
              </OrdersInformationColumn>
              <OrdersInformationColumn>
                Average amount: <b>{this.props.searchStore.orders.AverageAmount ? this.props.searchStore.orders.AverageAmount : 0}</b>
              </OrdersInformationColumn>
            </OrdersInformation>
            <OrdersCSV className="fs-12" href="#">Export results to CSV</OrdersCSV>
            
          </OrdersInformationWrapper>
        </OrdersHeader>
        <Table />
      </OrdersWrapper>
    );
  }
}
