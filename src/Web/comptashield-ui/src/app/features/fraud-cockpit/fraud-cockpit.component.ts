import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface BenfordDigitBar {
  digit: number;
  expected: number; // Percentage, e.g. 30.1
  actual: number;   // Percentage, e.g. 11.2
  isAnomalous: boolean;
}

export interface DuplicateComparison {
  field: string;
  originalValue: string;
  suspectValue: string;
  isMismatch: boolean;
}

export interface ThreeWayRow {
  itemCode: string;
  description: string;
  poQty: number;
  poPrice: number;
  blQty: number;
  invoiceQty: number;
  invoicePrice: number;
  status: 'Conforme' | 'Livraison Fantôme' | 'Surfacturation Prix';
  impactTnd: number;
}

@Component({
  selector: 'app-fraud-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto p-6 space-y-6">
      <!-- Cockpit Header & KPIs -->
      <div class="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-2">
            <h1 class="text-xl font-bold tracking-tight text-white">FRAUD SHIELD & AUDIT COCKPIT</h1>
            <span class="px-2 py-0.5 text-xs font-mono rounded-full bg-rose-500/10 text-rose-400 border border-rose-500/20 font-bold animate-pulse">
              3 ANOMALIES CRITIQUES
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-1">Supervision forensique en temps réel des flux comptables, détection de falsifications et smurfing.</p>
        </div>

        <div class="flex items-center gap-4">
          <div class="bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl text-right">
            <div class="text-[10px] text-slate-400 font-mono">INDICE DE RISQUE DOSSIER</div>
            <div class="text-lg font-bold font-mono text-rose-400">0.86 / 1.00</div>
          </div>
          <div class="bg-slate-900 border border-slate-800 px-4 py-2 rounded-xl text-right">
            <div class="text-[10px] text-slate-400 font-mono">EXPOSITION FINANCIÈRE</div>
            <div class="text-lg font-bold font-mono text-amber-400">18 950.000 TND</div>
          </div>
        </div>
      </div>

      <!-- Section 1: Benford's Law Forensic Distribution -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 backdrop-blur">
        <div class="flex flex-wrap items-center justify-between gap-2 mb-4">
          <div>
            <h2 class="text-sm font-bold text-white flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-sky-500"></span>
              TEST DE CONFORMITÉ LOI DE BENFORD (1ER CHIFFRE, df=8)
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">Analyse de régularité statistique des montants du Grand Livre (Classes 6 & 7).</p>
          </div>
          <div class="flex items-center gap-3 text-xs font-mono">
            <span class="px-2.5 py-1 rounded bg-rose-500/20 text-rose-400 border border-rose-500/30 font-bold">
              &chi;² = 24.81 &gt; 15.51 (p &lt; 0.001) - ANOMALIE CONFIRMÉE
            </span>
            <div class="flex items-center gap-2">
              <span class="flex items-center gap-1 text-slate-400"><span class="w-2 h-2 rounded-full bg-sky-500"></span> Théorique</span>
              <span class="flex items-center gap-1 text-slate-400"><span class="w-2 h-2 rounded-full bg-rose-500"></span> Observé</span>
            </div>
          </div>
        </div>

        <!-- Bar Chart Visualizer -->
        <div class="grid grid-cols-9 gap-3 h-48 items-end pt-4 pb-2 border-b border-slate-800">
          @for (b of benfordBars; track b.digit) {
            <div class="flex flex-col items-center h-full justify-end group">
              <div class="text-[10px] font-mono text-slate-400 mb-1 opacity-0 group-hover:opacity-100 transition-opacity">
                {{ b.actual }}%
              </div>
              <div class="w-full flex items-end justify-center gap-1 h-36">
                <!-- Expected Bar (Blue) -->
                <div 
                  class="w-3 rounded-t bg-sky-500/70 border border-sky-400"
                  [style.height.%]="(b.expected / 35) * 100">
                </div>
                <!-- Actual Bar (Red/Emerald) -->
                <div 
                  class="w-3 rounded-t border"
                  [ngClass]="b.isAnomalous ? 'bg-rose-500 border-rose-400 animate-pulse' : 'bg-emerald-500/70 border-emerald-400'"
                  [style.height.%]="(b.actual / 35) * 100">
                </div>
              </div>
              <div class="text-xs font-bold font-mono text-slate-300 mt-2">D{{ b.digit }}</div>
            </div>
          }
        </div>
        <div class="mt-3 text-xs text-slate-400 flex items-center justify-between">
          <span>Surreprésentation anormale des chiffres 4 et 9 (concentration artificielle de factures à 4 950 TND et 9 900 TND).</span>
          <span class="font-mono text-amber-400 font-semibold">Indicateur de Smurfing (Seuil d'approbation contourné)</span>
        </div>
      </div>

      <!-- Section 2: Side-by-Side Duplicate Invoice Diff Viewer -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 backdrop-blur">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h2 class="text-sm font-bold text-white flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span>
              DÉTECTION DE DOUBLON SÉMANTIQUE &amp; IMAGE (pHash)
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">Facture soumise ce jour suspectée d'être un doublon frauduleux d'un exercice antérieur.</p>
          </div>
          <span class="text-xs font-mono px-2 py-0.5 rounded bg-amber-500/20 text-amber-400 border border-amber-500/30 font-bold">
            Similarité pHash: 98.4% (Distance de Hamming: 1/64)
          </span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <!-- Left: Original Invoice in Prior Year -->
          <div class="bg-slate-950 p-4 rounded-xl border border-slate-800 space-y-2">
            <div class="text-xs font-mono text-slate-400 border-b border-slate-800 pb-2 flex justify-between">
              <span>ORIGINALE : Exercice FY 2025 (Comptabilisée &amp; Réglée)</span>
              <span class="text-emerald-400 font-bold">Doc #1042</span>
            </div>
            <div class="space-y-1.5 text-xs font-mono">
              <div class="flex justify-between"><span class="text-slate-400">N° Facture :</span> <span class="text-slate-200">FAC-2025-0042</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Date :</span> <span class="text-slate-200">28/12/2025</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Fournisseur :</span> <span class="text-slate-200">1234567/B/M/000 (STE TUNIS DISTRIBUTION)</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Montant TTC :</span> <span class="text-emerald-400 font-bold">12 500.0000 TND</span></div>
            </div>
          </div>

          <!-- Right: Suspect Inbound Duplicate -->
          <div class="bg-slate-950 p-4 rounded-xl border border-rose-500/30 space-y-2 bg-rose-950/10">
            <div class="text-xs font-mono text-rose-400 border-b border-rose-500/20 pb-2 flex justify-between">
              <span>SUSPECTE : Exercice FY 2026 (En cours d'ingestion)</span>
              <span class="text-rose-400 font-bold">Doc #4099</span>
            </div>
            <div class="space-y-1.5 text-xs font-mono">
              <div class="flex justify-between"><span class="text-slate-400">N° Facture :</span> <span class="text-rose-300 font-bold">FAC/2025/0042 (format modifié)</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Date :</span> <span class="text-slate-200">28/12/2025</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Fournisseur :</span> <span class="text-slate-200">1234567-B-M-000 (variante tirets)</span></div>
              <div class="flex justify-between"><span class="text-slate-400">Montant TTC :</span> <span class="text-rose-400 font-bold">12 500.0000 TND (identique)</span></div>
            </div>
          </div>
        </div>

        <div class="mt-4 flex items-center justify-end gap-3">
          <button 
            (click)="triggerQuarantine('Doublon cross-exercice détecté')"
            class="px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white font-semibold text-xs shadow-lg shadow-rose-600/20 transition-all flex items-center gap-2">
            Mettre en Quarantaine &amp; Bloquer le Paiement
          </button>
        </div>
      </div>

      <!-- Section 3: 3-Way Matching Matrix -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 backdrop-blur">
        <div class="flex items-center justify-between mb-4">
          <div>
            <h2 class="text-sm font-bold text-white flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-indigo-500"></span>
              MATRICE DE RAPPROCHEMENT 3-WAY (BC &lt;-&gt; BL &lt;-&gt; FACTURE &lt;-&gt; BANQUE)
            </h2>
            <p class="text-xs text-slate-400 mt-0.5">Rapprochement ligne par ligne des quantités commandées, reçues physiquement, et facturées.</p>
          </div>
        </div>

        <div class="overflow-x-auto rounded-xl border border-slate-800">
          <table class="w-full text-left text-xs font-mono">
            <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
              <tr>
                <th class="p-3">Réf Article</th>
                <th class="p-3">Désignation</th>
                <th class="p-3 text-right">Qté BC</th>
                <th class="p-3 text-right">PU BC</th>
                <th class="p-3 text-right">Qté BL (Reçue)</th>
                <th class="p-3 text-right">Qté Facturée</th>
                <th class="p-3 text-right">PU Facturé</th>
                <th class="p-3">Statut Audit</th>
                <th class="p-3 text-right">Impact</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800 bg-slate-900/40">
              @for (row of threeWayRows; track row.itemCode) {
                <tr class="hover:bg-slate-800/50 transition-colors">
                  <td class="p-3 font-bold text-slate-200">{{ row.itemCode }}</td>
                  <td class="p-3 text-slate-300 font-sans">{{ row.description }}</td>
                  <td class="p-3 text-right text-slate-400">{{ row.poQty }}</td>
                  <td class="p-3 text-right text-slate-400">{{ row.poPrice | number:'1.2-2' }}</td>
                  <td class="p-3 text-right font-bold" [ngClass]="row.blQty < row.invoiceQty ? 'text-rose-400' : 'text-slate-300'">
                    {{ row.blQty }}
                  </td>
                  <td class="p-3 text-right font-bold text-slate-200">{{ row.invoiceQty }}</td>
                  <td class="p-3 text-right font-bold" [ngClass]="row.invoicePrice > row.poPrice ? 'text-amber-400' : 'text-slate-300'">
                    {{ row.invoicePrice | number:'1.2-2' }}
                  </td>
                  <td class="p-3">
                    <span 
                      class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="{
                        'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': row.status === 'Conforme',
                        'bg-rose-500/20 text-rose-400 border border-rose-500/30': row.status === 'Livraison Fantôme',
                        'bg-amber-500/20 text-amber-400 border border-amber-500/30': row.status === 'Surfacturation Prix'
                      }">
                      {{ row.status }}
                    </span>
                  </td>
                  <td class="p-3 text-right font-bold" [ngClass]="row.impactTnd > 0 ? 'text-rose-400' : 'text-slate-500'">
                    {{ row.impactTnd > 0 ? '+' + (row.impactTnd | number:'1.3-3') + ' TND' : '-' }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>

      <!-- Action Drawer Modal: Client Clarification Ticket -->
      @if (showTicketModal()) {
        <div class="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-700 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
            <h3 class="text-base font-bold text-white flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-sky-500"></span>
              BILLET DE JUSTIFICATION CLIENT (MAGIC LINK)
            </h3>
            <p class="text-xs text-slate-400">Un lien chiffré temporaire (7 jours) sera transmis au portail client pour exiger la fourniture du bon de réception et des explications.</p>

            <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 text-xs font-mono text-emerald-400 break-all select-all">
              https://portal.comptashield.tn/guest/clarification?token={{ ticketToken() }}
            </div>

            <div class="flex justify-end gap-3 pt-2">
              <button (click)="showTicketModal.set(false)" class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-300">
                Fermer
              </button>
              <button (click)="copyToken()" class="px-4 py-2 rounded-lg bg-sky-600 hover:bg-sky-500 text-xs font-semibold text-white">
                Copier le Lien
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class FraudCockpitComponent {
  readonly showTicketModal = signal(false);
  readonly ticketToken = signal('d9a8f273c51b4e09871a28cb7190e3814f8a32d1');

  // Benford's Law empirical vs theoretical distribution
  readonly benfordBars: BenfordDigitBar[] = [
    { digit: 1, expected: 30.1, actual: 18.4, isAnomalous: true },
    { digit: 2, expected: 17.6, actual: 12.1, isAnomalous: false },
    { digit: 3, expected: 12.5, actual: 10.3, isAnomalous: false },
    { digit: 4, expected: 9.7, actual: 23.8, isAnomalous: true }, // Anomalous peak at 4
    { digit: 5, expected: 7.9, actual: 8.0, isAnomalous: false },
    { digit: 6, expected: 6.7, actual: 5.9, isAnomalous: false },
    { digit: 7, expected: 5.8, actual: 4.8, isAnomalous: false },
    { digit: 8, expected: 5.1, actual: 4.2, isAnomalous: false },
    { digit: 9, expected: 4.6, actual: 12.5, isAnomalous: true }  // Anomalous peak at 9
  ];

  readonly threeWayRows: ThreeWayRow[] = [
    { itemCode: 'SRV-DELL-R750', description: 'Serveur Dell PowerEdge R750', poQty: 5, poPrice: 15000, blQty: 3, invoiceQty: 5, invoicePrice: 15000, status: 'Livraison Fantôme', impactTnd: 30000 },
    { itemCode: 'LIC-WIN-SVR', description: 'Licence Windows Server 2025', poQty: 5, poPrice: 2500, blQty: 5, invoiceQty: 5, invoicePrice: 2800, status: 'Surfacturation Prix', impactTnd: 1500 },
    { itemCode: 'CBL-CAT6-SFP', description: 'Câbles Réseau SFP+ 10Gbps', poQty: 20, poPrice: 85, blQty: 20, invoiceQty: 20, invoicePrice: 85, status: 'Conforme', impactTnd: 0 }
  ];

  triggerQuarantine(reason: string): void {
    this.showTicketModal.set(true);
  }

  copyToken(): void {
    navigator.clipboard?.writeText(`https://portal.comptashield.tn/guest/clarification?token=${this.ticketToken()}`);
    alert('Lien de clarification copié dans le presse-papier !');
    this.showTicketModal.set(false);
  }
}
