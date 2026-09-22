import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';
export interface Scarf {
  fabricDescription?: string | null;
  suitableFor?: string | null;
  opacity?: string | null;
  slip?: string | null;
  breathability?: string | null;
  stretch?: string | null;
  season?: string | null;
  bobo?: string | null;
  images?: string[];
  size?: string;
  groupKey?: string;
  color?: string | null;
  inStock?: boolean;
  id: number;
  name: string;
  price: number;
  imageUrl: string;
  category: string;
  originalPrice?: number;
  onSale?: boolean;
  summerCollection?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class ScarfService {

  private readonly apiBaseUrl = '';
  private readonly apiUrl = `${this.apiBaseUrl}/api/products`;
  constructor(private http: HttpClient) { }

  checkAvailability(id: number, quantity: number) {
    return this.http.get<{available:boolean;message:string|null}>(this.apiUrl+'/'+id+'/availability', {params:{quantity}});
  }

  getScarves(): Observable<Scarf[]> {
    return this.http.get<Scarf[]>(this.apiUrl).pipe(
      map(scarves =>
        scarves.map(scarf => ({
          ...scarf,
          images:(scarf.images?.length?scarf.images:[scarf.imageUrl]).map(url=>url.startsWith('http')?url:this.apiBaseUrl+url),
          imageUrl: scarf.imageUrl.startsWith('http')
            ? scarf.imageUrl
            : `${this.apiBaseUrl}${scarf.imageUrl}`
        }))
      )
    );
  }
}
