import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import CustomerCard from 'components/Cards/CustomerCard';
import Icon from 'components/Icon';
import Loading from 'components/shared/Loading';
import Search from 'components/Search';
import SearchStore from 'stores/searchStore';
import * as variables from 'styles/variablesJS';

const CustomersWrapper = styled.div`
`;
const SearchWrapper = styled.div`
  margin-top:30px;
  margin-bottom:50px;
`;


const Container = styled.div`
  padding: 0 30px;
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


const CustomersTable = styled.div`
  display:flex;
  flex-wrap:wrap;
  margin: -20px -20px;
`;

interface IProps {
  searchStore?: SearchStore;
}

@inject('searchStore')
@observer
export default class Customers extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  componentDidMount() {
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
  render() {
    const customersMockUp = [
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      },
      {
        UniqueId: "Guid",
        Username: "Garðar Þorsteinsson",
        Email: "Gardar@vettvangur.is",
        StoreAlias: "IS",
        OrdersCount: 9,
      }
    ]
    return (
      <CustomersWrapper>
        <SearchWrapper>
        <Container>
          <Search customers />
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
        </SearchWrapper>
        <Container>
          <CustomersTable>
            {customersMockUp.map(customer => <CustomerCard {...customer} />)}
          </CustomersTable>
        </Container>
      </CustomersWrapper>
    )
  }
}
