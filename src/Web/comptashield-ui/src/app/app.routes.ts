import { Routes } from '@angular/router';
import { HitlValidationComponent } from './features/hitl-validation/hitl-validation.component';
import { FraudCockpitComponent } from './features/fraud-cockpit/fraud-cockpit.component';
import { BilanWorkspaceComponent } from './features/bilan-workspace/bilan-workspace.component';

export const routes: Routes = [
  { path: '', redirectTo: 'hitl-validation', pathMatch: 'full' },
  { path: 'hitl-validation', component: HitlValidationComponent },
  { path: 'fraud-cockpit', component: FraudCockpitComponent },
  { path: 'bilan-workspace', component: BilanWorkspaceComponent },
  { path: '**', redirectTo: 'hitl-validation' }
];
