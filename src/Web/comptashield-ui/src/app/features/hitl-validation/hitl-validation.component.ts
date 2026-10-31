import { Component, ElementRef, ViewChild, AfterViewInit, HostListener, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import Decimal from 'decimal.js';

export interface BoundingBoxField {
  fieldName: string;
  label: string;
  value: string;
  confidence: number; // 0.00 to 1.00
  box: { x: number; y: number; w: number; h: number };
}

@Component({
  selector: 'app-hitl-validation',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100">
      <!-- Toolbar -->
      <div class="bg-slate-900 border-b border-slate-800 px-6 py-2.5 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <span class="text-xs font-mono px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20 font-bold">
            HITL REVIEW QUEUE
          </span>
          <h1 class="text-sm font-semibold text-slate-200">Facture FAC-2026-0891 (Fournisseur: STE TUNISIENNE DE DISTRIBUTION)</h1>
        </div>

        <div class="flex items-center gap-2">
          <!-- Keyboard shortcut badges -->
          <div class="hidden lg:flex items-center gap-2 text-xs text-slate-400 mr-4 font-mono">
            <span><kbd class="px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700 text-slate-300">Enter</kbd> Champ suivant &lt;95%</span>
            <span><kbd class="px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700 text-slate-300">Ctrl+Enter</kbd> Valider</span>
          </div>

          <button 
            (click)="validateAndPost()"
            [disabled]="!isMathValid()"
            class="px-4 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white font-semibold text-xs transition-colors flex items-center gap-1.5 shadow-lg shadow-emerald-600/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
            Valider & Comptabiliser
          </button>
        </div>
      </div>

      <!-- Split Screen Layout -->
      <div class="flex-1 grid grid-cols-1 lg:grid-cols-12 overflow-hidden">
        <!-- Left Pane: HTML5 Canvas PDF / Document Viewer -->
        <div class="lg:col-span-7 bg-slate-900/60 p-4 flex flex-col border-r border-slate-800 overflow-hidden relative">
          <!-- Canvas Controls Overlay -->
          <div class="absolute top-6 left-6 z-10 flex items-center gap-2 bg-slate-900/90 backdrop-blur p-1 rounded-lg border border-slate-700 shadow-xl">
            <button (click)="zoom(0.1)" class="w-7 h-7 flex items-center justify-center rounded hover:bg-slate-800 text-slate-300 font-bold">+</button>
            <span class="text-xs font-mono text-slate-400 px-1">{{ (zoomLevel() * 100) | number:'1.0-0' }}%</span>
            <button (click)="zoom(-0.1)" class="w-7 h-7 flex items-center justify-center rounded hover:bg-slate-800 text-slate-300 font-bold">-</button>
            <button (click)="resetZoom()" class="px-2 py-0.5 text-xs rounded hover:bg-slate-800 text-slate-300 font-mono">Reset</button>
          </div>

          <!-- Color Code Legend -->
          <div class="absolute top-6 right-6 z-10 flex items-center gap-3 bg-slate-900/90 backdrop-blur px-3 py-1.5 rounded-lg border border-slate-700 text-[11px] font-mono shadow-xl">
            <div class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span> &ge;95% Conforme</div>
            <div class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span> 80-94% Incertain</div>
            <div class="flex items-center gap-1"><span class="w-2.5 h-2.5 rounded-full bg-rose-500"></span> &lt;80% / Erreur</div>
          </div>

          <!-- Canvas Viewport -->
          <div class="flex-1 overflow-auto rounded-xl border border-slate-800 bg-slate-950 flex items-center justify-center p-4">
            <canvas #docCanvas width="750" height="950" class="shadow-2xl rounded bg-white cursor-crosshair"></canvas>
          </div>
        </div>

        <!-- Right Pane: Reactive HITL Verification Form -->
        <div class="lg:col-span-5 bg-slate-950 p-6 flex flex-col overflow-y-auto">
          <!-- Cross-Sum Invariant Banner -->
          <div class="mb-6 p-4 rounded-xl border" [ngClass]="isMathValid() ? 'bg-emerald-950/20 border-emerald-500/30 text-emerald-300' : 'bg-rose-950/30 border-rose-500/40 text-rose-300'">
            <div class="flex items-center justify-between text-xs font-mono font-bold">
              <span>ÉQUILIBRE ARITHMÉTIQUE (HT + TVA + Timbre == TTC)</span>
              <span>{{ isMathValid() ? 'CONFORME (0.0000)' : 'ANOMALIE DÉTECTÉE' }}</span>
            </div>
            <div class="mt-2 text-xs flex items-center justify-between text-slate-400 font-mono">
              <span>{{ form.get('totalHT')?.value || 0 }} + {{ form.get('totalTVA')?.value || 0 }} + {{ form.get('timbreFiscal')?.value || 0 }} = </span>
              <span class="font-bold text-white">{{ calculatedTTC() }} TND</span>
              <span>(Extrait: {{ form.get('totalTTC')?.value || 0 }} TND)</span>
            </div>
          </div>

          <form [formGroup]="form" class="space-y-4 flex-1">
            <!-- Supplier Name -->
            <div class="space-y-1">
              <div class="flex items-center justify-between">
                <label class="text-xs font-semibold text-slate-300">Raison Sociale Fournisseur</label>
                <span [class]="getConfidenceBadgeClass('supplierName')">{{ getConfidenceText('supplierName') }}</span>
              </div>
              <input 
                type="text" 
                formControlName="supplierName" 
                (focus)="onFieldFocus('supplierName')"
                class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm text-slate-100 outline-none transition-all">
            </div>

            <!-- Matricule Fiscal -->
            <div class="space-y-1">
              <div class="flex items-center justify-between">
                <label class="text-xs font-semibold text-slate-300">Matricule Fiscal</label>
                <span [class]="getConfidenceBadgeClass('matriculeFiscal')">{{ getConfidenceText('matriculeFiscal') }}</span>
              </div>
              <input 
                type="text" 
                formControlName="matriculeFiscal" 
                (focus)="onFieldFocus('matriculeFiscal')"
                class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-mono text-slate-100 outline-none transition-all">
            </div>

            <!-- Invoice Number & Date -->
            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">N° Facture</label>
                  <span [class]="getConfidenceBadgeClass('invoiceNumber')">{{ getConfidenceText('invoiceNumber') }}</span>
                </div>
                <input 
                  type="text" 
                  formControlName="invoiceNumber" 
                  (focus)="onFieldFocus('invoiceNumber')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-mono text-slate-100 outline-none transition-all">
              </div>

              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Date Facture</label>
                  <span [class]="getConfidenceBadgeClass('invoiceDate')">{{ getConfidenceText('invoiceDate') }}</span>
                </div>
                <input 
                  type="date" 
                  formControlName="invoiceDate" 
                  (focus)="onFieldFocus('invoiceDate')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm text-slate-100 outline-none transition-all">
              </div>
            </div>

            <!-- Financial Totals (Strict Decimal Precision) -->
            <div class="grid grid-cols-2 gap-3 pt-2">
              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Total HT (TND)</label>
                  <span [class]="getConfidenceBadgeClass('totalHT')">{{ getConfidenceText('totalHT') }}</span>
                </div>
                <input 
                  type="number" step="0.0001" 
                  formControlName="totalHT" 
                  (focus)="onFieldFocus('totalHT')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-mono text-slate-100 outline-none text-right">
              </div>

              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Total TVA (TND)</label>
                  <span [class]="getConfidenceBadgeClass('totalTVA')">{{ getConfidenceText('totalTVA') }}</span>
                </div>
                <input 
                  type="number" step="0.0001" 
                  formControlName="totalTVA" 
                  (focus)="onFieldFocus('totalTVA')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-mono text-slate-100 outline-none text-right">
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Droit de Timbre</label>
                  <span [class]="getConfidenceBadgeClass('timbreFiscal')">{{ getConfidenceText('timbreFiscal') }}</span>
                </div>
                <input 
                  type="number" step="0.0001" 
                  formControlName="timbreFiscal" 
                  (focus)="onFieldFocus('timbreFiscal')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-mono text-slate-100 outline-none text-right">
              </div>

              <div class="space-y-1">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Total TTC (TND)</label>
                  <span [class]="getConfidenceBadgeClass('totalTTC')">{{ getConfidenceText('totalTTC') }}</span>
                </div>
                <input 
                  type="number" step="0.0001" 
                  formControlName="totalTTC" 
                  (focus)="onFieldFocus('totalTTC')"
                  class="w-full bg-slate-900 border border-slate-700 focus:border-sky-500 rounded-lg px-3 py-2 text-sm font-bold font-mono text-emerald-400 outline-none text-right">
              </div>
            </div>
          </form>

          <!-- Audit Decision Actions -->
          <div class="mt-6 pt-4 border-t border-slate-800 flex items-center justify-between gap-3">
            <button 
              type="button"
              class="px-3 py-2 rounded-lg bg-rose-500/10 hover:bg-rose-500/20 text-rose-400 border border-rose-500/30 text-xs font-semibold transition-colors">
              Mettre en Quarantaine
            </button>
            <button 
              type="button"
              class="px-3 py-2 rounded-lg bg-sky-500/10 hover:bg-sky-500/20 text-sky-400 border border-sky-500/30 text-xs font-semibold transition-colors">
              Demander Justificatif Client
            </button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class HitlValidationComponent implements AfterViewInit {
  @ViewChild('docCanvas') canvasRef!: ElementRef<HTMLCanvasElement>;

  readonly zoomLevel = signal(1.0);
  readonly selectedField = signal<string | null>('totalTTC');

  // OCR bounding boxes with confidence
  readonly fields: BoundingBoxField[] = [
    { fieldName: 'supplierName', label: 'Fournisseur', value: 'STE TUNISIENNE DE DISTRIBUTION', confidence: 0.98, box: { x: 50, y: 70, w: 320, h: 40 } },
    { fieldName: 'matriculeFiscal', label: 'Matricule Fiscal', value: '1234567/A/M/000', confidence: 0.96, box: { x: 50, y: 120, w: 220, h: 30 } },
    { fieldName: 'invoiceNumber', label: 'N° Facture', value: 'FAC-2026-0891', confidence: 0.97, box: { x: 450, y: 70, w: 220, h: 35 } },
    { fieldName: 'invoiceDate', label: 'Date', value: '2026-03-15', confidence: 0.95, box: { x: 450, y: 120, w: 180, h: 30 } },
    { fieldName: 'totalHT', label: 'Total HT', value: '34000.0000', confidence: 0.99, box: { x: 450, y: 750, w: 240, h: 30 } },
    { fieldName: 'totalTVA', label: 'Total TVA', value: '6460.0000', confidence: 0.96, box: { x: 450, y: 790, w: 240, h: 30 } },
    { fieldName: 'timbreFiscal', label: 'Timbre Fiscal', value: '1.0000', confidence: 0.91, box: { x: 450, y: 830, w: 240, h: 30 } }, // Low confidence (0.91 < 0.95)
    { fieldName: 'totalTTC', label: 'Total TTC', value: '40461.0000', confidence: 0.88, box: { x: 450, y: 870, w: 240, h: 40 } } // Low confidence (0.88 < 0.95)
  ];

  form: FormGroup;

  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      supplierName: ['STE TUNISIENNE DE DISTRIBUTION', Validators.required],
      matriculeFiscal: ['1234567/A/M/000', Validators.required],
      invoiceNumber: ['FAC-2026-0891', Validators.required],
      invoiceDate: ['2026-03-15', Validators.required],
      totalHT: [34000.0000, Validators.required],
      totalTVA: [6460.0000, Validators.required],
      timbreFiscal: [1.0000, Validators.required],
      totalTTC: [40461.0000, Validators.required]
    });
  }

  ngAfterViewInit(): void {
    this.renderCanvas();
  }

  // Live Decimal Cross-Sum Calculation
  calculatedTTC(): string {
    try {
      const ht = new Decimal(this.form.get('totalHT')?.value || 0);
      const tva = new Decimal(this.form.get('totalTVA')?.value || 0);
      const timbre = new Decimal(this.form.get('timbreFiscal')?.value || 0);
      return ht.plus(tva).plus(timbre).toFixed(4);
    } catch {
      return '0.0000';
    }
  }

  isMathValid(): boolean {
    try {
      const calc = new Decimal(this.calculatedTTC());
      const extracted = new Decimal(this.form.get('totalTTC')?.value || 0);
      return calc.equals(extracted);
    } catch {
      return false;
    }
  }

  onFieldFocus(fieldName: string): void {
    this.selectedField.set(fieldName);
    this.renderCanvas();
  }

  // Keyboard Navigation: Enter jumps to next field with confidence < 0.95
  @HostListener('window:keydown', ['$event'])
  handleKeyboardEvent(event: KeyboardEvent): void {
    if (event.ctrlKey && event.key === 'Enter') {
      event.preventDefault();
      this.validateAndPost();
    } else if (event.key === 'Enter' && !(event.target as HTMLElement).tagName.match(/BUTTON/i)) {
      event.preventDefault();
      this.jumpToNextLowConfidenceField();
    }
  }

  jumpToNextLowConfidenceField(): void {
    const lowConfidenceFields = this.fields.filter(f => f.confidence < 0.95);
    if (lowConfidenceFields.length === 0) return;

    const current = this.selectedField();
    const currentIndex = lowConfidenceFields.findIndex(f => f.fieldName === current);
    const nextIndex = (currentIndex + 1) % lowConfidenceFields.length;
    const nextField = lowConfidenceFields[nextIndex];

    this.selectedField.set(nextField.fieldName);
    const el = document.querySelector(`input[formControlName="${nextField.fieldName}"]`) as HTMLInputElement;
    el?.focus();
    this.renderCanvas();
  }

  zoom(delta: number): void {
    this.zoomLevel.update(z => Math.max(0.5, Math.min(2.5, z + delta)));
    this.renderCanvas();
  }

  resetZoom(): void {
    this.zoomLevel.set(1.0);
    this.renderCanvas();
  }

  validateAndPost(): void {
    if (this.isMathValid()) {
      alert(`Facture validée et comptabilisée avec succès pour ${this.calculatedTTC()} TND.`);
    }
  }

  getConfidenceText(fieldName: string): string {
    const f = this.fields.find(x => x.fieldName === fieldName);
    return f ? `${(f.confidence * 100).toFixed(0)}%` : '100%';
  }

  getConfidenceBadgeClass(fieldName: string): string {
    const f = this.fields.find(x => x.fieldName === fieldName);
    const conf = f?.confidence ?? 1;
    if (conf >= 0.95) return 'text-[11px] font-mono px-1.5 py-0.2 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
    if (conf >= 0.80) return 'text-[11px] font-mono px-1.5 py-0.2 rounded bg-amber-500/20 text-amber-400 border border-amber-500/30 font-bold';
    return 'text-[11px] font-mono px-1.5 py-0.2 rounded bg-rose-500/20 text-rose-400 border border-rose-500/30 font-bold animate-pulse';
  }

  private renderCanvas(): void {
    if (!this.canvasRef) return;
    const canvas = this.canvasRef.nativeElement;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const scale = this.zoomLevel();

    // Clear canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Draw invoice simulation background
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Header & Lines
    ctx.fillStyle = '#1e293b';
    ctx.font = 'bold 18px monospace';
    ctx.fillText('FACTURE COMMERCIALE (TEIF XML / OCR)', 50 * scale, 40 * scale);

    // Render Bounding Boxes
    for (const f of this.fields) {
      const isSelected = f.fieldName === this.selectedField();
      const b = f.box;

      const x = b.x * scale;
      const y = b.y * scale;
      const w = b.w * scale;
      const h = b.h * scale;

      // Color coding: green >= 0.95, amber 0.80-0.94, red < 0.80
      let strokeColor = '#10b981';
      let fillColor = 'rgba(16, 185, 129, 0.12)';

      if (f.confidence < 0.80) {
        strokeColor = '#ef4444';
        fillColor = 'rgba(239, 68, 68, 0.2)';
      } else if (f.confidence < 0.95) {
        strokeColor = '#f59e0b';
        fillColor = 'rgba(245, 158, 11, 0.18)';
      }

      ctx.fillStyle = fillColor;
      ctx.fillRect(x, y, w, h);

      ctx.lineWidth = isSelected ? 3 : 1.5;
      ctx.strokeStyle = isSelected ? '#38bdf8' : strokeColor;
      ctx.strokeRect(x, y, w, h);

      // Label & Confidence tag
      ctx.fillStyle = strokeColor;
      ctx.font = 'bold 11px sans-serif';
      ctx.fillText(`${f.label}: ${f.value} (${(f.confidence * 100).toFixed(0)}%)`, x + 4, y - 4);
    }
  }
}
