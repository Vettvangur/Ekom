import React, { Component } from 'react';
import { Link } from 'react-router-dom';
import statusList from '../../utilities/statusList';
const path = '/umbraco/backoffice/ekom';
const columns = [
  {
    Header: 'Orders',
    columns: [
      {
        Header: 'Order Number',
        id: 'orderNumber',
        accessor: d => {
          return {
            UniqueId: d.UniqueId,
            OrderNumber: d.OrderNumber,
          }
        },
        Cell: row => (
          <Link to={`${path}/manager/order/${row.value.UniqueId}`}>{row.value.OrderNumber}</Link>
        )
      },
      {
        Header: 'Status',
        id: 'status',
        accessor: d => {
          return {
            UniqueId: d.UniqueId,
            OrderStatus: d.OrderStatus
          }
        },
        filterMethod: (filter, row) => {
          console.log(row)
          console.log(row);
          if (filter.value === "all") {
            return true;
          }
          if (filter.value == "0") {
            console.log("test")
            if (row.status.OrderStatus == 0) {
              return row
            }
          }
          
          return row[filter.id] == "0";
        },
        Filter: ({ filter, onChange }) =>
          <select
            onChange={event => onChange(event.target.value)}
            style={{ width: "100%" }}
            value={filter ? filter.value : "all"}
          >
            <option value="all">Show All</option>
            {statusList.map(status => {
                return <option key={status.id} value={status.id}>{status.value}</option>
            })}
          </select>,
        Cell: row => (
          <select 
            onChange={event => this.updateStatus(event, row.value.UniqueId)}
          >
            {statusList.map(status => {
              if (row.value.OrderStatus === status.id) {
                return <option key={status.id} selected value={status.id}>{status.value}</option>
              } else {
                 return <option key={status.id} value={status.id}>{status.value}</option>
              }
            })}
          </select>
        )
      },
      {
        Header: 'Email',
        accessor: 'CustomerEmail',
      },
      {
        Header: 'Name',
        accessor: 'CustomerName',
      },
      {
        Header: 'Country',
        accessor: 'CustomerCountry',
        id: 'country',
        filterMethod: (filter, row) => {
          console.log(row)
          if (filter.value === "all") {
            return true;
          }
          if (filter.value === "Iceland") {
            return row[filter.CustomerCountry] == "IS";
          }
        },
        Filter: ({ filter, onChange }) =>
          <select
            onChange={event => onChange(event.target.value)}
            style={{ width: "100%" }}
            value={filter ? filter.value : "all"}
          >
            <option value="all">Show All countries</option>
            <option value="0">Albania</option>
            <option value="1">Andorra</option>
            <option value="2">Armenia</option>
            <option value="3">Algeria</option>
            <option value="4">Argentina</option>
            <option value="5">Australia</option>
            <option value="6">Austria</option>
            <option value="7">Azerbaijan</option>
            <option value="8">Belarus</option>
            <option value="9">Belgium</option>
            <option value="10">Bosnia And Herzegovina</option>
            <option value="11">Brazil</option>
            <option value="12">Bulgaria</option>
            <option value="13">Canada</option>
            <option value="14">Chile</option>
            <option value="15">China</option>
            <option value="16">Croatia (Local Name: Hrvatska)</option>
            <option value="17">Cuba</option>
            <option value="18">Cyprus</option>
            <option value="19">Czech Republic</option>
            <option value="20">Denmark</option>
            <option value="21">Egypt</option>
            <option value="22">Estonia</option>
            <option value="23">Finland</option>
            <option value="24">France</option>
            <option value="25">Georgia</option>
            <option value="26">Germany</option>
            <option value="27">Greece</option>
            <option value="28">Greenland</option>
            <option value="29">Haiti</option>
            <option value="30">Hong Kong</option>
            <option value="31">Hungary</option>
            <option value="32">Iceland</option>
            <option value="33">India</option>
            <option value="34">Indonesia</option>
            <option value="35">Iran, Islamic Republic Of</option>
            <option value="36">Iraq</option>
            <option value="37">Ireland</option>
            <option value="38">Israel</option>
            <option value="39">Italy</option>
            <option value="40">Jamaica</option>
            <option value="41">Japan</option>
            <option value="42">Jordan</option>
            <option value="43">Latvia</option>
            <option value="44">Liechtenstein</option>
            <option value="45">Lithuania</option>
            <option value="46">Luxembourg</option>
            <option value="47">Macedonia, The Former Yugoslav Republic Of</option>
            <option value="48">Malaysia</option>
            <option value="49">Malta</option>
            <option value="50">Mexico</option>
            <option value="51">Moldova, Republic Of</option>
            <option value="52">Monaco</option>
            <option value="53">Montenegro</option>
            <option value="54">Morocco</option>
            <option value="55">Namibia</option>
            <option value="56">Nepal</option>
            <option value="57">Netherlands</option>
            <option value="58">Netherlands Antilles</option>
            <option value="59">New Zealand</option>
            <option value="60">Norway</option>
            <option value="61">Paraguay</option>
            <option value="62">Peru</option>
            <option value="63">Philippines</option>
            <option value="64">Poland</option>
            <option value="65">Portugal</option>
            <option value="66">Qatar</option>
            <option value="67">Romania</option>
            <option value="68">Russian Federation</option>
            <option value="69">San Marino</option>
            <option value="70">Saudi Arabia</option>
            <option value="71">Singapore</option>
            <option value="72">Slovakia (Slovak Republic)</option>
            <option value="73">Slovenia</option>
            <option value="74">South Africa</option>
            <option value="75">Spain</option>
            <option value="76">Sudan</option>
            <option value="77">Suriname</option>
            <option value="78">Sweden</option>
            <option value="79">Switzerland</option>
            <option value="80">Taiwan, Province Of China</option>
            <option value="81">Thailand</option>
            <option value="82">Tunisia</option>
            <option value="83">Turkey</option>
            <option value="84">Ukraine</option>
            <option value="85">United Arab Emirates</option>
            <option value="86">United Kingdom</option>
            <option value="87">United States</option>
            <option value="88">Venezuela</option>
            <option value="89">Viet Nam</option>
          </select>
      },
      {
        Header: 'Created',
        accessor: 'CreateDate',
      },
      {
        Header: 'Paid',
        accessor: 'PaidDate',
      },
      {
        Header: 'Total',
        accessor: 'TotalAmount',
      },
    ],
  },
];

export default columns;