import * as React from 'react';
import classNames from 'classnames/bind';

import * as s from 'components/order/products/products.scss';

const cx = classNames.bind(s);

interface IProps {
  orderlines: any;
  orderTotal: any;
}

export default class OrderContainer extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  public render() {
    const {
      orderlines,
      orderTotal,
    } = this.props;

    const columns = [
      {
        Header: 'Product',
        classAccessor: 'productCol',
      },
      {
        Header: 'Quantity',
        classAccessor: 'quantityCol',
      },
      {
        Header: 'Price',
        classAccessor: 'priceCol',
      },
      {
        Header: 'Total',
        classAccessor: 'totalCol',
      },
    ];
    return (
      <div className={s.products}>

        <div className={s.container}>

          <div>

            <div className={s.thead}>
              {columns.map((col, index) => (
                <div
                  key={index}
                  className={cx({
                    column: true,
                    [`${[col.classAccessor]}`]: true,
                  })}
                >
                  {col.Header}
                </div>
              ))}
            </div>

            <div className={s.tbody}>
              {orderlines.map((orderline, index) => (
                <div
                  key={index}
                  className={s.row}
                >
                  <div
                    className={cx({
                      column: true,
                      productCol: true,
                    })}
                  >
                    {orderline.Product.Title}
                  </div>
                  <div
                    className={cx({
                      column: true,
                      quantityCol: true,
                    })}
                  >
                    {orderline.Quantity}
                  </div>
                  <div
                    className={cx({
                      column: true,
                      totalCol: true,
                    })}
                  >
                    {orderline.Product.Price.WithVat.CurrencyString}
                  </div>
                  <div
                    className={cx({
                      column: true,
                    })}
                  >
                    {orderline.Amount.WithVat.CurrencyString}
                  </div>
                </div>
              ))}
            </div>
            <div className={s.tfooter}>
              <div
                className={cx({
                  column: true,
                  productCol: true,
                })}
              />
              <div
                className={cx({
                  column: true,
                })}
              />
              <div
                className={cx({
                  column: true,
                  productCol: true,
                })}
              >
                Total
            </div>
              <div
                className={cx({
                  column: true,
                })}
              >
                {orderTotal}
              </div>
            </div>

          </div>

        </div>

      </div>
    );
  };
}