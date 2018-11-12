import * as React from 'react';
import * as moment from 'moment';
import ReactTable from 'react-table';
import styled from 'styled-components';
import { Link } from 'react-router-dom';
import { observer, inject } from 'mobx-react';

import OrdersStore from 'stores/ordersStore';
import statusList from 'utilities/statusList';
import { Checkbox } from 'components/Input';
import Total from 'components/shared/total';
import Pagination from 'components/Pagination';

const path = '/umbraco/backoffice/ekom';


const TableWrapper = styled.div`
  /* height: calc(100vh - 240px); */
  flex: 1 1 0;
  overflow-y:auto;
  position:relative;
  display:flex;
  flex-direction:column;
`;

interface ITableProps {
  ordersStore?: OrdersStore;
}

class State {
  selected: any;
  selectAll?: boolean = false;
  statusUpdateIndicator?: boolean = false;
}

@inject('ordersStore')
@observer
class Table extends React.Component<ITableProps, State> {

  constructor(props: ITableProps) {
    super(props);

    this.state = new State();

    this.toggleRow = this.toggleRow.bind(this);
  }

  defaultFilter = (filter, row) => {
    return String(row[filter.id]).includes(filter.value);
  }

  toggleSelectAll = () => {
    const { selectAll } = this.state;
    const { orders } = this.props.ordersStore;
    const newSelected = {};

    if (selectAll === false) {
      orders.forEach((x) => {
        newSelected[x.UniqueId] = true;
      });
    }

    this.setState({
      selected: newSelected,
      selectAll: selectAll === false,
    });
  }

  toggleRow(UniqueId) {
    const { selected } = this.state;
    const newSelected = Object.assign({}, selected);
    newSelected[UniqueId] = !selected[UniqueId];
    this.setState({
      selected: newSelected,
      selectAll: false,
    });
  }


  updateStatus(e, UniqueId) {
    const {
      ordersStore,
    } = this.props;
    const orderStatus = e.target.value;
    this.setState({
      statusUpdateIndicator: true,
    });
    ordersStore.updateStatus(UniqueId, orderStatus).then(() => {
      setTimeout(() => {
        this.setState({
          statusUpdateIndicator: false,
        });
      }, 1500);
    })
      .catch((err) => {
        this.setState({
          statusUpdateIndicator: false,
        });
        console.error(`error: ${err}`);
      });
  }


  render() {
    const {
      selectAll,
      //statusUpdateIndicator,
    } = this.state;

    const { ordersStore } = this.props;

    const columns = [
      {
        id: 'checkbox',
        accessor: '',
        maxWidth: 30,
        resizable: false,
        Cell: () => {
          //const UniqueId = original.UniqueId
          return (
            <Checkbox
            //onChange={() => this.toggleRow(UniqueId)}
            />
          )
        },
        Header: () => (
          <Checkbox
            checked={selectAll}
            onChange={() => this.toggleSelectAll()}
          />
        ),
        sortable: false,
        width: 45,
      },
      {
        Header: 'Order Number',
        id: 'orderNumber',
        accessor: d => ({
          UniqueId: d.UniqueId,
          OrderNumber: d.OrderNumber,
        }),
        filterMethod: (filter, row) => {
          if (String(row.orderNumber.OrderNumber.toLowerCase())
            .includes(filter.value.toLowerCase())) {
            return row;
          }
          if (String(row.orderNumber.UniqueId.toLowerCase()).includes(filter.value.toLowerCase())) {
            return row;
          }
          return row;
        },
        Cell: row => (
          <Link to={`${path}/manager/order/${row.value.UniqueId}`}>
            {row.value.OrderNumber}
          </Link>
        ),
      },
      {
        Header: 'Status',
        id: 'status',
        accessor: d => ({
          UniqueId: d.UniqueId,
          OrderStatus: d.OrderStatus,
        }),
        filterMethod: (filter, row) => {
          if (filter.value === 'all') {
            return true;
          }
          if (filter.value === '0') {
            if (row.status.OrderStatus === 0) {
              return row;
            }
          }
          if (filter.value === '1') {
            if (row.status.OrderStatus === 1) {
              return row;
            }
          }
          if (filter.value === '2') {
            if (row.status.OrderStatus === 2) {
              return row;
            }
          }
          if (filter.value === '3') {
            if (row.status.OrderStatus === 3) {
              return row;
            }
          }
          if (filter.value === '4') {
            if (row.status.OrderStatus === 4) {
              return row;
            }
          }
          if (filter.value === '5') {
            if (row.status.OrderStatus === 5) {
              return row;
            }
          }
          if (filter.value === '6') {
            if (row.status.OrderStatus === 6) {
              return row;
            }
          }
          if (filter.value === '7') {
            if (row.status.OrderStatus === 7) {
              return row;
            }
          }
          if (filter.value === '8') {
            if (row.status.OrderStatus === 8) {
              return row;
            }
          }
          if (filter.value === '9') {
            if (row.status.OrderStatus === 9) {
              return row;
            }
          }
          if (filter.value === '10') {
            if (row.status.OrderStatus === 10) {
              return row;
            }
          }
          if (filter.value === '11') {
            if (row.status.OrderStatus === 11) {
              return row;
            }
          }
          if (filter.value === '12') {
            if (row.status.OrderStatus === 12) {
              return row;
            }
          }
          return row;
        },
        Filter: ({ filter, onChange }) => (
          <React.Fragment>
            <div className="rt-resizable-header-content" />
            <div className="rt-th-filter">
              <select
                onChange={event => onChange(event.target.value)}
                style={{
                  width: '100%',
                  background: 'rgba(75, 141, 166, 0.1)',
                  borderRadius: '10px',
                  border: 0,
                }}
                defaultValue={filter ? filter.value : 'all'}
              >
                <option value="all">
                  Show All
                </option>
                {statusList.map(status => (
                  <option key={status.id} value={status.id}>
                    {status.value}
                  </option>
                ))}
              </select>
            </div>
          </React.Fragment>
        ),
        Cell: row => (
          <div className="select__wrapper">
            <select
              style={{
                background: 'rgba(75, 141, 166, 0.1)',
                borderRadius: '10px',
                border: 0,
              }}
              onChange={event => this.updateStatus(event, row.value.UniqueId)}
              defaultValue={row.value.OrderStatus}
            >
              {statusList.map((status) => {
                if (row.value.OrderStatus === status.id) {
                  return (
                    <option key={status.id} value={status.id}>
                      {status.value}
                    </option>
                  );
                }
                return (
                  <option key={status.id} value={status.id}>
                    {status.value}
                  </option>
                );
              })}
            </select>
          </div>
        ),
      },
      {
        Header: 'Name',
        accessor: 'CustomerName',
      },
      {
        Header: 'Paid',
        accessor: 'PaidDate',
        Cell: (row) => {
          if (row.value === '0001-01-01T00:00:00' || row.value === null) {
            return (
              <div className="reactTable__payStatus reactTable__payStatus-close">
                <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.2447 6.35867C14.7713 6.01467 14.9987 5.336 14.75 4.73333C14.5 4.13 13.8593 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0873 2.246C12.626 1.78533 11.9113 1.73467 11.392 2.09067C11.5227 1.47467 11.2033 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.97467 1.08933C8.85933 0.469333 8.31933 0 7.66667 0C7.01333 0 6.47267 0.469333 6.35733 1.08867C6.014 0.560667 5.33533 0.334 4.732 0.584C4.128 0.833333 3.80933 1.474 3.93933 2.09C3.42067 1.73467 2.70667 1.78467 2.24467 2.246C1.78333 2.70733 1.73333 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.56 6.01467 1.08733 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468667 8.86 1.08733 8.97533C0.56 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73333 11.9127 1.78333 12.6267 2.24467 13.088C2.70667 13.5493 3.42 13.6 3.93933 13.2447C3.80933 13.86 4.12933 14.5 4.732 14.75C5.33467 15 6.014 14.7733 6.35733 14.2453C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85933 14.8647 8.97467 14.2453C9.31733 14.7733 9.996 14.9987 10.6 14.75C11.2027 14.5 11.522 13.86 11.392 13.2433C11.9113 13.6 12.626 13.5493 13.0873 13.088C13.548 12.6267 13.5993 11.9127 13.242 11.3933C13.858 11.524 14.5 11.204 14.75 10.6013C15 9.998 14.772 9.31867 14.2447 8.97533C14.8633 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33337)" stroke="#F25555" strokeMiterlimit="10" strokeLinejoin="round" />
                  <path d="M0 0L6 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                  <path d="M6 0L0 6" transform="translate(6 6)" stroke="#F25555" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
                <span>
                  Unpaid
                </span>
                <svg width="8" height="8" viewBox="0 0 8 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                  <circle cx="4" cy="4" r="3.5" stroke="#F25555" />
                </svg>
              </div>
            );
          }
          return (
            <div className="reactTable__payStatus reactTable__payStatus-check">
              <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M6.66667 0L1.66667 4.66667L0 3" transform="translate(5.66669 6.66675)" stroke="#53BC50" strokeMiterlimit="10" strokeLinecap="round" strokeLinejoin="round" />
                <path d="M15.3333 7.66667C15.3333 7.014 14.8633 6.47333 14.244 6.35867C14.7713 6.01467 14.998 5.336 14.75 4.73333C14.5 4.13 13.8587 3.81 13.242 3.94133C13.5987 3.422 13.548 2.70733 13.0867 2.246C12.6253 1.78533 11.9107 1.73467 11.392 2.09067C11.5227 1.47467 11.2027 0.834667 10.5993 0.584667C9.99667 0.334667 9.31733 0.561333 8.974 1.08933C8.85867 0.469333 8.31933 0 7.66667 0C7.01267 0 6.47267 0.469333 6.35667 1.08867C6.014 0.560667 5.33467 0.334 4.73133 0.584C4.128 0.833333 3.80867 1.474 3.93867 2.09C3.42 1.73467 2.706 1.78467 2.244 2.246C1.78267 2.70733 1.73267 3.42133 2.08867 3.94067C1.47267 3.81 0.834 4.13 0.584 4.73333C0.334 5.33667 0.559333 6.01467 1.08667 6.358C0.467333 6.47333 0 7.014 0 7.66667C0 8.31933 0.468 8.86 1.08667 8.97533C0.559333 9.31933 0.334 9.99867 0.583333 10.6013C0.833333 11.2033 1.47267 11.5233 2.08867 11.3933C1.73267 11.9127 1.78333 12.6267 2.244 13.088C2.70533 13.5493 3.41933 13.6 3.93867 13.244C3.80867 13.8593 4.12867 14.4993 4.73133 14.7493C5.334 14.9993 6.014 14.7727 6.35667 14.2447C6.47267 14.8647 7.014 15.3333 7.66667 15.3333C8.31933 15.3333 8.85867 14.8647 8.974 14.2453C9.31733 14.7733 9.996 14.9987 10.5993 14.75C11.202 14.5 11.522 13.86 11.3913 13.2433C11.91 13.6 12.6247 13.5493 13.086 13.088C13.5473 12.6267 13.5987 11.9127 13.2413 11.3933C13.8573 11.524 14.4993 11.204 14.7493 10.6013C14.9993 9.998 14.7713 9.31867 14.2433 8.97533C14.8627 8.86067 15.3333 8.31933 15.3333 7.66667Z" transform="translate(1.33331 1.33331)" stroke="#53BC50" strokeMiterlimit="10" strokeLinejoin="round" />
              </svg>
              <span>
                Paid
              </span>
              <svg width="8" height="8" viewBox="0 0 8 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                <circle cx="4" cy="4" r="3.5" stroke="#53BC50" />
              </svg>
            </div>
          );
        },
      },
      {
        Header: 'Email',
        accessor: 'CustomerEmail',
      },
      {
        Header: 'Store',
        accessor: 'StoreAlias',
      },
      {
        Header: 'Country',
        accessor: ('ShippingCountry' || 'CustomerCountry'),
        id: 'country',
        filterMethod: (filter, row) => {
          if (filter.value === 'all') {
            return true;
          }
          if (filter.value === 'Iceland') {
            return row[filter.CustomerCountry] === 'IS';
          }
          return false;
        },
        Filter: ({ filter, onChange }) => (
          <div className="select__wrapper">
            <select
              onChange={event => onChange(event.target.value)}
              style={
                {
                  width: '100%',
                  background: 'rgba(75, 141, 166, 0.1)',
                  borderRadius: '10px',
                }
              }
              defaultValue={filter ? filter.value : 'all'}
            >
              <option value="all">
                Show All countries
              </option>
              <option value="0">
                Albania
              </option>
              <option value="1">
                Andorra
              </option>
              <option value="2">
                Armenia
              </option>
              <option value="3">
                Algeria
              </option>
              <option value="4">
                Argentina
              </option>
              <option value="5">
                Australia
              </option>
              <option value="6">
                Austria
              </option>
              <option value="7">
                Azerbaijan
              </option>
              <option value="8">
                Belarus
              </option>
              <option value="9">
                Belgium
              </option>
              <option value="10">
                Bosnia And Herzegovina
              </option>
              <option value="11">
                Brazil
              </option>
              <option value="12">
                Bulgaria
              </option>
              <option value="13">
                Canada
              </option>
              <option value="14">
                Chile
              </option>
              <option value="15">
                China
              </option>
              <option value="16">
                Croatia (Local Name: Hrvatska)
              </option>
              <option value="17">
                Cuba
              </option>
              <option value="18">
                Cyprus
              </option>
              <option value="19">
                Czech Republic
              </option>
              <option value="20">
                Denmark
              </option>
              <option value="21">
                Egypt
              </option>
              <option value="22">
                Estonia
              </option>
              <option value="23">
                Finland
              </option>
              <option value="24">
                France
              </option>
              <option value="25">
                Georgia
              </option>
              <option value="26">
                Germany
              </option>
              <option value="27">
                Greece
              </option>
              <option value="28">
                Greenland
              </option>
              <option value="29">
                Haiti
              </option>
              <option value="30">
                Hong Kong
              </option>
              <option value="31">
                Hungary
              </option>
              <option value="32">
                Iceland
              </option>
              <option value="33">
                India
              </option>
              <option value="34">
                Indonesia
              </option>
              <option value="35">
                Iran, Islamic Republic Of
              </option>
              <option value="36">
                Iraq
              </option>
              <option value="37">
                Ireland
              </option>
              <option value="38">
                Israel
              </option>
              <option value="39">
                Italy
              </option>
              <option value="40">
                Jamaica
              </option>
              <option value="41">
                Japan
              </option>
              <option value="42">
                Jordan
              </option>
              <option value="43">
                Latvia
              </option>
              <option value="44">
                Liechtenstein
              </option>
              <option value="45">
                Lithuania
              </option>
              <option value="46">
                Luxembourg
              </option>
              <option value="47">
                Macedonia, The Former Yugoslav Republic Of
              </option>
              <option value="48">
                Malaysia
              </option>
              <option value="49">
                Malta
              </option>
              <option value="50">
                Mexico
              </option>
              <option value="51">
                Moldova, Republic Of
              </option>
              <option value="52">
                Monaco
              </option>
              <option value="53">
                Montenegro
              </option>
              <option value="54">
                Morocco
              </option>
              <option value="55">
                Namibia
              </option>
              <option value="56">
                Nepal
              </option>
              <option value="57">
                Netherlands
              </option>
              <option value="58">
                Netherlands Antilles
              </option>
              <option value="59">
                New Zealand
              </option>
              <option value="60">
                Norway
              </option>
              <option value="61">
                Paraguay
              </option>
              <option value="62">
                Peru
              </option>
              <option value="63">
                Philippines
              </option>
              <option value="64">
                Poland
              </option>
              <option value="65">
                Portugal
              </option>
              <option value="66">
                Qatar
              </option>
              <option value="67">
                Romania
              </option>
              <option value="68">
                Russian Federation
              </option>
              <option value="69">
                San Marino
              </option>
              <option value="70">
                Saudi Arabia
              </option>
              <option value="71">
                Singapore
              </option>
              <option value="72">
                Slovakia (Slovak Republic)
              </option>
              <option value="73">
                Slovenia
              </option>
              <option value="74">
                South Africa
              </option>
              <option value="75">
                Spain
              </option>
              <option value="76">
                Sudan
              </option>
              <option value="77">
                Suriname
              </option>
              <option value="78">
                Sweden
              </option>
              <option value="79">
                Switzerland
              </option>
              <option value="80">
                Taiwan, Province Of China
              </option>
              <option value="81">
                Thailand
              </option>
              <option value="82">
                Tunisia
              </option>
              <option value="83">
                Turkey
              </option>
              <option value="84">
                Ukraine
              </option>
              <option value="85">
                United Arab Emirates
              </option>
              <option value="86">
                United Kingdom
              </option>
              <option value="87">
                United States
              </option>
              <option value="88">
                Venezuela
              </option>
              <option value="89">
                Viet Nam
              </option>
            </select>
          </div>
        ),
      },
      {
        Header: 'Created',
        accessor: 'CreateDate',
        Cell: data => {
          return (
            <span>{moment(data.row.CreateDate).format('DD. MMM YYYY')}</span>
          )
        }
      },
      {
        id: 'Total',
        Header: 'Total',
        accessor: d => { return { TotalAmount: d.TotalAmount, Currency: d.Currency, StoreAlias: d.StoreAlias } },
        Cell: data => {
          return (
            <Total price={data.value.TotalAmount} currency={data.value.Currency} store={data.value.StoreAlias} />
          )
        }
      },
    ];
    const orders = ordersStore.orders;
    return (
      <ReactTable
        data={orders}
        defaultFilterMethod={this.defaultFilter}
        columns={columns}
        defaultPageSize={10}

        sorted={ordersStore.sorted}
        page={ordersStore.page}
        pageSize={ordersStore.pageSize}
        expanded={ordersStore.expanded}
        resized={ordersStore.resized}
        filtered={ordersStore.filtered}
        onSortedChange={sorted => ordersStore.onSortedChange(sorted)}
        onPageChange={page => ordersStore.onPageChange(page)}
        onPageSizeChange={(pageSize, page) => ordersStore.onPageSizeChange(pageSize, page)}
        onExpandedChange={expanded => ordersStore.onExpandedChange(expanded)}
        onResizedChange={resized => ordersStore.onResizedChange(resized)}
        onFilteredChange={filtered => ordersStore.onFilteredChange(filtered)}
        showPagination={false}

        loading={ordersStore.loading}
        className="-highlight bg-white"
        style={{
          border: 'none',
        }}
        getTheadProps={() => ({
          style: {
            boxShadow: '0 2px 0px 0 rgba(0,0,0,0.05)',
          },
        })}
        getTheadTrProps={() => ({
          style: {
            textAlign: 'left',
            fontSize: '14px',
            fontWeight: 600,
            color: 'rgba(44, 56, 44, 0.7)',
          },
        })}
        getTheadThProps={() => ({
          style: {
            borderRight: 'none',
          },
        })}
        getTrProps={() => {

          return {
            style: {
              alignItems: 'center',
            },
          };
        }}
        getTdProps={() => ({
          style: {
            borderRight: 'none',
          },
        })}
      >
        {(state, makeTable, instance) => {
          return (
            <>
              <TableWrapper>
                {makeTable()}
              </TableWrapper>
              <Pagination
                canPrevious={state.canPrevious}
                canNext={state.canNext}
                page={state.page}
                totalPages={state.pages}
                pageRows={state.pageRows}
                pageSize={state.pageSize}
                totalItems={state.data.length}
                pageSizeOptions={state.pageSizeOptions}
                onPageSizeChange={state.onPageSizeChange}
              />
            </>
          )
        }}
      </ReactTable>
    )
  }
}

export default Table;