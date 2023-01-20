import * as React from 'react';
import styled from 'styled-components';
import OutsideClickHandler from 'react-outside-click-handler';

import { observer, inject } from 'mobx-react';
import { Link } from 'react-router-dom';

import ActivityLog from 'containers/order/components/ActivityLog';
import RootStore from 'stores/rootStore';
import OrdersStore from 'stores/ordersStore';
import * as variables from 'styles/variablesJS';
import Button from 'components/Button';

const path = '/umbraco/backoffice/ekom';

const ActivityWindowWrapper = styled.div`
  position: absolute;
  overflow: auto;
  z-index: 10;
  right:0;
  top:0;
  height:100vh;
  background-color: ${variables.white};
  width: 450px;
  display:flex;
  justify-content: center;
  box-shadow: -2.01285px -2.01285px 15.0964px rgba(0, 0, 0, 0.05);

  .activityWindow-close {
    float: right;
    margin-top: 10px;
  }
`;

const Table = styled.div`
  width: 100%;
  flex-basis: 100%;
  display:flex;
  flex-direction: column;
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

interface IActivityWindowLog {
  type: string;
  data?: any;
  ordersStore?: OrdersStore;
  rootStore?: RootStore;

}

@inject('rootStore', 'ordersStore')
@observer
class ActivityLogWindow extends React.Component<IActivityWindowLog> {
  public static defaultProps: Partial<IActivityWindowLog> = {
  };
  public constructor(props: IActivityWindowLog) {
    super(props);
  }
  public handleClose = () => {
    this.props.rootStore.showActivityLogWindow = false;
    this.props.rootStore.activityLogWindowType = "";
    this.props.rootStore.activityLogWindowData = null;
  }
  public render() {
    const { type, data } = this.props;
    return (
      <OutsideClickHandler onOutsideClick={() => this.handleClose()}>
        <ActivityWindowWrapper>
          <div>
            <Button className="activityWindow-close" type="button" onClick={this.handleClose} backgroundColor={variables.primaryColor} color={variables.white}>Close</Button>
            {type === "CurrentOrder" ? (
              <ActivityLog activityLogs={data} />
            )
              : (
                <>
                  <h3>Síðustu pantanir sem þú vannst í</h3>
                  <Table>
                    {data && data.map(log =>
                      <TableRow key={log.Key}>
                        <TableCol><Link to={`${path}/manager/order/${log.Key}`}>{log.OrderNumber}</Link></TableCol>
                        <TableCol>{log.UserName}</TableCol>
                      </TableRow>
                    )}
                  </Table>
                </>
              )}
          </div>
        </ActivityWindowWrapper>
      </OutsideClickHandler>
    )
  }
}

export default ActivityLogWindow;