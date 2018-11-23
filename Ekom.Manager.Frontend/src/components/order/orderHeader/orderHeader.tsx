import * as React from 'react';
import * as moment from 'moment';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import statusList from 'utilities/statusList';
import OrdersStore from 'stores/ordersStore';


const OrderHeaderWrapper = styled.div`
  display: flex;
  justify-content: space-between;
  background-color: rgba(224, 234, 237, 0.5);
  width:100%;
  min-height: 6.25rem;
  padding:1.625rem 6.25rem;
  position:relative;
`;

const OrderHeaderInfoWrapper = styled.div`
  display:flex;
`;

const OrderHeaderInfoColumn = styled.div`
  margin-right: 3.75rem;
  font-size: 1.5rem;
  color: rgba(44,56,44,.9);
`;

const OrderHeaderInfoColumnLabel = styled.div`
  color: rgba(44,56,44,.5);
`

const StatusSelectWrapper = styled.div`
  margin-right: 15px;
`;

interface IProps {
  ordersStore?: OrdersStore;
  originalStatus: any;
  order: any;
}

class State {
  status: any;
}

@inject('ordersStore')
@observer
export default class OrderHeader extends React.Component<IProps, State> {
  constructor(props) {
    super(props);

    this.state = new State();
    this.updateStatus = this.updateStatus.bind(this);
    this.handleStatusChange = this.handleStatusChange.bind(this);
  }

  componentDidMount() {
    const {
      originalStatus,
    } = this.props;

    this.setState({
      status: originalStatus,
    })
  }

  handleStatusChange(event) {
    this.setState({ status: event.target.value });
  }

  updateStatus(e) {
    e.preventDefault();
    const {
      status,
    } = this.state;
    const {
      order,
      ordersStore,
    } = this.props;
    const orderId = order.UniqueId;
    const orderStatus = status;
    ordersStore.updateOrderStatus(orderId, orderStatus)
  }

  render() {
    const {
      order,
      originalStatus,
    } = this.props;

    return (
      <OrderHeaderWrapper>
        <OrderHeaderInfoWrapper>
          <OrderHeaderInfoColumn>
            {order.OrderNumber}
            <OrderHeaderInfoColumnLabel className="fs-14 uppercase">
              Order No.
              </OrderHeaderInfoColumnLabel>
          </OrderHeaderInfoColumn>
          <OrderHeaderInfoColumn>
            {order.PaidDate ? (
              order.PaidDate === moment().format('YYYY-MM-DD') ? 'Í dag' : moment(order.PaidDate).format('YYYY-MM-DD')
            )
              : (
                moment(order.CreateDate).format('YYYY-MM-DD')
              )}
            <OrderHeaderInfoColumnLabel className="fs-14 uppercase">
              Order Date
                {order.PaidDate === null && ' (Created)'}
            </OrderHeaderInfoColumnLabel>
          </OrderHeaderInfoColumn>
          <OrderHeaderInfoColumn>
            {order.PaidDate ? moment(order.PaidDate).format('HH:mm') : moment(order.CreateDate).format('HH:mm')}
            <OrderHeaderInfoColumnLabel className="fs-14 uppercase">
              Order Time
                {order.PaidDate === null && ' (Created)'}
            </OrderHeaderInfoColumnLabel>
          </OrderHeaderInfoColumn>
          <OrderHeaderInfoColumn>
            {order.StoreInfo.Alias}
            <OrderHeaderInfoColumnLabel className="fs-14 uppercase">
              Store
              </OrderHeaderInfoColumnLabel>
          </OrderHeaderInfoColumn>
        </OrderHeaderInfoWrapper>
        <form onSubmit={e => this.updateStatus(e)} className="flex">
          <StatusSelectWrapper className="select__wrapper">
            <select name="" id="" defaultValue={originalStatus} onChange={this.handleStatusChange}>
              {statusList.map(statusItem => (
                <option key={statusItem.value} value={statusItem.value}>
                  {statusItem.label}
                </option>
              ))}
            </select>
          </StatusSelectWrapper>
          <button type="submit" className="button">Save</button>
        </form>
      </OrderHeaderWrapper>
    );
  }
}