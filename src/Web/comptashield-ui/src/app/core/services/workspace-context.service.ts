import { Injectable, signal, computed } from '@angular/core';
import { ActiveWorkspace, AvailableDossier, AvailableExercice, DossierRole } from '../models/workspace.model';

const STORAGE_KEY = 'comptashield_active_workspace';

@Injectable({
  providedIn: 'root'
})
export class WorkspaceContextService {
  // Primary reactive Signal holding current tenant workspace state
  readonly activeWorkspace = signal<ActiveWorkspace>(this.loadFromSession());

  // Computed signals
  readonly currentDossierId = computed(() => this.activeWorkspace().dossierId);
  readonly currentExerciceId = computed(() => this.activeWorkspace().exerciceId);
  readonly isExerciceLocked = computed(() => this.activeWorkspace().isLocked);
  readonly userRole = computed(() => this.activeWorkspace().role);

  // Available mock / switchable workspaces for rapid auditor switching
  readonly availableDossiers = signal<AvailableDossier[]>([
    { id: '33333333-3333-3333-3333-333333333333', name: 'STE MEDITERRANEE DISTRIBUTION', matriculeFiscal: '1234567/A/M/000' },
    { id: '44444444-4444-4444-4444-444444444444', name: 'CARTHAGE LOGISTICS SARL', matriculeFiscal: '7654321/B/N/000' },
    { id: '55555555-5555-5555-5555-555555555555', name: 'TUNISIE TEXTILE INDUSTRIE', matriculeFiscal: '9988776/C/P/000' }
  ]);

  readonly availableExercices = signal<AvailableExercice[]>([
    { id: '66666666-6666-6666-6666-666666666666', year: 2026, isLocked: false },
    { id: '77777777-7777-7777-7777-777777777777', year: 2025, isLocked: true },
    { id: '88888888-8888-8888-8888-888888888888', year: 2024, isLocked: true }
  ]);

  constructor() {
    // If not found in sessionStorage, seed default dev workspace
    if (!sessionStorage.getItem(STORAGE_KEY)) {
      this.saveToSession(this.activeWorkspace());
    }
  }

  setWorkspace(workspace: ActiveWorkspace): void {
    this.activeWorkspace.set(workspace);
    this.saveToSession(workspace);
  }

  switchDossier(dossier: AvailableDossier): void {
    const current = this.activeWorkspace();
    const updated: ActiveWorkspace = {
      ...current,
      dossierId: dossier.id,
      dossierName: dossier.name
    };
    this.setWorkspace(updated);
  }

  switchExercice(exercice: AvailableExercice): void {
    const current = this.activeWorkspace();
    const updated: ActiveWorkspace = {
      ...current,
      exerciceId: exercice.id,
      exerciceYear: exercice.year,
      isLocked: exercice.isLocked
    };
    this.setWorkspace(updated);
  }

  setRole(role: DossierRole): void {
    const current = this.activeWorkspace();
    this.setWorkspace({ ...current, role });
  }

  private loadFromSession(): ActiveWorkspace {
    try {
      const stored = sessionStorage.getItem(STORAGE_KEY);
      if (stored) {
        return JSON.parse(stored);
      }
    } catch {
      // Ignore parse failure and fallback
    }

    return {
      cabinetId: '22222222-2222-2222-2222-222222222222',
      cabinetName: 'Cabinet Audit & Conseil Partners',
      dossierId: '33333333-3333-3333-3333-333333333333',
      dossierName: 'STE MEDITERRANEE DISTRIBUTION',
      exerciceId: '66666666-6666-6666-6666-666666666666',
      exerciceYear: 2026,
      role: 'Partner',
      isLocked: false
    };
  }

  private saveToSession(workspace: ActiveWorkspace): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(workspace));
    } catch {
      // Ignore write failure in restricted iframe/private mode
    }
  }
}
