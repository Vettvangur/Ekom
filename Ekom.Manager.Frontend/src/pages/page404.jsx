import React, { Component } from 'react';
import Page404Container from 'containers/page404Container';
import Page from 'pages/Page';

class Page404 extends Component {
  getMetaData() {
    return {
      title: this.pageTitle(),
      meta: this.pageMeta(),
      link: this.pageLink()
    };
  }

  pageTitle() {
    return 'Page404';
  };

  pageMeta() {
    return [
      { name: 'description', content: 'Page404' }
    ];
  };

  pageLink() {
    return [];
  };

  render() {
    return (
      <Page {...this.getMetaData()}>
        <Page404Container {...this.props} />
      </Page>
    );
  }
}

export default Page404;