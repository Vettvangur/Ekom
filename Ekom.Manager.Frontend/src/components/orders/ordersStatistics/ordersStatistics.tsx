import * as React from 'react';

import { observer, inject } from 'mobx-react';

import OrdersStore from 'stores/ordersStore';

import * as s from 'components/orders/ordersStatistics/ordersStatistics.scss';

interface IProps {
  ordersStore?: OrdersStore;
}

class State {
}

@inject('ordersStore')
@observer
export default class OrdersStatistics extends React.Component<IProps, State> {
  constructor(props) {
    super(props);
  }
  render() {
    const {
      grandTotal,
      averageAmount,
      count
    } = this.props.ordersStore;
    return (
      <div className={s.host}>
        <div className="statistics">
          <div className="statistics__column">
            <div>{grandTotal != null ? grandTotal : "0"}</div>
            <div>grand total</div>
          </div>
          <div className="statistics__column">
            <div>{count}</div>
            <div>orders</div>
          </div>
          <div className="statistics__column">
            <div>{averageAmount != null ? averageAmount : "0"}</div>
            <div>avg. amount</div>
          </div>
        </div>
      </div>
    );
  }
}