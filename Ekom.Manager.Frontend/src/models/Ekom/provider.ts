import { IPrice } from 'models/Ekom/price'

export interface IProvider {
  Id: number;
  Key: string;
  Title: string;
  Price: IPrice;
}