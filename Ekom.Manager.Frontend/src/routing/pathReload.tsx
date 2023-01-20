import * as React from 'react';

export class PathReload extends React.Component {
  componentWillMount() {
    if (window) {
      window.location.reload();
    }
  }

  render() {
    return <div />;
  }
}
