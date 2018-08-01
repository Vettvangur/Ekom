import React, { Component } from 'react';
import OrderContainer from 'containers/orderContainer';
import Page from 'pages/Page';

class Order extends Component {
  getMetaData() {
    return {
      title: this.pageTitle(),
      meta: this.pageMeta(),
      link: this.pageLink()
    };
  }

  pageTitle() {
    return 'Order';
  };

  pageMeta() {
    return [
      { name: 'description', content: 'Order' }
    ];
  };

  pageLink() {
    return [];
  };

  render() {
    return (
      <Page {...this.getMetaData()}>
        <OrderContainer {...this.props} />
      </Page>
    );
  }
}

export default Order;