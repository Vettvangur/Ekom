import * as React from 'react';
import styled from 'styled-components';
import statusList from 'utilities/statusList';
import { Select } from 'components/Input';
import { Container } from 'styledComponents/global';
import * as variables from 'styles/variablesJS';


const SelectedOrdersWrapper = styled.div`
  position:absolute;
  left:0;
  top: -56px;
`;
const SelectedOrdersForm = styled.form`
  display:flex;
`;

const SelectedCount = styled.span`
  color: ${variables.primaryColor};
  margin-right:50px;
`;


interface ISelectedOrdersProps {
  count: number;
  orders: string[];
}

class SelectedOrders extends React.Component<ISelectedOrdersProps> {
  constructor(props: ISelectedOrdersProps) {
    super(props);
  }
  public render() {
    const { count } = this.props;
    return (
      <SelectedOrdersWrapper>
        <Container>
          <SelectedOrdersForm>
            <SelectedCount className="fs-20 lh-26 semi-bold">{count} {count > 1 ? 'orders' : 'order'} selected</SelectedCount>
            <Select options={statusList} radius={50} />
          </SelectedOrdersForm>
        </Container>
      </SelectedOrdersWrapper>
    )
  }
}
export default SelectedOrders;