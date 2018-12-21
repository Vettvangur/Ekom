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
  page: number;
  pages: number;
  pageRows: any;
  pageSize: number;
  canPrevious: boolean;
  canNext: boolean;
  pageSizeOptions: number[];
  onPageSizeChange: (pageSize: number, page: number) => void;
  onPageChange: any;
  tableStore?: TableStore;
}

@inject('tableStore')
@observer
class Pagination extends React.Component<IPaginationProps> {
  public constructor(props: IPaginationProps) {
    super(props);
  }

  public changePage(page) {
    const activePage = this.props.page + 1;

    if (page === activePage) {
      return;
    }

    this.props.onPageChange(page - 1);
  }

  changePageSize() {

  }

  public renderPager = () => {
    const activePage = this.props.page + 1
    return (
      <>
        <Button
          onClick={() => { 
            if (activePage === 1) return;
            this.changePage(activePage - 1);
          }}
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
          Previous
        </Button>

        <PaginationCurrentPageWrapper>Síða <PaginationCurrentPage>{activePage}</PaginationCurrentPage> af {this.props.pages}</PaginationCurrentPageWrapper>
        <Button
          onClick={() => {
            if (activePage === this.props.pages) return;
            this.changePage(activePage + 1);
          }}
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
          Next
          <Icon name="arrow-right-sm" iconSize={12} marginLeft={10} />
        </Button>
      </>
    )
  }

  public render() {
    return (
      <PaginationWrapper>
        <PaginationColumn>
          <span>Show</span>
          <PaginationRowSelectWrapper className="select__wrapper">
            <PaginationRowSelect value={this.props.pageSize} onChange={(e) => this.props.onPageSizeChange(+e.currentTarget.value, this.props.page)}>
              {this.props.pageSizeOptions.map((ps, psIndex) => (
                <option key={psIndex} value={ps}>{ps}</option>
              ))}
            </PaginationRowSelect>
          </PaginationRowSelectWrapper>
          orders of {this.props.totalItems}
        </PaginationColumn>
        <PaginationColumn className="align-center">
          {this.renderPager()}
        </PaginationColumn>
      </PaginationWrapper>
    )
  }
}
export default Pagination;
