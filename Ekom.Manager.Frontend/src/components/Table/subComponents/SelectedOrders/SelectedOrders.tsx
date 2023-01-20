import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import { Select } from 'components/Input';
import { Container } from 'styledComponents/global';
import * as variables from 'styles/variablesJS';
import Button from 'components/Button';
import OrdersStore from 'stores/ordersStore';
import RootStore from 'stores/rootStore';
import SearchStore from 'stores/searchStore';


const SelectedOrdersWrapper = styled.div`
  position:absolute;
  left:0;
  top: -56px;
  .selected-button {
    
    margin-left:16px;
  }
`;
const SelectedOrdersForm = styled.form`
  display:flex;
`;

const SelectedCount = styled.span`
  color: ${variables.primaryColor};
  margin-right:50px;
`;


interface ISelectedOrdersProps {
  ordersStore?: OrdersStore;
  rootStore?: RootStore;
  searchStore?: SearchStore;
  count: number;
  orders: string[];
  tableInstance?: any;
  updateStatus?: any;
}

@inject('ordersStore', 'rootStore', 'searchStore')
@observer
class SelectedOrders extends React.Component<ISelectedOrdersProps> {
  constructor(props: ISelectedOrdersProps) {
    super(props);
  }
  public handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const orderStatus = e.currentTarget['orderStatus'].value;
    console.log(orderStatus)
    this.props.orders.forEach(x => {
      this.props.updateStatus(e, x, orderStatus);
    })
  }
  public render() {
    const { count } = this.props;
    const orders = [];
    this.props.orders.forEach(item => {
      const x = this.props.searchStore.orders.Orders.filter(x => x.UniqueId === item);
      orders.push(x[0]);
    })
    const isAllEqual = orders.every((x, i, arr) => x.OrderStatusCol === orders[0].OrderStatusCol && x.OrderStatus === orders[0].OrderStatus) ? true : false;
    const statusList = this.props.rootStore.statusList.filter(x => x.label !== "Completed orders");
    console.log(isAllEqual)
    return (
      <SelectedOrdersWrapper>
        <Container>
          <SelectedOrdersForm onSubmit={this.handleSubmit}>
            <SelectedCount className="fs-20 lh-26 semi-bold">{count} {count > 1 ? 'orders' : 'order'} selected</SelectedCount>
            {isAllEqual ? (
              <Select name="orderStatus" options={statusList} radius={50} defaultValue={orders[0].OrderStatus} />
            ) : 
              <Select name="orderStatus" options={statusList} radius={50} />
            }
            <Button className="selected-button" type="submit" primary paddingTop={1} paddingBottom={1} borderRadius={0}>Save</Button>
          </SelectedOrdersForm>
        </Container>
      </SelectedOrdersWrapper>
    )
  }
}
export default SelectedOrders;