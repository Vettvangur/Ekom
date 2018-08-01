import React, { Component } from 'react';
import DashboardContainer from 'containers/dashboardContainer';
import Page from 'pages/Page';

class Dashboard extends Component {
  getMetaData() {
    return {
      title: this.pageTitle(),
      meta: this.pageMeta(),
      link: this.pageLink()
    };
  }

  pageTitle() {
    return 'Dashboard';
  };

  pageMeta() {
    return [
      { name: 'description', content: 'Dashboard' }
    ];
  };

  pageLink() {
    return [];
  };

  render() {
    return (
      <Page {...this.getMetaData()}>
        <DashboardContainer {...this.props} />
      </Page>
    );
  }
}

export default Dashboard;