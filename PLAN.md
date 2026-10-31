# PLAN.md - ComptaShield Autonomous Implementation Roadmap

> **Agent Execution Rule:** Work through phases sequentially (`P1.1` -> `P5.4`). Do not skip verification gates. After completing each item, change `[ ]` to `[x]`, update `PROGRESS.md`, commit, and immediately start the next item without waiting for user input.

## Solution Layout
- `src/Shared/ComptaShield.SharedKernel` (`Money`, `ITenantContext`, `IDossierScopedEntity`, `ScopedModuleDbContext`, `RlsConnectionInterceptor`, `TenantMessageHeader`, Outbox base)
- `src/Modules/Tenancy` (Cabinets, Dossiers, Exercices, `DossierAssignment` RBAC matrix, Redis `IPermissionChecker`, Subscription Quotas)
- `src/Modules/Ingestion` (S3/MinIO WORM vault, SHA-256 deduplication, TEIF XML e-invoice parser, OCR job outbox publisher, Cross-sum & 0.95 confidence router)
- `src/Modules/FraudShield` (PDF/EXIF forensics, Semantic/Fuzzy duplicate detector, 3-Way Matcher PO/BL/Invoice/Bank, Benford's Law Chi-Square engine, Rectification & Reversing Entry generator)
- `src/Modules/LedgerAndBilan` (Chart of Accounts SCE/PCG, Append-only Double-Entry Journal, Trial Balance, Bilan Actif/Passif, État de Résultat)
- `src/Modules/Inventory` (Stock items, CUMP & FIFO valuation calculators, Physical vs. Theoretical stock reconciliation, PV d'Inventaire)
- `src/Modules/Audit` (Materiality calculator, Stratified statistical sampling, Audit working papers & signed report export)
- `src/Web/ComptaShield.Api` (`TenantScopeMiddleware`, YARP/API endpoints, SignalR progress hub, MassTransit bus host)
- `src/Web/comptashield-ui` (Angular 19+ SPA, `sessionStorage` tab-isolated `WorkspaceContextService`, Split-Screen OCR Reviewer, Fraud & Bilan Cockpits)

---

## Phase 1: Foundation, Two-Tier Multi-Tenancy (`Cabinet` -> `Dossier` -> `Exercice`) & RLS
- [x] **P1.1:** Initialize `.NET 9` solution (`ComptaShield.sln`), `Directory.Build.props` (`TreatWarningsAsErrors=true`, `Nullable=enable`), and `docker-compose.yml` (`postgres:16-alpine`, `minio/minio`, `rabbitmq:3-management-alpine`, `redis:7-alpine`).
- [x] **P1.2:** Implement `ComptaShield.SharedKernel`:
  - `Money` value object (`decimal Amount`, `string Currency`, banker's rounding `MidpointRounding.ToEven`, arithmetic operators).
  - `DossierRole` enum (`Partner`, `SeniorAuditor`, `JuniorCollaborator`, `ClientGuest`), `ITenantContext`, `ScopedTenantContext`, and `IDossierScopedEntity` (`CabinetId`, `DossierId`, `ExerciceId`).
- [x] **P1.3:** Implement `ScopedModuleDbContext` and PostgreSQL RLS infrastructure in `SharedKernel`:
  - Reflection-based EF Core Global Query Filter applying `CabinetId`, `DossierId`, and `ExerciceId` to all `IDossierScopedEntity` models.
  - `SaveChangesAsync` override that blocks writes when `IsExerciceLocked == true`, auto-stamps `CabinetId`/`DossierId`/`ExerciceId` on `Added`, and prevents mutation of tenant keys on `Modified`/`Deleted`.
  - `RlsDbConnectionInterceptor` executing `set_config('app.current_cabinet_id', ...)`, `set_config('app.current_dossier_id', ...)`, and `set_config('app.current_exercice_id', ...)` on every connection open.
- [x] **P1.4:** Implement `Tenancy` module & PostgreSQL schema (`tenancy`):
  - Entities & migrations with composite keys: `Cabinet` `(id)`, `Dossier` `(cabinet_id, id)`, `Exercice` `(cabinet_id, dossier_id, id)`, and `DossierAssignment` `(cabinet_id, dossier_id, user_id)` with granular permission flags (`CanValidateOcr`, `CanPostEntries`, `CanLockBilan`).
  - `RedisPermissionChecker` caching user-to-dossier assignments (`cabinet:{cabinetId}:user:{userId}:dossiers`) with sub-millisecond lookup and cache invalidation on role change.
  - `TenantScopeMiddleware` in `ComptaShield.Api` resolving `CabinetId` from JWT and `X-Dossier-Id` / `X-Exercice-Id` from request headers, returning `403 Forbidden` on unauthorized dossier access.
  - MassTransit `TenantPublishFilter` and `TenantConsumeFilter` propagating `TenantMessageHeader` to async background workers.
- [x] **P1.5:** Write xUnit integration tests (`tests/ComptaShield.Tenancy.IntegrationTests`) verifying:
  - Cross-cabinet and cross-dossier isolation (Dossier A query never returns Dossier B rows via EF Core or raw SQL with RLS enabled).
  - `JuniorCollaborator` assigned to Dossier A receives `403 Forbidden` when sending `X-Dossier-Id: DossierB`.
  - `Partner` has automatic access to all Dossiers within their `CabinetId`.
  - Locked `Exercice` rejects any write operation.

---

## Phase 2: WORM Storage Vault & Hybrid Ingestion Pipeline (OCR + TEIF XML)
- [x] **P2.1:** Implement `S3WormStorageService` in `Ingestion` module storing inbound files at `/{cabinetId}/{dossierId}/{exerciceId}/inbound/{sha256}.{ext}` and signed deliverables at `/{cabinetId}/{dossierId}/{exerciceId}/deliverables/{docType}_{version}_{sha256}.pdf`.
- [x] **P2.2:** Implement `UploadDocumentCommand` with SHA-256 binary deduplication scoped per `(CabinetId, DossierId)` and Transactional Outbox publishing `DocumentUploadedIntegrationEvent` with `TenantMessageHeader`.
- [x] **P2.3:** Implement `TeifXmlInvoiceParser` (parsing structured e-invoice XML fields, supplier Matricule Fiscal, lines, TVA breakdown, and digital signature metadata) and `OcrExtractionWorker` (extracting JSONB fields, bounding box coordinates `[page, x, y, w, h]`, and per-field confidence scores `0.00`–`1.00`).
- [x] **P2.4:** Implement `DocumentMathAndConfidenceValidator`:
  - Verify `Sum(Qty * UnitPrice - Discount) == TotalHT` and `TotalHT + TotalTVA + TimbreFiscal == TotalTTC`.
  - Route any document with field confidence `< 0.95` or math discrepancy to `DocumentStatus.RequiresHumanReview`; route verified documents to `DocumentStatus.ReadyForPosting`.
- [x] **P2.5:** Write unit & integration tests verifying duplicate SHA-256 rejection, TEIF XML parsing, and low-confidence routing to HITL review queue.

---

## Phase 3: Forensic Fraud Detection & Rectification Engine ("FraudShield")
- [ ] **P3.1:** Implement `PdfMetadataForensicsScanner` inspecting PDF producer/creator tags (`Photoshop`, `Canva`, `GIMP`, `iLovePDF`), XMP modification history, and font subset anomalies on total lines.
- [ ] **P3.2:** Implement `SemanticDuplicateDetector` running composite fuzzy matching on `(SupplierMatriculeFiscal, InvoiceDate, TotalTTC, NormalizedInvoiceNumber)` + image perceptual hash (`pHash`) across current and prior `Exercice` records within the same `DossierId`.
- [ ] **P3.3:** Implement `ThreeWayMatchingEngine` reconciling Purchase Order (`BonDeCommande`) <-> Delivery Note (`BonDeReception`) <-> Supplier Invoice (`Facture`) <-> Bank Statement Line (`LigneReleveBancaire`), flagging phantom deliveries or unit price inflation.
- [ ] **P3.4:** Implement `BenfordLawAuditService` (calculating 1st-digit and 2nd-digit distributions across ledger accounts and running a Chi-Square goodness-of-fit test with $df=8$, flagging p-values $< 0.05$ and threshold-splitting clusters just below approval limits).
- [ ] **P3.5:** Implement `FraudRectificationService`:
  - Quarantine flagged documents from `Bilan` calculations.
  - Generate magic-link `ClientClarificationTicket` for `ClientGuest` upload portal.
  - Auto-generate reversing/adjusting journal entries (`Écriture de Contrepassation / Régularisation`) upon auditor confirmation.
- [ ] **P3.6:** Write xUnit test suite in `tests/ComptaShield.FraudShield.UnitTests` proving synthetic tampered PDFs, fuzzy duplicates, 3-way mismatches, and non-Benford fabricated numbers trigger accurate `FraudAlert` entities.

---

## Phase 4: Core Calculation Services (Ledger, Bilan, Inventaire, Audit)
- [ ] **P4.1:** Implement `LedgerAndBilan` module (`ledger` schema with RLS):
  - Hierarchical Chart of Accounts (`PlanComptable` classes 1 to 7 supporting SCE/PCG).
  - `JournalEntry` and `JournalEntryLine` with invariant `Sum(Debit) == Sum(Credit)` and PostgreSQL trigger `prevent_posted_entry_mutation()`.
  - `BilanCalculationService` generating `BalanceGenerale` (Trial Balance), `BilanActif`, `BilanPassif`, and `EtatDeResultat` with exact drill-down references to source `JournalEntry` and `Document` IDs.
- [ ] **P4.2:** Implement `Inventory` module (`inventory` schema with RLS):
  - `CumpValuationCalculator` (Weighted Average Cost after each receipt) and `FifoValuationCalculator` (First-In, First-Out lot tracking) using strict `decimal` math.
  - `InventoryReconciliationService` comparing `TheoreticalStock` vs. `PhysicalCount` (`PV d'Inventaire`), calculating shrinkage anomalies (`Écart d'Inventaire`), and generating inventory depreciation provisions.
- [ ] **P4.3:** Implement `Audit` module (`audit` schema with RLS):
  - Materiality calculator (`Seuil de Signification` & `Erreur Tolérable` based on revenue/equity/profit benchmarks).
  - Monetary Unit Sampling (MUS) / Stratified random sampling selector for audit workpapers.
  - Cryptographically hashed PDF/XML deliverable generator stamping final `Bilan`, `PV d'Inventaire`, and `AuditReport` into the Tier-2 Deliverables Vault.
- [ ] **P4.4:** Write golden-dataset accounting verification tests proving zero rounding drift across 1,000+ journal entries, CUMP/FIFO lots, and balanced `Actif == Passif + Capitaux Propres`.

---

## Phase 5: Angular 19+ Workspace, Split-Screen HITL Reviewer & Cockpits
- [ ] **P5.1:** Scaffold Angular 19+ application (`src/Web/comptashield-ui`) with Standalone Components, Signals, TailwindCSS, and `decimal.js`:
  - Implement `WorkspaceContextService` storing `ActiveWorkspace` (`cabinetId`, `dossierId`, `exerciceId`, `role`, `isLocked`) in **`sessionStorage`** for multi-tab isolation.
  - Implement `tenantHeadersInterceptor` (`HttpInterceptorFn`) injecting `X-Dossier-Id` and `X-Exercice-Id` headers on every request, plus header switcher UI for instant Dossier/Exercice switching.
- [ ] **P5.2:** Build **Split-Screen HITL OCR Validation Component**:
  - Left pane: HTML5 Canvas PDF/Image viewer rendering color-coded bounding boxes (`green` >= 0.95, `amber` 0.80–0.94, `red` < 0.80 or math failure).
  - Right pane: Reactive Form synced bi-directionally with bounding boxes (focusing an input pans/zooms the canvas to that field's coordinates; keyboard shortcuts `Enter` to jump to next low-confidence field, `Ctrl+Enter` to validate and post).
- [ ] **P5.3:** Build **Fraud Shield & Audit Cockpit**:
  - Interactive Benford's Law expected vs. actual digit bar chart, side-by-side duplicate invoice diff viewer, 3-way match status matrix, and one-click "Quarantine & Request Client Justification" workflow.
- [ ] **P5.4:** Build **Interactive Bilan & Inventaire Workspaces** with full drill-down (`Bilan Line` -> `Account` -> `Journal Entry` -> `Original Scanned Document`) and run end-to-end build & test verification across the entire solution.