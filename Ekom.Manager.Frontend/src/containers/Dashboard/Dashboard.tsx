import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';

import OrdersStore from 'stores/ordersStore';
import * as variables from 'styles/variablesJS';

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
      // fetchLatestActivityLogByUser 
    } = this.props.ordersStore
    fetchLatestActivityLogs();

    /**
     * Some how get userId from umbraco....
     */
    // getActivityLogByUser(userId)
  }
  render() {
    const { 
      latestActivityLog, 
      // latestUserActivityLog
    } = this.props.ordersStore;
    console.log(latestActivityLog)
    return (
      <DashboardWrapper>
        <Container>
          <TablesWrapper>
            <Table>
              <h3>Síðustu pantanir sem þú vannst í</h3>
            </Table>
            <Table>
              <h3>Síðustu pantanir sem var unnið í</h3>
              {latestActivityLog && latestActivityLog.map(log => 
                <TableRow key={log.UniqueId}>
                  <TableCol>{log.UniqueId}</TableCol>
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
