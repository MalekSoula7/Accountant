import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DrillDownDocument {
  documentId: string;
  fileName: string;
  sha256: string;
  vaultPath: string;
  confidence: number;
}

export interface DrillDownEntry {
  entryId: string;
  entryNumber: string;
  date: string;
  description: string;
  debit: number;
  credit: number;
  document?: DrillDownDocument;
}

export interface DrillDownAccount {
  accountNumber: string;
  label: string;
  balance: number;
  entries: DrillDownEntry[];
}

export interface BilanItem {
  code: string;
  label: string;
  amount: number;
  accounts: DrillDownAccount[];
}

export interface StockItemUi {
  itemCode: string;
  description: string;
  theoreticalQty: number;
  physicalQty: number;
  discrepancyQty: number;
  unitCostCump: number;
  unitCostFifo: number;
  totalValuation: number;
  provision: number;
}

@Component({
  selector: 'app-bilan-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="h-full flex flex-col bg-slate-950 text-slate-100 overflow-y-auto p-6 space-y-6">
      <!-- Workspace Top Navigation -->
      <div class="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-xl font-bold tracking-tight text-white">ESPACE ÉTATS FINANCIERS &amp; INVENTAIRE</h1>
            <span class="px-2 py-0.5 text-xs font-mono rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-bold">
              DRILL-DOWN 4 NIVEAUX
            </span>
          </div>
          <p class="text-xs text-slate-400 mt-1">Auditabilité intégrale : Ligne de Bilan &rarr; Compte &rarr; Écriture Journal &rarr; Document Numérisé WORM.</p>
        </div>

        <!-- Tab Switcher -->
        <div class="flex items-center p-1 bg-slate-900 border border-slate-800 rounded-xl">
          <button 
            (click)="activeTab.set('bilan')"
            [class]="activeTab() === 'bilan' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-1.5 rounded-lg text-xs font-semibold transition-all">
            Bilan Actif &amp; Passif
          </button>
          <button 
            (click)="activeTab.set('inventaire')"
            [class]="activeTab() === 'inventaire' ? 'bg-sky-600 text-white shadow' : 'text-slate-400 hover:text-slate-200'"
            class="px-4 py-1.5 rounded-lg text-xs font-semibold transition-all">
            Valorisation Stocks (CUMP / FIFO)
          </button>
        </div>
      </div>

      <!-- TAB 1: BILAN ACTIF & PASSIF DRILL-DOWN -->
      @if (activeTab() === 'bilan') {
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Bilan Actif -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h2 class="text-sm font-bold text-white flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-emerald-500"></span>
                BILAN ACTIF (EMPLOIS)
              </h2>
              <span class="text-sm font-mono font-bold text-emerald-400">Total : {{ totalActif() | number:'1.3-3' }} TND</span>
            </div>

            <!-- Drill-Down Lines -->
            <div class="space-y-3">
              @for (item of actifItems; track item.code) {
                <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl overflow-hidden">
                  <!-- Tier 1: Bilan Line Header -->
                  <div 
                    (click)="toggleExpand(item.code)"
                    class="p-3.5 flex items-center justify-between cursor-pointer hover:bg-slate-900 transition-colors">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-bold text-sky-400 font-mono">{{ item.code }}</span>
                      <span class="text-xs font-semibold text-slate-200">{{ item.label }}</span>
                    </div>
                    <div class="flex items-center gap-3">
                      <span class="text-xs font-mono font-bold text-white">{{ item.amount | number:'1.3-3' }} TND</span>
                      <span class="text-slate-500 text-xs">{{ isExpanded(item.code) ? '▼' : '▶' }}</span>
                    </div>
                  </div>

                  <!-- Tier 2: Accounts Breakdown -->
                  @if (isExpanded(item.code)) {
                    <div class="bg-slate-900/40 border-t border-slate-800 p-3 space-y-3">
                      @for (acc of item.accounts; track acc.accountNumber) {
                        <div class="pl-3 border-l-2 border-sky-500/40 space-y-2">
                          <div class="flex items-center justify-between text-xs font-mono">
                            <span class="font-bold text-slate-300">{{ acc.accountNumber }} - {{ acc.label }}</span>
                            <span class="text-sky-300 font-semibold">{{ acc.balance | number:'1.3-3' }} TND</span>
                          </div>

                          <!-- Tier 3: Journal Entries -->
                          <div class="space-y-1.5 pl-2">
                            @for (entry of acc.entries; track entry.entryId) {
                              <div class="bg-slate-950/60 p-2 rounded border border-slate-800/60 flex items-center justify-between text-[11px] font-mono">
                                <div>
                                  <span class="font-bold text-amber-400 mr-2">{{ entry.entryNumber }}</span>
                                  <span class="text-slate-400 mr-2">{{ entry.date }}</span>
                                  <span class="text-slate-300">{{ entry.description }}</span>
                                </div>
                                <div class="flex items-center gap-3">
                                  <span class="text-white font-bold">{{ entry.debit > 0 ? entry.debit : entry.credit | number:'1.3-3' }} TND</span>
                                  @if (entry.document) {
                                    <!-- Tier 4: Document Modal Trigger -->
                                    <button 
                                      (click)="openDocPreview(entry.document)"
                                      class="px-2 py-0.5 rounded bg-sky-500/20 text-sky-400 border border-sky-500/30 text-[10px] hover:bg-sky-500/30 transition-all font-sans font-semibold">
                                      Voir Document WORM
                                    </button>
                                  }
                                </div>
                              </div>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          </div>

          <!-- Bilan Passif -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h2 class="text-sm font-bold text-white flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-indigo-500"></span>
                BILAN PASSIF &amp; CAPITAUX PROPRES (RESSOURCES)
              </h2>
              <span class="text-sm font-mono font-bold text-indigo-400">Total : {{ totalPassif() | number:'1.3-3' }} TND</span>
            </div>

            <div class="space-y-3">
              @for (item of passifItems; track item.code) {
                <div class="bg-slate-950/80 border border-slate-800/80 rounded-xl overflow-hidden">
                  <div 
                    (click)="toggleExpand(item.code)"
                    class="p-3.5 flex items-center justify-between cursor-pointer hover:bg-slate-900 transition-colors">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-bold text-indigo-400 font-mono">{{ item.code }}</span>
                      <span class="text-xs font-semibold text-slate-200">{{ item.label }}</span>
                    </div>
                    <div class="flex items-center gap-3">
                      <span class="text-xs font-mono font-bold text-white">{{ item.amount | number:'1.3-3' }} TND</span>
                      <span class="text-slate-500 text-xs">{{ isExpanded(item.code) ? '▼' : '▶' }}</span>
                    </div>
                  </div>

                  @if (isExpanded(item.code)) {
                    <div class="bg-slate-900/40 border-t border-slate-800 p-3 space-y-3">
                      @for (acc of item.accounts; track acc.accountNumber) {
                        <div class="pl-3 border-l-2 border-indigo-500/40 space-y-2">
                          <div class="flex items-center justify-between text-xs font-mono">
                            <span class="font-bold text-slate-300">{{ acc.accountNumber }} - {{ acc.label }}</span>
                            <span class="text-indigo-300 font-semibold">{{ acc.balance | number:'1.3-3' }} TND</span>
                          </div>

                          <div class="space-y-1.5 pl-2">
                            @for (entry of acc.entries; track entry.entryId) {
                              <div class="bg-slate-950/60 p-2 rounded border border-slate-800/60 flex items-center justify-between text-[11px] font-mono">
                                <div>
                                  <span class="font-bold text-amber-400 mr-2">{{ entry.entryNumber }}</span>
                                  <span class="text-slate-400 mr-2">{{ entry.date }}</span>
                                  <span class="text-slate-300">{{ entry.description }}</span>
                                </div>
                                <div class="flex items-center gap-3">
                                  <span class="text-white font-bold">{{ entry.credit | number:'1.3-3' }} TND</span>
                                  @if (entry.document) {
                                    <button 
                                      (click)="openDocPreview(entry.document)"
                                      class="px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-400 border border-indigo-500/30 text-[10px] hover:bg-indigo-500/30 transition-all font-sans font-semibold">
                                      Voir Document WORM
                                    </button>
                                  }
                                </div>
                              </div>
                            }
                          </div>
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>

            <!-- Equilibrium Check Banner -->
            <div class="p-4 rounded-xl bg-emerald-950/30 border border-emerald-500/30 flex items-center justify-between text-xs font-mono">
              <span class="text-emerald-400 font-bold flex items-center gap-2">
                <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                ÉGALITÉ FONDAMENTALE : ACTIF == PASSIF
              </span>
              <span class="text-slate-300">Écart : 0.0000 TND (Conforme)</span>
            </div>
          </div>
        </div>
      }

      <!-- TAB 2: INVENTAIRE & VALORISATION STOCKS (CUMP / FIFO) -->
      @if (activeTab() === 'inventaire') {
        <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
          <div class="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800 pb-3">
            <div>
              <h2 class="text-sm font-bold text-white flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-amber-500"></span>
                RAPPROCHEMENT INVENTAIRE PHYSIQUE &amp; VALORISATION
              </h2>
              <p class="text-xs text-slate-400 mt-0.5">Comparaison du Stock Théorique (Grand Livre) vs Procès-Verbal d'Inventaire Physique.</p>
            </div>

            <div class="flex items-center gap-3">
              <span class="text-xs text-slate-400 font-mono">MÉTHODE :</span>
              <div class="flex p-0.5 bg-slate-950 rounded-lg border border-slate-800 text-xs font-mono">
                <button 
                  (click)="valuationMethod.set('CUMP')"
                  [class]="valuationMethod() === 'CUMP' ? 'bg-amber-500 text-slate-950 font-bold' : 'text-slate-400'"
                  class="px-3 py-1 rounded transition-all">
                  CUMP
                </button>
                <button 
                  (click)="valuationMethod.set('FIFO')"
                  [class]="valuationMethod() === 'FIFO' ? 'bg-amber-500 text-slate-950 font-bold' : 'text-slate-400'"
                  class="px-3 py-1 rounded transition-all">
                  FIFO
                </button>
              </div>
            </div>
          </div>

          <!-- Stock Table -->
          <div class="overflow-x-auto rounded-xl border border-slate-800">
            <table class="w-full text-left text-xs font-mono">
              <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                <tr>
                  <th class="p-3">Réf Article</th>
                  <th class="p-3">Désignation</th>
                  <th class="p-3 text-right">Qté Théorique</th>
                  <th class="p-3 text-right">Qté PV Physique</th>
                  <th class="p-3 text-right">Écart Inventaire</th>
                  <th class="p-3 text-right">Coût Unitaire ({{ valuationMethod() }})</th>
                  <th class="p-3 text-right">Valeur Stock</th>
                  <th class="p-3 text-right">Provision Dépréciation</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 bg-slate-900/40">
                @for (item of stockItems; track item.itemCode) {
                  <tr class="hover:bg-slate-800/50 transition-colors">
                    <td class="p-3 font-bold text-slate-200">{{ item.itemCode }}</td>
                    <td class="p-3 text-slate-300 font-sans">{{ item.description }}</td>
                    <td class="p-3 text-right text-slate-400">{{ item.theoreticalQty }}</td>
                    <td class="p-3 text-right font-bold text-white">{{ item.physicalQty }}</td>
                    <td class="p-3 text-right font-bold" [ngClass]="item.discrepancyQty < 0 ? 'text-rose-400' : (item.discrepancyQty > 0 ? 'text-emerald-400' : 'text-slate-500')">
                      {{ item.discrepancyQty > 0 ? '+' : '' }}{{ item.discrepancyQty }}
                    </td>
                    <td class="p-3 text-right font-bold text-amber-400">
                      {{ (valuationMethod() === 'CUMP' ? item.unitCostCump : item.unitCostFifo) | number:'1.3-3' }} TND
                    </td>
                    <td class="p-3 text-right font-bold text-white">
                      {{ (item.physicalQty * (valuationMethod() === 'CUMP' ? item.unitCostCump : item.unitCostFifo)) | number:'1.3-3' }} TND
                    </td>
                    <td class="p-3 text-right font-bold" [ngClass]="item.provision > 0 ? 'text-amber-400' : 'text-slate-600'">
                      {{ item.provision > 0 ? (item.provision | number:'1.3-3') + ' TND' : '-' }}
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Document Preview Modal (Tier 4 Drill-Down) -->
      @if (selectedDoc()) {
        <div class="fixed inset-0 bg-black/85 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-700 rounded-2xl max-w-xl w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-sky-400"></span>
                PIÈCE JUSTIFICATIVE NUMÉRISÉE (TIER-1 VAULT)
              </h3>
              <button (click)="selectedDoc.set(null)" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="space-y-3 text-xs font-mono">
              <div class="p-3 rounded-lg bg-slate-950 border border-slate-800 space-y-1.5">
                <div class="text-slate-400">Nom du fichier : <span class="text-white">{{ selectedDoc()!.fileName }}</span></div>
                <div class="text-slate-400">ID Document : <span class="text-white">{{ selectedDoc()!.documentId }}</span></div>
                <div class="text-slate-400">Emplacement WORM S3 : <span class="text-sky-300 break-all">{{ selectedDoc()!.vaultPath }}</span></div>
                <div class="text-slate-400">Empreinte SHA-256 : <span class="text-emerald-400 break-all">{{ selectedDoc()!.sha256 }}</span></div>
                <div class="text-slate-400">Score de Confiance OCR : <span class="text-emerald-400 font-bold">{{ (selectedDoc()!.confidence * 100).toFixed(1) }}%</span></div>
              </div>

              <div class="h-40 rounded-lg bg-slate-950 border border-slate-800 flex items-center justify-center text-slate-500 font-sans">
                Aperçu du scan certifié conforme Object Lock WORM
              </div>
            </div>

            <div class="flex justify-end pt-2">
              <button (click)="selectedDoc.set(null)" class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-300">
                Fermer
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class BilanWorkspaceComponent {
  readonly activeTab = signal<'bilan' | 'inventaire'>('bilan');
  readonly valuationMethod = signal<'CUMP' | 'FIFO'>('CUMP');
  readonly expandedItems = signal<Set<string>>(new Set(['ACT_CREANCES', 'PAS_DETTES_CT']));
  readonly selectedDoc = signal<DrillDownDocument | null>(null);

  readonly totalActif = signal(615461.000);
  readonly totalPassif = signal(615461.000);

  readonly actifItems: BilanItem[] = [
    {
      code: 'ACT_IMMOB',
      label: 'Actifs non courants / Immobilisations',
      amount: 125000.000,
      accounts: [
        {
          accountNumber: '215000',
          label: 'Installations techniques & outillage',
          balance: 125000.000,
          entries: [
            {
              entryId: 'e1', entryNumber: 'HA-2026-0012', date: '15/01/2026', description: 'Acquisition serveur Dell R750', debit: 125000, credit: 0,
              document: { documentId: 'doc-001', fileName: 'facture_dell_r750.pdf', sha256: '9a7c3b2f8190d4e5f...', vaultPath: '/cabinet/dossier/2026/inbound/9a7c3b2f...pdf', confidence: 0.99 }
            }
          ]
        }
      ]
    },
    {
      code: 'ACT_CREANCES',
      label: 'Clients et autres créances courantes',
      amount: 40461.000,
      accounts: [
        {
          accountNumber: '411000',
          label: 'Clients ordinaires',
          balance: 40461.000,
          entries: [
            {
              entryId: 'e2', entryNumber: 'VT-2026-0891', date: '15/03/2026', description: 'Facture vente #0891 Client ABC', debit: 40461, credit: 0,
              document: { documentId: 'doc-002', fileName: 'facture_fac_2026_0891.pdf', sha256: '8b4f1d3e7201c6a...', vaultPath: '/cabinet/dossier/2026/inbound/8b4f1d3e...pdf', confidence: 0.98 }
            }
          ]
        }
      ]
    },
    {
      code: 'ACT_TRESO',
      label: 'Trésorerie Actif (Banques & Caisses)',
      amount: 450000.000,
      accounts: [
        {
          accountNumber: '512000',
          label: 'Banque BIAT Courant',
          balance: 450000.000,
          entries: [
            { entryId: 'e3', entryNumber: 'OD-2026-0001', date: '01/01/2026', description: 'Apport capital social', debit: 500000, credit: 0 },
            { entryId: 'e4', entryNumber: 'BQ-2026-0045', date: '20/01/2026', description: 'Règlement fournisseur Dell', debit: 0, credit: 50000 }
          ]
        }
      ]
    }
  ];

  readonly passifItems: BilanItem[] = [
    {
      code: 'PAS_EQUITY',
      label: 'Capital et réserves',
      amount: 500000.000,
      accounts: [
        {
          accountNumber: '101000',
          label: 'Capital social souscrit',
          balance: 500000.000,
          entries: [
            { entryId: 'e5', entryNumber: 'OD-2026-0001', date: '01/01/2026', description: 'Constitution capital social', debit: 0, credit: 500000 }
          ]
        }
      ]
    },
    {
      code: 'PAS_RESULTAT',
      label: 'Résultat net de l\'exercice (Bénéfice)',
      amount: 75000.000,
      accounts: [
        {
          accountNumber: '120000',
          label: 'Résultat de l\'exercice',
          balance: 75000.000,
          entries: [
            { entryId: 'e6', entryNumber: 'CL-2026-0001', date: '31/12/2026', description: 'Solde créditeur Produits - Charges', debit: 0, credit: 75000 }
          ]
        }
      ]
    },
    {
      code: 'PAS_DETTES_CT',
      label: 'Fournisseurs et autres dettes courantes',
      amount: 40461.000,
      accounts: [
        {
          accountNumber: '401000',
          label: 'Fournisseurs d\'exploitation',
          balance: 40461.000,
          entries: [
            {
              entryId: 'e7', entryNumber: 'HA-2026-0042', date: '10/02/2026', description: 'Facture Fournisseur Tech', debit: 0, credit: 40461,
              document: { documentId: 'doc-003', fileName: 'facture_fournisseur_tech.pdf', sha256: '7c2e1a9f4567...', vaultPath: '/cabinet/dossier/2026/inbound/7c2e1a...pdf', confidence: 0.97 }
            }
          ]
        }
      ]
    }
  ];

  readonly stockItems: StockItemUi[] = [
    { itemCode: 'SRV-R750', description: 'Serveur Dell PowerEdge R750', theoreticalQty: 10, physicalQty: 9, discrepancyQty: -1, unitCostCump: 15000, unitCostFifo: 15000, totalValuation: 135000, provision: 0 },
    { itemCode: 'LIC-WIN-2025', description: 'Licence Windows Server 2025', theoreticalQty: 25, physicalQty: 25, discrepancyQty: 0, unitCostCump: 2500, unitCostFifo: 2500, totalValuation: 62500, provision: 0 },
    { itemCode: 'RAM-DDR5-64G', description: 'Module RAM 64GB DDR5 ECC', theoreticalQty: 50, physicalQty: 48, discrepancyQty: -2, unitCostCump: 650, unitCostFifo: 680, totalValuation: 31200, provision: 1300 },
    { itemCode: 'SSD-NVME-3.8T', description: 'Disque NVMe 3.84TB Datacenter', theoreticalQty: 30, physicalQty: 30, discrepancyQty: 0, unitCostCump: 1200, unitCostFifo: 1250, totalValuation: 36000, provision: 0 }
  ];

  toggleExpand(code: string): void {
    const set = new Set(this.expandedItems());
    if (set.has(code)) {
      set.delete(code);
    } else {
      set.add(code);
    }
    this.expandedItems.set(set);
  }

  isExpanded(code: string): boolean {
    return this.expandedItems().has(code);
  }

  openDocPreview(doc: DrillDownDocument): void {
    this.selectedDoc.set(doc);
  }
}
