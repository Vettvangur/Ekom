import * as React from 'react';
import styled from 'styled-components';
import classNames from 'classnames/bind';

import * as s from './style.scss';
import { IValueAndCurrency } from 'models/Ekom/valueAndCurrency';

const cx = classNames.bind(s);

const ProductsWrapper = styled.div``;


interface IProps {
  orderlines: any;
  orderTotal: any;
  orderDiscountAmount?: IValueAndCurrency;
}

export default class OrderContainer extends React.Component<IProps> {
  constructor(props: IProps) {
    super(props);
  }
  public render() {
    const {
      orderlines,
      orderTotal,
      orderDiscountAmount
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
      <ProductsWrapper className={s.products}>
        <h3>Order details</h3>
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
          {orderDiscountAmount && (
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
                Discount
            </div>
              <div
                className={cx({
                  column: true,
                })}
              >
                -{orderDiscountAmount.CurrencyString}
              </div>
            </div>
          )}
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

      </ProductsWrapper>
    );
  };
}
