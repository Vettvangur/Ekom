import * as React from 'react';

const format = (price, currency, locale = '', store = '') => {
  if (currency && (currency.toLowerCase() === "isk" || currency.toLowerCase() === "kr"))
    return (
      <>{price.toLocaleString('is-IS')} kr.</>
    )
  if (locale && currency)
    return new Intl.NumberFormat([locale, 'is-IS'], {
      style: 'currency',
      currency
    }).format(price)
  if (store && currency)
    return new Intl.NumberFormat([store, 'is-IS'], {
      style: 'currency',
      currency
    }).format(price)
  return (
    <>
      {currency} {price.toLocaleString('us')}
    </>
  )
}

interface IProps {
  price: number;
  currency: string;
  locale?: string;
  store?: string;
}

const defaultProps: IProps = {
  price: 0,
  currency: null,
  locale: null,
  store: null
}

const Total: React.SFC<IProps> = ({ price, currency, locale, store }) => {
  return (
    <React.Fragment>
      {format(price, currency, locale, store)}
    </React.Fragment>
  );
};

Total.defaultProps = defaultProps;

export default Total