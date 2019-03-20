import { IProduct } from 'models/Ekom/product'
import { IPrice } from 'models/Ekom/price'

export interface IOrderLine {
  ProductKey: string;
  Key: string;
  Product: IProduct;
  Quantity: number;
  Discount: any;
  Coupon: any;
  Amount: IPrice;
}
