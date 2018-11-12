import * as React from 'react';
import styled from 'styled-components';

import * as variables from 'styles/variablesJS';

const StyledSearch = styled.div`
  display:flex;
  flex-wrap: wrap;
  background-color: ${variables.secondaryColor}80;
`;

const StyledSearchInputWrapper = styled.div`
  flex: 1 1 0;
  position: relative;
  min-width: 720px;
  &:not(:last-child) {
    &::after {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      right:0;
      position: absolute;
      opacity: .07;
    }
  }
`;

const StyledInput = styled.input`
  padding: 22px 20px;
  background-color: transparent;
  border:0;
`;

const StyledButtonFilter = styled.div`
  padding: 25px 30px;
  position: relative;
  &:not(:last-child) {
    &::after {
      content: '';
      border: 1px solid #414C41;
      top:0;
      height:100%;
      right:0;
      position: absolute;
      opacity: .07;
    }
  }
`

interface ISearchProps {
  
}

class Search extends React.Component<ISearchProps> {
  public render() {
    return (
      <StyledSearch>
        <StyledSearchInputWrapper>
          <StyledInput placeholder="Search orders..."/>
        </StyledSearchInputWrapper>
        <StyledButtonFilter>
          All stores
        </StyledButtonFilter>
        <StyledButtonFilter>
          This Month
        </StyledButtonFilter>
        <StyledButtonFilter>
          Advanced Filters
        </StyledButtonFilter>
      </StyledSearch>
    )
  }
}

export default Search;