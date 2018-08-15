/* eslint no-console: ["error", { allow: ["warn", "error"] }] */

import React, { Component } from 'react';
import PropTypes from 'prop-types';
import moment from 'moment';
import SavingLoader from 'containers/orderContainer/components/savingLoader';
import statusList from 'utilities/statusList';
import { orderService } from 'services';
import s from './styles.scss';

class OrderHeader extends Component {
  constructor(props) {
    super(props);
    this.state = {
      status: null,
      statusUpdateIndicator: false,
    };
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
    } = this.props;
    const orderId = order.UniqueId;
    const orderStatus = status;
    this.setState({
      statusUpdateIndicator: true,
    });
    orderService().updateStatus(orderId, orderStatus).then(() => {
      setTimeout(() => {
        this.setState({
          statusUpdateIndicator: false,
        });
      }, 1500);
    })
      .catch((err) => {
        this.setState({
          statusUpdateIndicator: false,
        });
        console.error(`error: ${err}`);
      });
  }

  render() {
    const {
      order,
      originalStatus,
    } = this.props;
    const {
      statusUpdateIndicator,
    } = this.state;

    return (
      <div className={s.header}>
        <div className="flex flex__justify--between">
          <div className="flex">
            <div className={s.column}>
              <div>
                {order.OrderNumber}
              </div>
              <div>
                Order No.
              </div>
            </div>
            <div className={s.column}>
              <div>
                {order.PaidDate && (
                  order.PaidDate === moment().format('YYYY-MM-DD') ? 'Í dag' : moment(order.PaidDate).format('YYYY-MM-DD')
                )}
                {order.PaidDate === null && (
                  moment(order.CreateDate).format('YYYY-MM-DD')
                )}
              </div>
              <div>
                Order Date
                {order.PaidDate === null && ' (Created)'}
              </div>
            </div>
            <div className={s.column}>
              <div>
                {order.PaidDate ? moment(order.PaidDate).format('HH:mm') : moment(order.CreateDate).format('HH:mm')}
              </div>
              <div>
                Order Time
                {order.PaidDate === null && ' (Created)'}
              </div>
            </div>
            <div className={s.column}>
              <div>
                {order.StoreInfo.Alias}
              </div>
              <div>
                Store
              </div>
            </div>
          </div>
          <div className={s.updateStatusForm}>
            <form className="flex" onSubmit={e => this.updateStatus(e)}>
              <div className="select__wrapper">
                <select name="" id="" defaultValue={originalStatus} onChange={this.handleStatusChange}>
                  {statusList.map(statusItem => (
                    <option key={statusItem.id} value={statusItem.id}>
                      {statusItem.value}
                    </option>
                  ))}
                </select>
              </div>
              <button type="submit" className="button">
                Save
              </button>
            </form>
          </div>
        </div>
        {statusUpdateIndicator
          && <SavingLoader />
        }
      </div>
    );
  }
}

export default OrderHeader;
