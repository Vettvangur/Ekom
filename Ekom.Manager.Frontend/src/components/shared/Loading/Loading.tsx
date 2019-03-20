import * as React from 'react';
import styled from 'styled-components';
import classNames from 'classnames/bind';

import * as s from './Loading.scss';


const LoadingWrapper = styled.div`
	/* background-color: rgba(#000, .6);
	overflow: hidden;
  position: absolute;
	top: 0;
	bottom: 0;
	left: 0;
	right: 0;
	height: 100%;
	width: 100%;
  z-index: 9999999; */
`;

const LoadingUl = styled.ul`
	/* position: absolute;
	left: calc(50% - 0.7em);
	top: calc(50% - 4.2em);
	display: inline-block;
  text-indent:$size*2;
  &::after {
    content:"";
    display: block;
    width: $size;
    height: $size;
    background-color: #fff;
    border-radius: 100%;
    position: absolute;
    top: $size*2;
  } */
`;

const LoadingLi = styled.li`
  /* position: absolute;
  padding-bottom: $size*4;
  top: 0;
  left: 0;
  &::after {
    content:"";
    display: block;
    width: $size;
    height: $size;
    background-color: #fff;
    border-radius: 100%;
  } */
`;

const cx = classNames.bind(s);

const Loading = () => 
  <LoadingWrapper className={cx({ 'peeek-loading': true })}>
    <LoadingUl>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
      <LoadingLi></LoadingLi>
    </LoadingUl>
  </LoadingWrapper>

export default Loading;