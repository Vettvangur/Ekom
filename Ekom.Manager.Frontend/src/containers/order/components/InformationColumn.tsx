import * as React from 'react';
import styled from 'styled-components';


const InformationColumnWrapper = styled.div`
  width: 50%;
  margin-bottom: 50px;
`;

const UnorderedList = styled.ul``;

const ListItem = styled.li`
`;

interface IInformationColumnProps {
  heading: string;
  list: string[];
}

const InformationColumn: React.SFC<IInformationColumnProps> = ({ heading, list }) =>
  <InformationColumnWrapper>
    {list && list.length > 0 && (
      <>
        <h3 className="lh-31">{heading}</h3>
        <UnorderedList className="fs-16 lh-23">
          {list.map((value, index) =>
            <ListItem key={index} >{value}</ListItem>
          )}
        </UnorderedList>
      </>
    )}
  </InformationColumnWrapper>

export default InformationColumn;