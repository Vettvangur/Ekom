import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import { Link } from 'react-router-dom';
import OrdersStore from 'stores/ordersStore';
import * as variables from 'styles/variablesJS';

const path = '/umbraco/backoffice/ekom';

const DashboardWrapper = styled.div`

`;

const Container = styled.div`
  padding: 0 30px;
`;


const TablesWrapper = styled.div`
  display:flex;
  width: 100%;
`;

const Table = styled.div`
  width: 33.333%;
  flex-basis: 33.333%;
  display:flex;
  flex-direction: column;
  &:not(:last-child) {
    padding-right: 40px;
  }
`;

const TableRow = styled.div`
  display:flex;
  align-items:center;
  padding-bottom: 11px;
  border-bottom: 1px solid ${variables.dividerColor};
  &:not(:first-child) {
    padding-top: 10px;
  }
`;

const TableCol = styled.div`
  &:not(:last-child) {
    margin-right:40px;
  }
`;


interface IProps {
  ordersStore?: OrdersStore;
}

@inject('ordersStore')
@observer
export default class Dashboard extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  componentDidMount() {
    const {
      fetchLatestActivityLogs,
      fetchLatestActivityLogByUser 
    } = this.props.ordersStore
    fetchLatestActivityLogs();

    const userName = document.querySelector("#app").getAttribute("data-userName");
    fetchLatestActivityLogByUser(userName)
  }
  render() {
    const { 
      latestActivityLog, 
      latestUserActivityLog
    } = this.props.ordersStore;
    console.log("latestUserActivityLog", latestUserActivityLog)
    console.log(latestActivityLog)
    return (
      <DashboardWrapper>
        <Container>
          <TablesWrapper>
            <Table>
              <h3>Síðustu pantanir sem þú vannst í</h3>
              {latestUserActivityLog && latestUserActivityLog.map(log => 
                <TableRow key={log.Key}>
                <TableCol><Link to={`${path}/manager/order/${log.Key}`}>{log.OrderNumber}</Link></TableCol>
                <TableCol>{log.UserName}</TableCol>
                </TableRow>
               )}
            </Table>
            <Table>
              <h3>Síðustu pantanir sem var unnið í</h3>
              {latestActivityLog && latestActivityLog.map(log => 
                <TableRow key={log.Key}>
                  <TableCol><Link to={`${path}/manager/order/${log.Key}`}>{log.OrderNumber}</Link></TableCol>
                  <TableCol>{log.UserName}</TableCol>
                </TableRow>
               )}
            </Table>
            <Table>
              <h3>Nýjustu pantanir</h3>
            </Table>
          </TablesWrapper>
        </Container>
      </DashboardWrapper>
    )
  }
}
