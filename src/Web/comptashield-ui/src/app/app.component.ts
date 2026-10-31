import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { HeaderSwitcherComponent } from './shared/components/header-switcher/header-switcher.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, HeaderSwitcherComponent],
  template: `
    <div class="h-screen w-screen flex flex-col bg-slate-950 text-slate-100 overflow-hidden font-sans">
      <!-- Tier 1: Multi-Tenant Header Switcher -->
      <app-header-switcher></app-header-switcher>

      <!-- Main Navigation Tabs -->
      <nav class="bg-slate-900/90 border-b border-slate-800 px-6 py-2 flex items-center justify-between text-xs">
        <div class="flex items-center gap-2">
          <a 
            routerLink="/hitl-validation" 
            routerLinkActive="bg-sky-600 text-white font-bold shadow" 
            [routerLinkActiveOptions]="{ exact: false }"
            class="px-3.5 py-1.5 rounded-lg text-slate-300 hover:text-white transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>
            Revue HITL OCR / TEIF
          </a>
          <a 
            routerLink="/fraud-cockpit" 
            routerLinkActive="bg-sky-600 text-white font-bold shadow" 
            [routerLinkActiveOptions]="{ exact: false }"
            class="px-3.5 py-1.5 rounded-lg text-slate-300 hover:text-white transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>
            Fraud Shield Cockpit
          </a>
          <a 
            routerLink="/bilan-workspace" 
            routerLinkActive="bg-sky-600 text-white font-bold shadow" 
            [routerLinkActiveOptions]="{ exact: false }"
            class="px-3.5 py-1.5 rounded-lg text-slate-300 hover:text-white transition-all flex items-center gap-1.5">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h18M7 15h1m4 0h1m-7 4h12a3 3 0 003-3V8a3 3 0 00-3-3H6a3 3 0 00-3 3v8a3 3 0 003 3z"/></svg>
            Bilan &amp; Inventaire (Drill-Down)
          </a>
        </div>

        <div class="flex items-center gap-3 text-slate-400 font-mono text-[11px]">
          <span class="flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            SignalR Hub Connecté (/hubs/fraud-alerts)
          </span>
          <span class="text-slate-600">|</span>
          <span>WORM Object Lock: ACTIF</span>
        </div>
      </nav>

      <!-- Main Application Viewport -->
      <main class="flex-1 overflow-hidden">
        <router-outlet></router-outlet>
      </main>
    </div>
  `
})
export class AppComponent {}
