import * as React from 'react';
import * as variables from 'styles/variables';
import styled, { keyframes } from 'styled-components';



const loading = keyframes`
  0% {
      left: 50%;
      width: 0;
      z-index: 100
  }
  33% {
      left: 0;
      width: 100%;
      z-index: 10
  }
  100% {
      left: 0;
      width: 100%
  }
`;


const SavingBarWrapper = styled.div`
  position: absolute;
  width: 100%;
  height: 4px;
  background-color: #fff;
  bottom:0;
  left:0;
`;

const Bar = styled.div`
  content: "";
  display: inline;
  position: absolute;
  width: 0;
  height: 100%;
  left: 50%;
  text-align: center;
  &:nth-child(1) {
    background-color: ${variables.secondaryColor};
    -webkit-animation: ${loading} 3s linear infinite;
    animation: ${loading} 3s linear infinite
  }
  &:nth-child(2) {
    background-color: ${variables.white};
    -webkit-animation: ${loading} 3s linear 1s infinite;
    animation: ${loading} 3s linear 1s infinite
  }
  &:nth-child(3) {
    background-color: ${variables.secondaryColor};
    -webkit-animation: ${loading} 3s linear 2s infinite;
    animation: ${loading} 3s linear 2s infinite
  }
`;
interface IProps {
  error?: boolean;
}
const SavingLoader: React.SFC<IProps> = ({ error }) => {
  if (error) {
    <SavingBarWrapper>
    </SavingBarWrapper>
  }

  return (
    <SavingBarWrapper>
      <Bar/>
      <Bar/>
      <Bar/>
    </SavingBarWrapper>
  );
}

export default SavingLoader;
