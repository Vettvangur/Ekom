import * as React from 'react';
import styled from 'styled-components';


const InformationColumnWrapper = styled.div`

  min-width: 370px;
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
    <h3>{heading}</h3>
    <UnorderedList>
      {list.map(value => 
        <ListItem>{value}</ListItem>
      )}
    </UnorderedList>
  </InformationColumnWrapper>

export default InformationColumn;