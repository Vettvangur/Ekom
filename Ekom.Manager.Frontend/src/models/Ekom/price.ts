import { IValueAndCurrency } from 'models/Ekom/valueAndCurrency';
import { IStore } from 'models/Ekom/store';

export interface IPrice {
  Discount: any;
  Store: IStore;
  DiscountAlwaysBeforeVat: boolean;
  OriginalValue: number;
  Quantity: number;
  BeforeDiscount: IValueAndCurrency;
  AfterDiscount: IValueAndCurrency;
  WithoutVat: IValueAndCurrency;
  value: number;
  WithVat: IValueAndCurrency;
  Vat: IValueAndCurrency;
  DiscountAmount: IValueAndCurrency;
}