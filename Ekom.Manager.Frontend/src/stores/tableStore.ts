import { observable, action } from 'mobx';

import { Resize, Filter, SortingRule } from 'react-table';

export default class TableStore {
  @observable page = 0;
  @observable pageSize = 10;
  @observable pageRows = [];
  @observable sorted: SortingRule[];
  @observable expanded: any;
  @observable resized: Resize[];
  @observable filtered: Filter[];

  selectedRows = observable.map<string, boolean>();

  constructor() {
  }
  
  @action
  handlePageSize(pageSize) {
    this.pageSize = pageSize;
  }

  @action
  setPageRows(pageRows) {
    this.pageRows = pageRows;
  }
  @action
  onSortedChange = (sorted) => {
    this.sorted = sorted;
  }
  @action
  onPageChange = (page) => {
    this.page = page;
  }
  @action
  onPageSizeChange = (pageSize, page) => {
    this.pageSize = pageSize;
    this.page = page;
  }
  @action
  onExpandedChange = (expanded) => {
    this.expanded = expanded;
  }
  @action
  onResizedChange = (resized) => {
    this.resized = resized;
  }
  @action
  onFilteredChange = (filtered) => {
    this.filtered = filtered;
  }

}