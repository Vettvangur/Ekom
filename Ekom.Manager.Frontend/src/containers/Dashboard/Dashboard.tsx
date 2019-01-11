import * as React from 'react';
import styled from 'styled-components';
import { observer, inject } from 'mobx-react';
import { Link } from 'react-router-dom';
import OrdersStore from 'stores/ordersStore';
import RootStore from 'stores/rootStore';
import * as variables from 'styles/variablesJS';
import Button from 'components/Button';

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

const TableWrapper = styled.div`
  width: 33.333%;
  flex-basis: 33.333%;
  &:not(:last-child) {
    padding-right: 40px;
  }
`


const Table = styled.div`
  display:flex;
  flex-direction: column;
  margin-bottom: 40px;
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
  rootStore?: RootStore;
}

@inject('ordersStore', 'rootStore')
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
  public openActivityLogWindow = (data) => {
    this.props.rootStore.activityLogWindowType = "OrderList";
    this.props.rootStore.activityLogWindowData = data
    this.props.rootStore.showActivityLogWindow = true;

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
            <TableWrapper>
              <h3>Síðustu pantanir sem þú vannst í</h3>
              <Table>
                {latestUserActivityLog && latestUserActivityLog.slice(0, 12).map(log =>
                  <TableRow key={log.Key}>
                    <TableCol><Link to={`${path}/manager/order/${log.Key}`}>{log.OrderNumber}</Link></TableCol>
                    <TableCol>{log.UserName}</TableCol>
                  </TableRow>
                )}
              </Table>
              {latestUserActivityLog && latestUserActivityLog.length > 12 && (
                <Button type="button" onClick={() => this.openActivityLogWindow(latestUserActivityLog)}>Show More</Button>
              )}
            </TableWrapper>
            <TableWrapper>
              <h3>Síðustu pantanir sem var unnið í</h3>
              <Table>
                {latestActivityLog && latestActivityLog.slice(0, 12).map(log =>
                  <TableRow key={log.Key}>
                    <TableCol><Link to={`${path}/manager/order/${log.Key}`}>{log.OrderNumber}</Link></TableCol>
                    <TableCol>{log.UserName}</TableCol>
                  </TableRow>
                )}
              </Table>
              {latestActivityLog && latestActivityLog.length > 12 && (
                <Button type="button" onClick={() => this.openActivityLogWindow(latestActivityLog)}>Show More</Button>
              )}
            </TableWrapper>
            <TableWrapper>
              <h3>Nýjustu pantanir</h3>
              <Table>
              </Table>
            </TableWrapper>
          </TablesWrapper>
        </Container>
      </DashboardWrapper>
    )
  }
}
