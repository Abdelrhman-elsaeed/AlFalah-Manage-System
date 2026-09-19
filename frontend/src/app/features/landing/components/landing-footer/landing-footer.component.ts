import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
@Component({
  selector: 'app-landing-footer',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  templateUrl: './landing-footer.component.html',
  styleUrls: ['./landing-footer.component.css']
})
export class LandingFooterComponent {
  readonly year = new Date().getFullYear();
}
