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

  state = {};

  async FetchNode(nodeId) {

    const response = await fetch('/umbraco/backoffice/ekom/managerapi/GetNodeName?nodeId=' + nodeId);
    const json = await response.json();

    var newNodeId = nodeId.substring(17, nodeId.length - 17);

    this.setState({
      [newNodeId]: json
    });

  };

  async componentDidMount() {

    this.props.orderlines.filter(line => line.Product.Properties.hasOwnProperty("stores")).forEach((orderline, index) =>{
        this.FetchNode(orderline.Product.Properties.stores);
    })

  };

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
                  {orderline.Product.Title} - {orderline.Product.SKU}<br/>

                  {orderline.Product.VariantGroups.map((variantGroup) => (
                    <div>
                      - Variant<br/>
                      {variantGroup.Title}

                      {variantGroup.Variants.map((variant) => (
                        <span> - Size: { variant.Properties.size} SKU: { variant.Properties.sku}</span>
                       ))}

                      {orderline.Product.Properties.hasOwnProperty("stores") && orderline.Product.Properties.stores && orderline.Product.Properties.stores.length >= 17 ? <div>
                        <br/>
                        <span>Store:  </span>
                        <strong>{ this.state[orderline.Product.Properties.stores.substring(17, orderline.Product.Properties.stores.length - 17)] }</strong>
                      </div> : null}

                      </div>
                    ))}

          
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
