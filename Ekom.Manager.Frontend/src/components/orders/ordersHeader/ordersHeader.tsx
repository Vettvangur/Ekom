import * as React from 'react';
import styled from 'styled-components';

import SearchForm from 'components/orders/searchForm';
import SavingLoader from 'components/order/savingLoader';

const SearchWrapper = styled.div`
  position:relative;
  margin-bottom: 6.25rem;
`;
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
      <SearchWrapper>
        {statusUpdateIndicator
          && <SavingLoader />
        }
        <SearchForm />
      </SearchWrapper>
    );
  }
}