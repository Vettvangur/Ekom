import React, { Component } from 'react';

export default class OrderContainer extends Component {
  constructor(props) {
    super(props);
    this.state = {
      order: null,
      status: null,
    };
  }

  render() {
    return (
      <div className="content" />
    );
  }
}
