export interface IOrders {
  UniqueId: string;
  ReferenceId: number;
  OrderInfo: string;
  OrderNumber: string;
  OrderStatusCol: number;
  OrderStatus: number;
  CustomerEmail: string;
  CustomerName: string;
  CustomerId: number;
  CustomerUsername: string | null;
  ShippingCountry: string | null;
  TotalAmount: number;
  Currency: string;
  StoreAlias: string;
  CreateDate: string;
  UpdateDate: string;
  PaidDate: string;
}