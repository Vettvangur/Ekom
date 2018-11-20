
import { IShipping } from 'models/Ekom/shipping';
export interface ICustomerInformation {
  CustomerIpAddress: string;
  Customer: ICustomer;
  Shipping: IShipping;
}
export interface ICustomer {
  Properties: ICustomerProperties;
  Name: string;
  FirstName: string;
  LastName: string;
  Email: string;
  Address: string;
  City: string;
  Apartment: string;
  Country: string;
  ZipCode: string;
  Phone: string;
  UserId: string;
  UserName: string;
}
export interface ICustomerProperties {
  customerName: string;
  customerEmail: string;
  customerCountry: string;
  customerCity: string;
  customerAddress: string;
  customerZipCode: string;
  customerPhone: string;
  customerComment: string;
}
