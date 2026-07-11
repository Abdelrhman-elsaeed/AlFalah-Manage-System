import { Component, ElementRef, OnInit, ViewChild, AfterViewInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';
import { HttpClient } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { environment } from '../../../../environments/environment';
import SignaturePad from 'signature_pad';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [CommonModule, TranslateModule, ButtonModule],
  templateUrl: './account-settings.component.html',
  styleUrls: ['./account-settings.component.css']
})
export class AccountSettingsComponent implements OnInit, AfterViewInit {
  @ViewChild('signaturePad', { static: true }) signaturePadElement!: ElementRef<HTMLCanvasElement>;

  private signaturePad!: SignaturePad;
  isSaving = false;
  existingSignatureDataUrl: string | null = null;

  private readonly http = inject(HttpClient);
  private readonly toast = inject(ToastService);

  ngOnInit(): void {}

  ngAfterViewInit(): void {
    this.signaturePad = new SignaturePad(this.signaturePadElement.nativeElement, {
      backgroundColor: 'rgba(255, 255, 255, 0)',
      penColor: '#000000'
    });

    this.loadSignature();
  }

  loadSignature() {
    this.http.get<any>(`${environment.apiUrl}/api/v1/account/signature`).subscribe({
      next: (res) => {
        if (res.isSuccess && res.data?.signatureDrawnData) {
          this.existingSignatureDataUrl = res.data.signatureDrawnData;
          this.signaturePad.fromDataURL(res.data.signatureDrawnData);
        }
      },
      error: (err) => {
        console.error('Failed to load signature', err);
      }
    });
  }

  clearSignature() {
    this.signaturePad.clear();
  }

  saveSignature() {
    if (this.signaturePad.isEmpty()) {
      // Allow saving an empty signature (clearing it)
    }

    const dataUrl = this.signaturePad.isEmpty() ? null : this.signaturePad.toDataURL('image/png');
    this.isSaving = true;

    this.http.put<any>(`${environment.apiUrl}/api/v1/account/signature`, {
      signatureDrawnData: dataUrl
    }).subscribe({
      next: (res) => {
        this.isSaving = false;
        if (res.isSuccess) {
          if (dataUrl) {
            this.existingSignatureDataUrl = dataUrl;
          } else {
            this.existingSignatureDataUrl = null;
          }
          this.toast.success(res.message || 'تم حفظ التوقيع بنجاح');
        } else {
          this.toast.error(res.errors?.[0] || 'تعذر حفظ التوقيع');
        }
      },
      error: (err) => {
        this.isSaving = false;
        this.toast.error('حدث خطأ أثناء الحفظ.');
        console.error(err);
      }
    });
  }
}
