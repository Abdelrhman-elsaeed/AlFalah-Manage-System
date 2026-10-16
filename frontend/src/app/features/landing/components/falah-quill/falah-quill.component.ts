import { Component } from '@angular/core';

@Component({
  selector: 'app-falah-quill',
  standalone: true,
  template: `<svg viewBox="0 0 420 76" fill="none" aria-hidden="true" focusable="false">
    <path class="quill-trail" d="M8 61C88 18 157 75 235 47C266 36 287 38 309 31" />
    <path class="quill-edge" d="M288 56C313 18 343 9 399 5C390 34 361 52 320 49M288 56L380 17M316 44L323 25M337 35L345 17M358 25L366 12" />
  </svg>`,
  styles: [`:host{display:block;width:156px;height:30px;color:var(--gold-700,#946800);pointer-events:none}svg{display:block;width:100%;height:100%;overflow:visible}.quill-trail{stroke:currentColor;stroke-width:1.6;stroke-linecap:round}.quill-edge{fill:color-mix(in srgb,currentColor 8%,transparent);stroke:currentColor;stroke-width:2;stroke-linecap:round;stroke-linejoin:round}`]
})
export class FalahQuillComponent {}
