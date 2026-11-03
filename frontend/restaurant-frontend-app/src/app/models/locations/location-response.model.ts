export interface LocationResponse {
  id: string;
  address: string | null;
  description: string;
  totalCapacity: number;
  averageOccupancy: string;
  imageUrl: string | null;
  rating: number;
}
