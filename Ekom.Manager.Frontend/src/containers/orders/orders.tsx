import * as React from 'react';

import styled from 'styled-components';

import { observer, inject } from 'mobx-react';
import OrdersStore from 'stores/ordersStore';

import SavingLoader from 'components/order/savingLoader';
import Search from 'components/Search';
import Table from 'components/Table';

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
  ordersStore?: OrdersStore;
}

@inject('ordersStore')
@observer
export default class Orders extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  componentDidMount() {
    this.props.ordersStore.shouldGetOrders();
  }
  public render() {
    return (
      <OrdersWrapper>
        {this.props.ordersStore.isUpdating && (
          <SavingLoader />
        )}
        <OrdersHeader>
          <Search />
          <OrdersInformationWrapper>
            <OrdersInformation className="fs-16 lh-16">
              <OrdersInformationColumn>
                Orders: <b>{this.props.ordersStore.ordersData.Count ? this.props.ordersStore.ordersData.Count : 0}</b>
              </OrdersInformationColumn>
              <OrdersInformationColumn>
                Grand total: <b>{this.props.ordersStore.ordersData.GrandTotal ? this.props.ordersStore.ordersData.GrandTotal : 0}</b>
              </OrdersInformationColumn>
              <OrdersInformationColumn>
                Average amount: <b>{this.props.ordersStore.ordersData.AverageAmount ? this.props.ordersStore.ordersData.AverageAmount : 0}</b>
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
