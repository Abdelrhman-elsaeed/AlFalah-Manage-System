import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { StudentAnalyzerDelegate, StudentAnalyzerModel, StudentAnalyzerProvider, StudentAnalyzerSettings, UpdateStudentAnalyzerSettingsRequest } from '../../core/models/student-analyzer.models';
import { StudentAnalyzerService } from '../../core/services/student-analyzer.service';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-student-analyzer-settings',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './student-analyzer-settings.component.html',
  styleUrls: ['./student-analyzer-settings.component.css']
})
export class StudentAnalyzerSettingsComponent implements OnInit {
  private readonly api = inject(StudentAnalyzerService);
  private readonly toast = inject(ToastService);

  readonly provider = StudentAnalyzerProvider;
  readonly capabilities = signal({ canAccess: true, canDelegate: false, canManageSettings: true, schoolId: null as number | null, schoolName: null as string | null });
  readonly settings = signal<StudentAnalyzerSettings | null>(null);
  readonly delegates = signal<StudentAnalyzerDelegate[]>([]);
  readonly models = signal<StudentAnalyzerModel[]>([]);
  readonly loadingModels = signal(false);
  readonly saving = signal(false);
  readonly savingDelegates = signal(false);
  delegateSearch = '';

  activeProvider = StudentAnalyzerProvider.Groq;
  groqApiKey = '';
  groqModel = 'llama-3.3-70b-versatile';
  clearGroq = false;
  geminiApiKey = '';
  geminiModel = 'gemini-2.5-flash';
  clearGemini = false;
  openRouterApiKey = '';
  openRouterModel = 'openrouter/free';
  clearOpenRouter = false;

  ngOnInit(): void {
    forkJoin({ capabilities: this.api.capabilities(), settings: this.api.settings() }).subscribe({
      next: ({ capabilities, settings }) => {
        if (capabilities.data) this.capabilities.set(capabilities.data);
        if (settings.data) this.applySettings(settings.data);
        if (capabilities.data?.canDelegate) this.loadDelegates();
        this.loadModels();
      }
    });
  }

  providerChanged(): void { this.models.set([]); this.loadModels(); }

  loadModels(): void {
    const current = this.settings();
    const hasStored = this.activeProvider === 1 ? current?.hasGroqApiKey : this.activeProvider === 2 ? current?.hasGeminiApiKey : current?.hasOpenRouterApiKey;
    const typedKey = this.currentApiKey();
    if (this.activeProvider !== StudentAnalyzerProvider.OpenRouter && !hasStored && !typedKey) return;
    this.loadingModels.set(true);
    this.api.models(this.activeProvider, typedKey || undefined).pipe(finalize(() => this.loadingModels.set(false))).subscribe({
      next: response => { if (response.data) this.models.set(response.data); }
    });
  }

  save(): void {
    this.saving.set(true);
    const body: UpdateStudentAnalyzerSettingsRequest = {
      activeProvider: this.activeProvider,
      groqApiKey: this.groqApiKey.trim() || null, clearGroqApiKey: this.clearGroq, groqModel: this.groqModel.trim() || null,
      geminiApiKey: this.geminiApiKey.trim() || null, clearGeminiApiKey: this.clearGemini, geminiModel: this.geminiModel.trim() || null,
      openRouterApiKey: this.openRouterApiKey.trim() || null, clearOpenRouterApiKey: this.clearOpenRouter, openRouterModel: this.openRouterModel.trim() || null
    };
    this.api.updateSettings(body).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: response => {
        if (!response.data) return;
        this.applySettings(response.data);
        this.toast.success('تم حفظ الإعدادات', response.message || 'تم تحديث مزود الذكاء الاصطناعي بأمان.');
        this.api.capabilities(true).subscribe();
        this.loadModels();
      }
    });
  }

  loadDelegates(): void {
    this.api.delegates().subscribe({ next: response => { if (response.data) this.delegates.set(response.data); } });
  }

  toggleDelegate(userId: string, granted: boolean): void {
    this.delegates.update(items => items.map(item => item.userId === userId ? { ...item, isGranted: granted } : item));
  }

  saveDelegates(): void {
    this.savingDelegates.set(true);
    const ids = this.delegates().filter(item => item.isGranted).map(item => item.userId);
    this.api.updateDelegates(ids).pipe(finalize(() => this.savingDelegates.set(false))).subscribe({
      next: response => {
        if (response.data) this.delegates.set(response.data);
        this.toast.success('تم تحديث التفويض', response.message || 'تم حفظ الأشخاص المفوّضين.');
      }
    });
  }

  filteredDelegates(): StudentAnalyzerDelegate[] {
    const query = this.delegateSearch.trim().toLowerCase();
    return query ? this.delegates().filter(item => `${item.fullName} ${item.username} ${item.roles.join(' ')}`.toLowerCase().includes(query)) : this.delegates();
  }

  grantedDelegateCount(): number { return this.delegates().filter(item => item.isGranted).length; }

  currentModel(): string { return this.activeProvider === 1 ? this.groqModel : this.activeProvider === 2 ? this.geminiModel : this.openRouterModel; }
  setCurrentModel(model: string): void {
    if (this.activeProvider === 1) this.groqModel = model;
    else if (this.activeProvider === 2) this.geminiModel = model;
    else this.openRouterModel = model;
  }
  hasCurrentKey(): boolean {
    const settings = this.settings();
    const hasStored = this.activeProvider === 1 ? !!settings?.hasGroqApiKey : this.activeProvider === 2 ? !!settings?.hasGeminiApiKey : !!settings?.hasOpenRouterApiKey;
    return this.activeProvider === StudentAnalyzerProvider.OpenRouter || hasStored || !!this.currentApiKey();
  }
  providerName(value: StudentAnalyzerProvider): string { return value === 1 ? 'Groq' : value === 2 ? 'Gemini' : 'OpenRouter'; }

  private currentApiKey(): string {
    return (this.activeProvider === 1 ? this.groqApiKey : this.activeProvider === 2 ? this.geminiApiKey : this.openRouterApiKey).trim();
  }

  private applySettings(settings: StudentAnalyzerSettings): void {
    this.settings.set(settings);
    this.activeProvider = settings.activeProvider;
    this.groqModel = settings.groqModel || 'llama-3.3-70b-versatile';
    this.geminiModel = settings.geminiModel || 'gemini-2.5-flash';
    this.openRouterModel = settings.openRouterModel || 'openrouter/free';
    this.groqApiKey = ''; this.geminiApiKey = ''; this.openRouterApiKey = '';
    this.clearGroq = false; this.clearGemini = false; this.clearOpenRouter = false;
  }
}
