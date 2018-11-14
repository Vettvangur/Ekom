import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import TableStore from 'stores/tableStore';

import Button from 'components/Button';
import Icon from 'components/Icon';

import * as variables from 'styles/variablesJS';

const PaginationWrapper = styled.div`
  display:flex;
  justify-content: space-between;
  width:100%;
  left:0;
  bottom: 0;
  padding: 23px 30px;
`;

const PaginationColumn = styled.div`
display:flex;
`;


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

const PaginationCurrentPageWrapper = styled.div`
  margin: 0 1.25rem;
`;
const PaginationCurrentPage = styled.span``;

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
  tableStore?: TableStore;
}

@inject('tableStore')
@observer
class Pagination extends React.Component<IPaginationProps> {
  public constructor(props: IPaginationProps) {
    super(props);
  }

  public renderPager = () => {
    return (
      <>
        <Button
          onClick={() => this.props.tableStore.onPageChange(this.props.page - 1)}
          disabled={this.props.canPrevious ? false : true}
          className="fs-12 lh-16"
          paddingLeft={13}
          paddingRight={12}
          paddingTop={9}
          paddingBottom={9}
          borderRadius="3px"
          iconPos="left"
          center
        >
          <Icon name="arrow-left-sm" iconSize={12} marginRight={10} />
          Fyrri
        </Button>

        <PaginationCurrentPageWrapper>Síða <PaginationCurrentPage>{this.props.page + 1}</PaginationCurrentPage> af {this.props.totalPages}</PaginationCurrentPageWrapper>
        <Button
          onClick={() => this.props.tableStore.onPageChange(this.props.page + 1)}
          disabled={this.props.canNext ? false : true}
          className="fs-12 lh-16"
          paddingLeft={13}
          paddingRight={12}
          paddingTop={9}
          paddingBottom={9}
          borderRadius="3px"
          iconPos="left"
          center
        >
          Næsta
          <Icon name="arrow-right-sm" iconSize={12} marginLeft={10} />
        </Button>
      </>
    )
  }

  public render() {
    return (
      <PaginationWrapper>
        <PaginationColumn>
          <span>Sýna</span>
          <PaginationRowSelectWrapper className="select__wrapper">
            <PaginationRowSelect value={this.props.pageSize} onChange={(e) => this.props.tableStore.handlePageSize(e.currentTarget.value)}>
              {this.props.pageSizeOptions.map((ps, psIndex) => (
                <option key={psIndex} value={ps}>{ps}</option>
              ))}
            </PaginationRowSelect>
          </PaginationRowSelectWrapper>
          pantanir af {this.props.totalItems}
        </PaginationColumn>
        <PaginationColumn className="align-center">
          {this.renderPager()}
        </PaginationColumn>
      </PaginationWrapper>
    )
  }
}
export default Pagination;