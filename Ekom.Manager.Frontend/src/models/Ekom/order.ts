import { ICustomerInformation } from 'models/Ekom/customer';
import { IShipping } from 'models/Ekom/shipping';
import { IValueAndCurrency } from 'models/Ekom/valueAndCurrency';
import { IStore } from 'models/Ekom/store';
import { IOrderLine } from 'models/Ekom/orderline';
import { IProvider } from 'models/Ekom/provider';

export interface IOrderModel {
  StoreInfo: IStore;
  Discount: any;
  Coupon: any;
  UniqueId: string;
  ReferenceId: number;
  OrderNumber: string;
  OrderLines: IOrderLine[];
  ShippingProvider: IProvider;
  PaymentProvider: IProvider;
  TotalQuantity: number;
  CustomerInformation: ICustomerInformation;
  Shipping: IShipping;
  OrderLineTotal: IValueAndCurrency;
  SubTotal: IValueAndCurrency;
  Vat: IValueAndCurrency;
  GrandTotal: IValueAndCurrency;
  DiscountAmount: IValueAndCurrency;
  ChargedAmount: IValueAndCurrency;
  CreateDate: string;
  UpdateDate: string;
  PaidDate: string;
  OrderStatus: number;
  HangfireJobs: any;
}
