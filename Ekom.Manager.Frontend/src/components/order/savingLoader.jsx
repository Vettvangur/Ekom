import React from 'react';
import classNames from 'classnames/bind';
import s from './savingLoader.scss';

const cx = classNames.bind(s);

const SavingLoader = () => (
  <div
    className={cx({
      'load-bar': true,
      'load-bar--small': true,
    })}
  >
    <div className={s.bar} />
    <div className={s.bar} />
    <div className={s.bar} />
  </div>
);

export default SavingLoader;
