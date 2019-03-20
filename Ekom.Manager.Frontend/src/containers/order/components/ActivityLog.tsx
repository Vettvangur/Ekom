import * as React from 'react';
import * as moment from 'moment';
import styled from 'styled-components';
import Divider from './Divider';
import { IActivityLog } from 'models/Ekom/activityLog';
import * as variables from 'styles/variablesJS';
import Button from 'components/Button';


const ActivityLogsWrapper = styled.div`
  width:100%;
  @media screen and (min-width: 1340px) {
    max-width: 370px;    
  }
`;

const UnorderedList = styled.ul``;

const ActivityLogItemWrapper = styled.li`
`;

const ActivityLogLabel = styled.div`
  color: ${variables.black};
`;

const ActivityLogLabelUser = styled.span`
  font-weight: 600;
`

const ActivityLogDate = styled.span`
  opacity: .5;
`;

const ActivityLogDivider = styled(Divider)`
  margin: 10px 0;
`;
interface IActivityLogProps {
  activityLogs: IActivityLog[];
  showMore?: boolean;
  openActivityLogWindow?: any;
}

class ActivityLog extends React.Component<IActivityLogProps> {
  constructor(props: IActivityLogProps) {
    super(props);
  }

  public render() {
    const {
      activityLogs,
      showMore
    } = this.props;
    return (
      <ActivityLogsWrapper>
        <h3>Activity log</h3>
        {activityLogs && activityLogs.length > 0 && (
          <UnorderedList>
            {activityLogs.map((log, index) =>
              <ActivityLogItemWrapper key={index}>
                <ActivityLogLabel className="fs-14 lh-20"><ActivityLogLabelUser>{log.UserName}: </ActivityLogLabelUser>{log.Log}</ActivityLogLabel>
                <ActivityLogDate className="fs-12 lh-17">{moment(log.Date).format("hh:mm | DD.MM.YYYY")}</ActivityLogDate>
                <ActivityLogDivider />
              </ActivityLogItemWrapper>
            )}
          </UnorderedList>
        )}
        {showMore && (
          <Button type="button" onClick={this.props.openActivityLogWindow}>Show More</Button>
        )}
      </ActivityLogsWrapper>
    );
  }
}

export default ActivityLog;
