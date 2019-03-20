import { IPrice } from 'models/Ekom/price';

export interface IProduct {
  Properties: IProductProperties;
  Id: number;
  Key: string;
  SKU: string;
  Title: string;
  Price: IPrice;
  ImageIds: string[];
  ImageUrls: string[];
  VariantGroups: any;
}

export interface IProductProperties {
  id: string;
  key: string;
  parentID: string;
  level: string;
  writerID: string;
  creatorID: string;
  nodeType: string;
  template: string;
  sortOrder: string;
  createDate: string;
  updateDate: string;
  nodeName: string;
  urlName: string;
  writerName: string;
  creatorName: string;
  nodeTypeAlias: string;
  path: string;
  categories: string;
  disable: string;
  enableBackorder: string;
  images: string;
  price: string;
  primaryVariantGroup: string;
  sku: string;
  slug: string;
  title: string;  
}