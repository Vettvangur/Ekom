import * as React from 'react';

import styled from 'styled-components';

import { observer, inject } from 'mobx-react';

import SavingLoader from 'components/order/savingLoader';
import Loading from 'components/shared/Loading';
import Search from 'components/Search';
import Table from 'components/Table';
import SearchStore from 'stores/searchStore';
import OrdersStore from 'stores/ordersStore';

import Icon from 'components/Icon';

import * as variables from 'styles/variablesJS';

const OrdersWrapper = styled.div`
  height: 100%;
  display:flex; 
  flex-direction: column;
`;

const Container = styled.div`
  padding: 0 1.875rem;
`;


const OrdersHeader = styled.div`
  margin-top: 1.875rem;
  margin-bottom: 6.25rem;
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


const EmptyStateWrapper = styled.div`
  display: flex;
  justify-content: center;
`;

const LoadingStateWrapper = styled.div``;

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
    const { shouldSearchForOrders } = this.props.searchStore;
    shouldSearchForOrders();
    document.addEventListener('keypress', (e) => this.handleSearchOnEnter(e), false);
  }
  componentWillUnmount() {
    document.removeEventListener('keypress', (e) => this.handleSearchOnEnter(e), false);
  }
  public handleSearchOnEnter(e) {
    if (e.keyCode === 13)
      this.props.searchStore.search();
  }
  renderLoadingState() {
    return (
      <LoadingStateWrapper>
        <Loading />
      </LoadingStateWrapper>
    )
  }
  renderEmptyState() {
    return (
      <EmptyStateWrapper>
        <Icon name="icon-sleepboldman" iconSize={300} color={variables.black} />
      </EmptyStateWrapper>
    )
  }
  public render() {
    const { searchStore } = this.props;
    return (
      <OrdersWrapper>
        {this.props.ordersStore.state === "loading" && (
          <SavingLoader />
        )}
        <OrdersHeader>
          <Container>
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
          </Container>
        </OrdersHeader>
        {searchStore.state === "pending" && this.renderLoadingState()}
        {searchStore.state === "empty" && this.renderEmptyState()}
        {searchStore.state === "done" &&
          <Table />
        }
      </OrdersWrapper>
    );
  }
}
