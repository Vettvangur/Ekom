import * as React from 'react';

import SearchForm from 'components/orders/searchForm';
import OrdersStatistics from 'components/orders/ordersStatistics';
import SavingLoader from 'components/order/savingLoader';

import * as s from 'components/orders/ordersHeader/ordersHeader.scss';

interface IProps {
  statusUpdateIndicator: boolean;
}

class State {
}

export default class OrdersHeader extends React.Component<IProps, State> {
  constructor(props) {
    super(props);

    this.state = new State();
  }
  render() {
    const {
      statusUpdateIndicator
    } = this.props;
    return (
      <div className={s.host}>
        {statusUpdateIndicator
          && <SavingLoader />
        }
        <SearchForm />
        <OrdersStatistics />
      </div>
    );
  }
}