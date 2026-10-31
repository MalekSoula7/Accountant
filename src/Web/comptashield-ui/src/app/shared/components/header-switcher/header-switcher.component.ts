import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { WorkspaceContextService } from '../../../core/services/workspace-context.service';
import { DossierRole } from '../../../core/models/workspace.model';

@Component({
  selector: 'app-header-switcher',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <header class="bg-slate-900 border-b border-slate-800 px-6 py-3 flex flex-wrap items-center justify-between gap-4">
      <!-- Left: Logo & Platform Identity -->
      <div class="flex items-center gap-3">
        <div class="w-9 h-9 rounded-lg bg-gradient-to-tr from-sky-500 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-sky-500/20">
          CS
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="font-bold text-white tracking-wide">COMPTASHIELD</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-sky-500/10 text-sky-400 border border-sky-500/20 font-mono">SOC v2.0</span>
          </div>
          <div class="text-xs text-slate-400">{{ workspace.activeWorkspace().cabinetName }}</div>
        </div>
      </div>

      <!-- Center: Multi-Tenancy Switcher Controls -->
      <div class="flex items-center gap-3 bg-slate-950/80 p-1.5 rounded-xl border border-slate-800/80">
        <!-- Dossier Switcher -->
        <div class="flex items-center gap-1.5 px-3 py-1 bg-slate-900 rounded-lg border border-slate-800">
          <span class="text-xs text-slate-400 font-medium">DOSSIER:</span>
          <select 
            [ngModel]="workspace.activeWorkspace().dossierId"
            (ngModelChange)="onDossierChange($event)"
            class="bg-transparent text-sm font-semibold text-slate-200 outline-none cursor-pointer">
            @for (d of workspace.availableDossiers(); track d.id) {
              <option [value]="d.id" class="bg-slate-900 text-slate-200">{{ d.name }}</option>
            }
          </select>
        </div>

        <!-- Exercice Switcher -->
        <div class="flex items-center gap-1.5 px-3 py-1 bg-slate-900 rounded-lg border border-slate-800">
          <span class="text-xs text-slate-400 font-medium">EXERCICE:</span>
          <select 
            [ngModel]="workspace.activeWorkspace().exerciceId"
            (ngModelChange)="onExerciceChange($event)"
            class="bg-transparent text-sm font-semibold text-slate-200 outline-none cursor-pointer">
            @for (ex of workspace.availableExercices(); track ex.id) {
              <option [value]="ex.id" class="bg-slate-900 text-slate-200">
                FY {{ ex.year }} {{ ex.isLocked ? '(Clôturé)' : '(Actif)' }}
              </option>
            }
          </select>

          @if (workspace.isExerciceLocked()) {
            <span class="ml-1 px-1.5 py-0.5 rounded bg-rose-500/20 text-rose-400 border border-rose-500/30 text-[10px] font-bold tracking-wider">
              VERROUILLÉ
            </span>
          } @else {
            <span class="ml-1 px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 text-[10px] font-bold tracking-wider">
              OUVERT
            </span>
          }
        </div>

        <!-- Role Simulator -->
        <div class="flex items-center gap-1.5 px-3 py-1 bg-slate-900 rounded-lg border border-slate-800">
          <span class="text-xs text-slate-400 font-medium">RÔLE:</span>
          <select 
            [ngModel]="workspace.userRole()"
            (ngModelChange)="onRoleChange($event)"
            class="bg-transparent text-sm font-semibold text-amber-400 outline-none cursor-pointer">
            <option value="Partner" class="bg-slate-900 text-slate-200">Partner (Associé)</option>
            <option value="SeniorAuditor" class="bg-slate-900 text-slate-200">Senior Auditor</option>
            <option value="JuniorCollaborator" class="bg-slate-900 text-slate-200">Junior Collaborator</option>
            <option value="ClientGuest" class="bg-slate-900 text-slate-200">Client Guest</option>
          </select>
        </div>
      </div>

      <!-- Right: User Avatar & Session Multi-Tab Info -->
      <div class="flex items-center gap-3">
        <div class="text-right hidden sm:block">
          <div class="text-xs font-semibold text-slate-200">Expert-Comptable Connecté</div>
          <div class="text-[10px] font-mono text-emerald-400 flex items-center justify-end gap-1">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
            RLS Active / Multi-Tab Isolated
          </div>
        </div>
        <div class="w-9 h-9 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-slate-300">
          EC
        </div>
      </div>
    </header>
  `
})
export class HeaderSwitcherComponent {
  readonly workspace = inject(WorkspaceContextService);

  onDossierChange(dossierId: string): void {
    const found = this.workspace.availableDossiers().find(d => d.id === dossierId);
    if (found) {
      this.workspace.switchDossier(found);
    }
  }

  onExerciceChange(exerciceId: string): void {
    const found = this.workspace.availableExercices().find(e => e.id === exerciceId);
    if (found) {
      this.workspace.switchExercice(found);
    }
  }

  onRoleChange(role: DossierRole): void {
    this.workspace.setRole(role);
  }
}
