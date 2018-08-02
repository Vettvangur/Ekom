import React from 'react';
import classNames from 'classnames/bind';
import styles from './savingLoader.scss';

const cx = classNames.bind(styles);

const SavingLoader = () => (
  <div
    className={cx({
      'load-bar': true,
      'load-bar--small': true,
    })}
  >
    <div className={styles.bar} />
    <div className={styles.bar} />
    <div className={styles.bar} />
  </div>
);

export default SavingLoader;
