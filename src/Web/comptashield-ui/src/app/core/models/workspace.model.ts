export type DossierRole = 'Partner' | 'SeniorAuditor' | 'JuniorCollaborator' | 'ClientGuest';

export interface ActiveWorkspace {
  cabinetId: string;
  cabinetName: string;
  dossierId: string;
  dossierName: string;
  exerciceId: string;
  exerciceYear: number;
  role: DossierRole;
  isLocked: boolean;
  token?: string;
}

export interface AvailableDossier {
  id: string;
  name: string;
  matriculeFiscal: string;
}

export interface AvailableExercice {
  id: string;
  year: number;
  isLocked: boolean;
}
