export interface IShipping {
  Properties: IShippingProperties;
  Name: string;
  FirstName: string;
  LastName: string;
  Address: string;
  City: string;
  Apartment: string;
  Country: string;
  ZipCode: string;
}
export interface IShippingProperties {
  shippingName: string;
  shippingCountry: string;
  shippingCity: string;
  shippingAddress: string;
  shippingZipCode: string;
}
