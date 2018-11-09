import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import OrdersStore from 'stores/ordersStore';

import * as variables from 'styles/variablesJS';

const PaginationWrapper = styled.div`
  display:flex;
  justify-content: space-between;
  position: absolute;
  width:100%;
  left:0;
  bottom: 0;
  padding: 23px 30px;
`;

const PaginationColumn = styled.div`
`;

const Button = styled.button``;

const PaginationPrev = styled(Button)``;
const PaginationNext = styled(Button)``;

const PaginationRowSelectWrapper = styled.div`
  display:inline-block;
  margin: 0 7px;
  &:after {
    top: 14px;
    right: 7px;
  }
`;
const PaginationRowSelect = styled.select`
  width: 3.5rem;
  padding:0 0.3125rem;
  height: 1.625rem;
  background-color: ${variables.secondaryColor};
  border-radius: 3px;

`;

interface IPaginationProps {
  totalItems: number;
  totalPages: number;
  page: number;
  pageRows: any;
  pageSize: number;
  canPrevious: boolean;
  canNext: boolean;
  pageSizeOptions: number[];
  onPageSizeChange: (pageSize: number, page: number) => void;
  ordersStore?: OrdersStore;
}

@inject('ordersStore')
@observer
class Pagination extends React.Component<IPaginationProps> {
  public constructor(props: IPaginationProps) {
    super(props);
  }

  public renderPager = () => {
    return (
      <>
        {this.props.canPrevious && (
          <PaginationPrev onClick={() => this.props.ordersStore.onPageChange(this.props.page - 1)}>prev</PaginationPrev>
        )}
        {this.props.canNext && (
          <PaginationNext onClick={() => this.props.ordersStore.onPageChange(this.props.page + 1)}>next</PaginationNext>
        )}
      </>
    )
  }

  public render() {
    console.log(this.props.ordersStore.pageSize)
    return (
      <PaginationWrapper>
        <PaginationColumn>
          <span>Sýna</span>
          <PaginationRowSelectWrapper className="select__wrapper">
            <PaginationRowSelect value={this.props.pageSize} onChange={(e) => this.props.ordersStore.handlePageSize(e.currentTarget.value)}>
              {this.props.pageSizeOptions.map(ps => (
                <option value={ps}>{ps}</option>
              ))}
            </PaginationRowSelect>
          </PaginationRowSelectWrapper>
          pantanir af {this.props.totalItems}
        </PaginationColumn>
        <PaginationColumn>
          {this.renderPager()}
        </PaginationColumn>
      </PaginationWrapper>
    )
  }
}
export default Pagination;