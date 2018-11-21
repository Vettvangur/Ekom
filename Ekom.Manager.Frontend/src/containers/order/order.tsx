import * as React from 'react';
import * as moment from 'moment';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import { RouterStore } from 'mobx-react-router';
import {
  Link,
  withRouter,
} from 'react-router-dom';
// import OrderHeader from 'components/order/orderHeader';
import Products from 'components/order/products';
import SavingLoader from 'components/order/savingLoader';
import statusList from 'utilities/statusList';

import OrdersStore from 'stores/ordersStore';
import InformationColumn from './components/InformationColumn';
import ActivityLog from './components/ActivityLog';
import Icon from 'components/Icon';
import * as variables from 'styles/variablesJS';
import Divider from './components/Divider';
import { Checkbox } from 'components/Input';

import Button from 'components/Button';

const OrderWrapper = styled.div`
  position: relative;
  overflow: auto;
  height: 100vh;
  min-height: 100vh;
`;

const Container = styled.div`
  padding: 0 30px;
`;

const GoBack = styled(Link)`
  display:flex;
  color: ${variables.primaryColor};
  background-color: ${variables.ekomSecondaryColor};
  align-items:center;
  padding: 10px 20px;
  svg {
      margin-right: 6px;
  }
`;

const OrderInformationWrapper = styled.div`
  margin-top: 50px;
  display:flex;
  justify-content: space-between;
  margin-bottom:20px;
`;

const OrderInformationSubWrapper = styled.div`
  display:flex;
  flex-wrap:wrap;
  margin-bottom: 50px;
`;

const OrderInformationSubColumn = styled.div`
  &:not(:last-child) {
    margin-right: 30px;
  }
`;



const OrderPaymentWrapper = styled.div`
  display:flex;
  justify-content: space-between;
  margin-top: 20px;
  margin-bottom: 70px;
  flex-wrap: wrap;
`;

const OrderPaymentColumns = styled.div`
  display:flex;
  flex: 1 1 0;
  flex-wrap: wrap;
`;

const Heading = styled.h1`
  color: ${variables.black};
  margin: 0;
`

const OrderNumberWrapper = styled.div`
  margin-right: 30px;
`;

const StatusUpdate = styled.div`
  display:flex;
  height: 48px;
  select {
    height:100%;
  }
`;

const StatusSelectWrapper = styled.div`
  height:48px;
  &::after {
    content: none!important;
  }
`;
interface IProps {
  ordersStore?: OrdersStore;
  match: any;
  routing: RouterStore;
}

@inject('routing', 'ordersStore')
@withRouter
@observer
export default class Order extends React.Component<IProps> {
  constructor(props) {
    super(props);
  }

  componentDidMount() {
    const { match, ordersStore } = this.props;
    const orderId = match.params.id;
    ordersStore.shouldFetchOrder(orderId);
  }

  updateStatus = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const { order, updateOrderStatus } = this.props.ordersStore;
    const orderId = order.UniqueId;
    const orderStatus = e.currentTarget['orderStatus'].value;
    const ShouldSendNotification = e.currentTarget['sendNotification'].checked;
    updateOrderStatus(orderId, orderStatus, ShouldSendNotification)
  }

  render() {
    const { order, state, orderState } = this.props.ordersStore;
    console.log(orderState);
    if (orderState === "pending")
      return <div></div>
    if (orderState === "loading")
      return <div></div>
    if (orderState === "error")
      return <div></div>
    return (
      <OrderWrapper>
        {state === "loading" && (
          <SavingLoader />
        )}
        <Container>
          <GoBack to="/umbraco/backoffice/ekom/manager/orders" className="fs-16 lh-21 semi-bold">
            <Icon name="arrow-left-sm" iconSize={17} color={variables.primaryColor} />
            Back to search...
          </GoBack>
          <OrderInformationWrapper>
            <div className="flex align-center">
              <OrderNumberWrapper>
                <Heading className="fs-32 lh-42">{order && order.OrderNumber}</Heading>
                <span className="fs-16 lh-21">Order number</span>
              </OrderNumberWrapper>
              <div>
                {order.PaidDate !== '0001-01-01T00:00:00' && order.PaidDate !== null
                  ? (
                    <svg width="24" height="24" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M6.66667 0L1.66667 4.66667L0 3" transform="translate(5.66669 6.66675)" stroke="#53BC50" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                      <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.244 6.35867C14.7713 6.01467 14.998 5.336 14.75 4.73333C14.5 4.13 13.8587 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0867 2.246C12.6253 1.78533 11.9107 1.73467 11.392 2.09067C11.5227 1.47467 11.2027 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.974 1.08933C8.85867 0.469333 8.31933 0 7.66667 0C7.01267 0 6.47267 0.469333 6.35667 1.08867C6.014 0.560667 5.33467 0.334 4.73133 0.584C4.128 0.833333 3.80867 1.474 3.93867 2.09C3.42 1.73467 2.706 1.78467 2.244 2.246C1.78267 2.70733 1.73267 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.559333 6.01467 1.08667 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468 8.86 1.08667 8.97533C0.559333 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73267 11.9127 1.78333 12.6267 2.244 13.088C2.70533 13.5493 3.41933 13.6 3.93867 13.244C3.80867 13.8593 4.12867 14.4993 4.73133 14.7493C5.334 14.9993 6.014 14.7727 6.35667 14.2447C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85867 14.8647 8.974 14.2453C9.31733 14.7733 9.996 14.9987 10.5993 14.75C11.202 14.5 11.522 13.86 11.3913 13.2433C11.91 13.6 12.6247 13.5493 13.086 13.088C13.5473 12.6267 13.5987 11.9127 13.2413 11.3933C13.8573 11.524 14.4993 11.204 14.7493 10.6013C14.9993 9.998 14.7713 9.31867 14.2433 8.97533C14.8627 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33337)" stroke="#53BC50" strokeMiterlimit="10" strokeLinejoin="round" />
                    </svg>
                  )
                  : (
                    <svg width="24" height="24" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.2447 6.35867C14.7713 6.01467 14.9987 5.336 14.75 4.73333C14.5 4.13 13.8593 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0873 2.246C12.626 1.78533 11.9113 1.73467 11.392 2.09067C11.5227 1.47467 11.2033 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.97467 1.08933C8.85933 0.469333 8.31933 0 7.66667 0C7.01333 0 6.47267 0.469333 6.35733 1.08867C6.014 0.560667 5.33533 0.334 4.732 0.584C4.128 0.833333 3.80933 1.474 3.93933 2.09C3.42067 1.73467 2.70667 1.78467 2.24467 2.246C1.78333 2.70733 1.73333 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.56 6.01467 1.08733 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468667 8.86 1.08733 8.97533C0.56 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73333 11.9127 1.78333 12.6267 2.24467 13.088C2.70667 13.5493 3.42 13.6 3.93933 13.2447C3.80933 13.86 4.12933 14.5 4.732 14.75C5.33467 15 6.014 14.7733 6.35733 14.2453C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85933 14.8647 8.97467 14.2453C9.31733 14.7733 9.996 14.9987 10.6 14.75C11.2027 14.5 11.522 13.86 11.392 13.2433C11.9113 13.6 12.626 13.5493 13.0873 13.088C13.548 12.6267 13.5993 11.9127 13.242 11.3933C13.858 11.524 14.5 11.204 14.75 10.6013C15 9.998 14.772 9.31867 14.2447 8.97533C14.8633 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33337)" stroke="#F25555" strokeMiterlimit="10" strokeLinejoin="round" />
                      <path d="M0 0L6 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                      <path d="M6 0L0 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  )


                }
              </div>
            </div>
            <form onSubmit={e => this.updateStatus(e)}>
              <StatusUpdate>
                <StatusSelectWrapper className="select__wrapper">
                  <select name="orderStatus" id="orderStatus" defaultValue={order.OrderStatus.toString()}>
                    {statusList.map(statusItem => (
                      <option key={statusItem.id} value={statusItem.id}>
                        {statusItem.value}
                      </option>
                    ))}
                  </select>
                </StatusSelectWrapper>
                <Button className="fs-16 semi-bold" paddingTop={0} paddingBottom={0} primary>Save</Button>
              </StatusUpdate>
              <Checkbox className="align-center" name="sendNotification" id="sendNotification" label="Send notification when status changes" />
            </form>
          </OrderInformationWrapper>
          <OrderInformationSubWrapper>
            <OrderInformationSubColumn>
              <Heading
                as="h3"
                className="fs-24 lh-31 semi-bold"
              >
                {order.PaidDate && (
                  order.PaidDate === moment().format('DD.mm.YYYY') ? 'Í dag' : moment(order.PaidDate).format('DD.mm.YYYY')
                )}
                {order.PaidDate === null && (
                  moment(order.CreateDate).format('DD.mm.YYYY')
                )}
              </Heading>
              <span className="fs-16 lh-21">
                Order date
                {order.PaidDate === null && ' (Created)'}
              </span>
            </OrderInformationSubColumn>
            <OrderInformationSubColumn>
              <Heading
                as="h3"
                className="fs-24 lh-31 semi-bold"
              >
                {order.PaidDate ? moment(order.PaidDate).format('HH:mm') : moment(order.CreateDate).format('HH:mm')}
              </Heading>
              <span className="fs-16 lh-21">
                Order time
                {order.PaidDate === null && ' (Created)'}
              </span>
            </OrderInformationSubColumn>
            <OrderInformationSubColumn>
              <Heading
                as="h3"
                className="fs-24 lh-31 semi-bold"
              >
                {order.StoreInfo.Alias}
              </Heading>
              <span className="fs-16 lh-21">
                Store
                {order.PaidDate === null && ' (Created)'}
              </span>

            </OrderInformationSubColumn>
          </OrderInformationSubWrapper>
        </Container>
        <Divider />
        <Container>
          <OrderPaymentWrapper>
            <OrderPaymentColumns>
              <InformationColumn heading="Billing" list={[
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.Name,
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.Email,
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.Address,
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.ZipCode,
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.City,
                order.CustomerInformation.Customer !== null && order.CustomerInformation.Customer.Country
              ]} />
              <InformationColumn heading="Shipping" list={[
                order.CustomerInformation.Shipping !== null && order.CustomerInformation.Shipping.Name,
                order.CustomerInformation.Shipping !== null && order.CustomerInformation.Shipping.Address,
                order.CustomerInformation.Shipping !== null && order.CustomerInformation.Shipping.ZipCode,
                order.CustomerInformation.Shipping !== null && order.CustomerInformation.Shipping.City,
                order.CustomerInformation.Shipping !== null && order.CustomerInformation.Shipping.Country
              ]} />
              <InformationColumn heading="Payment" list={[
                order.PaymentProvider !== null ? order.PaymentProvider.Title : 'Not registered'
              ]} />
              <InformationColumn heading="Delivery" list={[
                order.ShippingProvider !== null ? order.ShippingProvider.Title : 'Not registered'
              ]} />

              {/* <div>
                <h3>
                  Refund to Credit Card
                      </h3>
                {order.PaidDate !== '0001-01-01T00:00:00' && order.PaidDate !== null
                  ? (
                    <div>
                      <form className="flex">
                        <input type="text" placeholder="Refund amount" />
                        <button type="button" className="button">
                          Refund
                            </button>
                      </form>
                    </div>
                  )
                  : 'Unavailable'
                }
              </div> */}
            </OrderPaymentColumns>
            <ActivityLog activityLogs={order.ActivityLog} />
          </OrderPaymentWrapper>
        </Container>
        <Divider />

        <React.Fragment>
          {/* <OrderHeader
                order={order}
                originalStatus={status}
              /> */}
          {/* <div className={s.billingColumn}>
                    <div className={s.billingIcon}>
                      {order.PaidDate !== '0001-01-01T00:00:00' && order.PaidDate !== null
                        ? (
                          <svg width="24" height="24" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M6.66667 0L1.66667 4.66667L0 3" transform="translate(5.66669 6.66675)" stroke="#53BC50" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.244 6.35867C14.7713 6.01467 14.998 5.336 14.75 4.73333C14.5 4.13 13.8587 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0867 2.246C12.6253 1.78533 11.9107 1.73467 11.392 2.09067C11.5227 1.47467 11.2027 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.974 1.08933C8.85867 0.469333 8.31933 0 7.66667 0C7.01267 0 6.47267 0.469333 6.35667 1.08867C6.014 0.560667 5.33467 0.334 4.73133 0.584C4.128 0.833333 3.80867 1.474 3.93867 2.09C3.42 1.73467 2.706 1.78467 2.244 2.246C1.78267 2.70733 1.73267 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.559333 6.01467 1.08667 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468 8.86 1.08667 8.97533C0.559333 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73267 11.9127 1.78333 12.6267 2.244 13.088C2.70533 13.5493 3.41933 13.6 3.93867 13.244C3.80867 13.8593 4.12867 14.4993 4.73133 14.7493C5.334 14.9993 6.014 14.7727 6.35667 14.2447C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85867 14.8647 8.974 14.2453C9.31733 14.7733 9.996 14.9987 10.5993 14.75C11.202 14.5 11.522 13.86 11.3913 13.2433C11.91 13.6 12.6247 13.5493 13.086 13.088C13.5473 12.6267 13.5987 11.9127 13.2413 11.3933C13.8573 11.524 14.4993 11.204 14.7493 10.6013C14.9993 9.998 14.7713 9.31867 14.2433 8.97533C14.8627 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33337)" stroke="#53BC50" strokeMiterlimit="10" strokeLinejoin="round" />
                          </svg>
                        )
                        : (
                          <svg width="24" height="24" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.2447 6.35867C14.7713 6.01467 14.9987 5.336 14.75 4.73333C14.5 4.13 13.8593 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0873 2.246C12.626 1.78533 11.9113 1.73467 11.392 2.09067C11.5227 1.47467 11.2033 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.97467 1.08933C8.85933 0.469333 8.31933 0 7.66667 0C7.01333 0 6.47267 0.469333 6.35733 1.08867C6.014 0.560667 5.33533 0.334 4.732 0.584C4.128 0.833333 3.80933 1.474 3.93933 2.09C3.42067 1.73467 2.70667 1.78467 2.24467 2.246C1.78333 2.70733 1.73333 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.56 6.01467 1.08733 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468667 8.86 1.08733 8.97533C0.56 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73333 11.9127 1.78333 12.6267 2.24467 13.088C2.70667 13.5493 3.42 13.6 3.93933 13.2447C3.80933 13.86 4.12933 14.5 4.732 14.75C5.33467 15 6.014 14.7733 6.35733 14.2453C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85933 14.8647 8.97467 14.2453C9.31733 14.7733 9.996 14.9987 10.6 14.75C11.2027 14.5 11.522 13.86 11.392 13.2433C11.9113 13.6 12.626 13.5493 13.0873 13.088C13.548 12.6267 13.5993 11.9127 13.242 11.3933C13.858 11.524 14.5 11.204 14.75 10.6013C15 9.998 14.772 9.31867 14.2447 8.97533C14.8633 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33337)" stroke="#F25555" strokeMiterlimit="10" strokeLinejoin="round" />
                            <path d="M0 0L6 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                            <path d="M6 0L0 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                          </svg>
                        )


                      }
                    </div>
                    <h3>
                      {order.PaidDate !== '0001-01-01T00:00:00' && order.PaidDate !== null ? 'Greidd pöntun' : 'Ógreidd pöntun'}
                    </h3>
                  </div> */}
          {/* {refundable && (
                    <div className={s.billingColumn}>
                      <h3>
                        Refund to creditcard
                      </h3>
                      {order.PaidDate !== '0001-01-01T00:00:00' && order.PaidDate !== null
                        ? (
                          <div className={s.refundForm}>
                            <form className="flex">
                              <input type="text" placeholder="Refund amount" />
                              <button type="button" className="button">
                                Refund
                            </button>
                            </form>
                          </div>
                        )
                        : 'Unavailable'
                      }
                    </div>
                  )} */}
          <Container>
            <Products orderlines={order.OrderLines} orderTotal={order.ChargedAmount.CurrencyString} />
          </Container>
        </React.Fragment>
      </OrderWrapper>
    );
  }
}
