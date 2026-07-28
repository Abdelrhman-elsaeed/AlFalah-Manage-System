import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, SimpleChanges, ViewChild } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import * as L from 'leaflet';
import { formatPublishedScore } from '../../score-scale';

export interface SchoolMapMarker {
  id: number;
  name: string;
  city: string;
  region: string | null;
  locationDetails: string | null;
  latitude: number;
  longitude: number;
  average: number | null;
}

@Component({
  selector: 'app-school-map',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './school-map.component.html',
  styleUrls: ['./school-map.component.css']
})
export class SchoolMapComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input() markers: SchoolMapMarker[] = [];
  @ViewChild('mapCanvas', { static: true }) private mapCanvas!: ElementRef<HTMLDivElement>;

  private map?: L.Map;
  private readonly markerLayer = L.layerGroup();
  private readonly saudiBounds = L.latLngBounds([15.8, 34.2], [32.7, 56.2]);

  ngAfterViewInit(): void {
    this.map = L.map(this.mapCanvas.nativeElement, {
      center: [23.8859, 45.0792],
      zoom: 5,
      minZoom: 4,
      maxZoom: 18,
      zoomControl: true,
      scrollWheelZoom: true,
      maxBounds: L.latLngBounds([13.5, 31.5], [35, 59]),
      maxBoundsViscosity: 0.72
    });

    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      noWrap: true,
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(this.map);

    this.markerLayer.addTo(this.map);
    this.renderMarkers();
    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['markers'] && this.map) this.renderMarkers();
  }

  ngOnDestroy(): void {
    this.map?.remove();
    this.map = undefined;
  }

  showAllSchools(): void {
    if (!this.map) return;
    const valid = this.validMarkers();
    if (valid.length === 0) {
      this.map.fitBounds(this.saudiBounds, { padding: [18, 18] });
      return;
    }
    this.map.flyToBounds(
      L.latLngBounds(valid.map(marker => [marker.latitude, marker.longitude] as L.LatLngTuple)),
      { padding: [56, 56], maxZoom: 10, duration: 0.7 }
    );
  }

  private renderMarkers(): void {
    if (!this.map) return;
    this.markerLayer.clearLayers();

    for (const marker of this.validMarkers()) {
      const point = L.circleMarker([marker.latitude, marker.longitude], {
        radius: 9,
        weight: 3,
        color: '#ffffff',
        fillColor: '#0f7132',
        fillOpacity: 1,
        className: 'school-leaflet-marker'
      });
      point.bindTooltip(this.tooltipContent(marker), {
        permanent: true,
        direction: 'top',
        offset: [0, -9],
        opacity: 1,
        className: 'school-map-tooltip'
      });
      point.on('click', () => this.map?.flyTo([marker.latitude, marker.longitude], 13, { duration: 0.75 }));
      point.addTo(this.markerLayer);
    }
  }

  private validMarkers(): SchoolMapMarker[] {
    return this.markers.filter(marker =>
      Number.isFinite(marker.latitude) && Number.isFinite(marker.longitude)
      && marker.latitude >= 16 && marker.latitude <= 33
      && marker.longitude >= 34 && marker.longitude <= 56);
  }

  private tooltipContent(marker: SchoolMapMarker): HTMLElement {
    const container = document.createElement('div');
    container.className = 'school-map-tooltip__content';
    container.dir = 'rtl';

    const name = document.createElement('strong');
    name.textContent = marker.name;
    const meta = document.createElement('small');
    meta.textContent = marker.average === null
      ? marker.city
      : `${marker.city} · ${formatPublishedScore(marker.average)}`;

    container.append(name, meta);
    return container;
  }
}
