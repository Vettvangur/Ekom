import { observable, action } from 'mobx';

import { Resize, Filter, SortingRule } from 'react-table';

export default class TableStore {
  @observable page: number;
  @observable pageSize: number;
  @observable sorted: SortingRule[];
  @observable expanded: any;
  @observable resized: Resize[];
  @observable filtered: Filter[];

  constructor() {
    this.page = 0;
    this.pageSize = 10;
  }
  
  @action
  handlePageSize(pageSize) {
    this.pageSize = pageSize;
  }

  @action
  onSortedChange(sorted) {
    this.sorted = sorted;
  }
  @action
  onPageChange(page) {
    this.page = page;
  }
  @action
  onPageSizeChange(pageSize, page) {
    this.pageSize = pageSize;
    this.page = page;
  }
  @action
  onExpandedChange(expanded) {
    this.expanded = expanded;
  }
  @action
  onResizedChange(resized) {
    this.resized = resized;
  }
  @action
  onFilteredChange(filtered) {
    this.filtered = filtered;
  }

}