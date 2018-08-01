import React, { Component } from 'react';
import OrdersContainer from 'containers/ordersContainer';
import Page from 'pages/Page';

class Orders extends Component {
  getMetaData() {
    return {
      title: this.pageTitle(),
      meta: this.pageMeta(),
      link: this.pageLink()
    };
  }

  pageTitle() {
    return 'Orders';
  };

  pageMeta() {
    return [
      { name: 'description', content: 'Orders' }
    ];
  };

  pageLink () {
    return [];
  };

  render() {
    return (
      <Page {...this.getMetaData()}>
        <OrdersContainer {...this.props} />
      </Page>
    );
  }
}

export default Orders;