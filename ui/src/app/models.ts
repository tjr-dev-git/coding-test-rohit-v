export type MovementPriority = 'Normal' | 'Urgent';
export type MovementStatus = 'Requested' | 'Assigned' | 'InProgress' | 'Completed' | 'Cancelled';
export type ShiftKind = 'Day' | 'Night';

export interface Site {
  id: string;
  name: string;
  timeZoneId: string;
}

export interface Equipment {
  id: string;
  siteId: string;
  displayName: string;
  isAvailable: boolean;
}

export interface Movement {
  id: string;
  siteId: string;
  materialType: string;
  sourceLocation: string;
  destinationLocation: string;
  estimatedTonnes: number;
  priority: MovementPriority;
  status: MovementStatus;
  equipmentId: string | null;
  requestedAtUtc: string;
  lastUpdatedAtUtc: string;
  eTag: string | null;
}

export interface CreateMovementRequest {
  materialType: string;
  sourceLocation: string;
  destinationLocation: string;
  estimatedTonnes: number;
  priority: MovementPriority;
}

export interface UpdateMovementRequest {
  status: MovementStatus;
  equipmentId: string | null;
  eTag: string;
}

export interface ValidationProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  current?: Movement;
}

