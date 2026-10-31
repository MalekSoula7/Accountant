import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { WorkspaceContextService } from '../services/workspace-context.service';

/**
 * Functional HTTP Interceptor injecting multi-tenancy headers into every outgoing request:
 * - X-Dossier-Id: Current Dossier UUID from sessionStorage
 * - X-Exercice-Id: Current Exercice UUID from sessionStorage
 * - Authorization: Bearer JWT token if present
 */
export const tenantHeadersInterceptor: HttpInterceptorFn = (req, next) => {
  const workspaceService = inject(WorkspaceContextService);
  const workspace = workspaceService.activeWorkspace();

  let headers = req.headers;

  if (workspace.dossierId) {
    headers = headers.set('X-Dossier-Id', workspace.dossierId);
  }

  if (workspace.exerciceId) {
    headers = headers.set('X-Exercice-Id', workspace.exerciceId);
  }

  if (workspace.token) {
    headers = headers.set('Authorization', `Bearer ${workspace.token}`);
  }

  const clonedReq = req.clone({ headers });
  return next(clonedReq);
};
