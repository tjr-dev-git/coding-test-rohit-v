import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';
import { DispatchApiService } from './dispatch-api.service';
import {
  Equipment,
  Movement,
  MovementPriority,
  MovementStatus,
  Site,
  UpdateMovementRequest,
  ValidationProblemDetails
} from './models';
import {
  allowedTransitions,
  formatSiteTime,
  isTerminal as isTerminalStatus,
  priorityIcon as priorityIconFor,
  requiresEquipment,
  statusIcon as statusIconFor,
  statusLabel as statusLabelFor
} from './movement-ui';

export interface ManageMovementDialogData {
  movement: Movement;
  equipment: Equipment[];
  site: Site;
}

export interface ManageDialogResult {
  updated?: Movement;
  conflict?: boolean;
}

@Component({
  selector: 'app-movement-manage-dialog',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './movement-manage-dialog.component.html',
  styleUrl: './movement-manage-dialog.component.scss'
})
export class MovementManageDialogComponent {
  readonly data = inject<ManageMovementDialogData>(MAT_DIALOG_DATA);
  readonly movement = this.data.movement;
  readonly equipment = this.data.equipment;
  readonly site = this.data.site;
  readonly terminal = isTerminalStatus(this.movement.status);
  readonly statusOptions: MovementStatus[] = [...allowedTransitions(this.movement.status)];
  readonly saving = signal(false);
  readonly conflict = signal(false);
  readonly conflictMovement = signal<Movement | null>(null);
  readonly serverMessages = signal<string[]>([]);

  private readonly api = inject(DispatchApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogRef = inject(MatDialogRef) as MatDialogRef<
    MovementManageDialogComponent,
    ManageDialogResult
  >;
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    status: this.fb.nonNullable.control<MovementStatus>(this.movement.status, [
      Validators.required
    ]),
    equipmentId: this.fb.control<string | null>(this.movement.equipmentId)
  });

  constructor() {
    this.updateEquipmentValidator(this.movement.status);

    this.form.controls.status.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(status => {
        this.updateEquipmentValidator(status);
        this.clearServerErrors();
      });

    this.form.controls.equipmentId.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.clearServerErrors());
  }

  submit(): void {
    if (this.saving() || this.terminal || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    if (!this.hasChanges()) {
      return;
    }

    const eTag = this.movement.eTag;
    if (!eTag) {
      this.serverMessages.set([
        'This movement has no version token. Close the dialog and reload the board.'
      ]);
      return;
    }

    this.clearServerErrors();
    this.conflict.set(false);
    this.conflictMovement.set(null);

    const request: UpdateMovementRequest = {
      status: this.form.controls.status.value,
      equipmentId: this.form.controls.equipmentId.value ?? null,
      eTag
    };

    this.saving.set(true);
    this.api
      .updateMovement(this.site.id, this.movement.id, request)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: updated => this.dialogRef.close({ updated }),
        error: error => this.handleError(error)
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  reload(): void {
    this.dialogRef.close({ conflict: true });
  }

  hasChanges(): boolean {
    const status = this.form.controls.status.value;
    const equipmentId = this.form.controls.equipmentId.value ?? null;
    return status !== this.movement.status || equipmentId !== this.movement.equipmentId;
  }

  statusLabel(status: MovementStatus): string {
    return statusLabelFor(status);
  }

  statusIcon(status: MovementStatus): string {
    return statusIconFor(status);
  }

  priorityIcon(priority: MovementPriority): string {
    return priorityIconFor(priority);
  }

  equipmentName(equipmentId: string | null): string {
    if (!equipmentId) {
      return 'Unassigned';
    }
    return this.equipment.find(item => item.id === equipmentId)?.displayName ?? equipmentId;
  }

  equipmentLabel(item: Equipment): string {
    const suffix = item.id === this.movement.equipmentId
      ? ' — currently assigned'
      : item.isAvailable
        ? ''
        : ' — unavailable';
    return `${item.displayName} (${item.id})${suffix}`;
  }

  equipmentOptionDisabled(item: Equipment): boolean {
    return !item.isAvailable && item.id !== this.movement.equipmentId;
  }

  equipmentHint(): string {
    const status = this.form.controls.status.value;
    if (requiresEquipment(status)) {
      return 'Required for Assigned and In progress movements.';
    }
    return this.movement.equipmentId
      ? 'Equipment stays assigned unless you choose another available unit.'
      : 'Optional for this status.';
  }

  siteTime(iso: string): string {
    return formatSiteTime(iso, this.site.timeZoneId);
  }

  private updateEquipmentValidator(status: MovementStatus): void {
    const control = this.form.controls.equipmentId;
    const required = requiresEquipment(status);
    control.setValidators(required ? [Validators.required] : []);
    if (required && !control.value) {
      control.markAsTouched();
    }
    control.updateValueAndValidity({ emitEvent: false });
  }

  private handleError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.serverMessages.set([
        'Unable to update the movement. Check your connection and try again.'
      ]);
      return;
    }

    if (error.status === 409) {
      this.conflictMovement.set(this.extractCurrent(error.error));
      this.conflict.set(true);
      this.serverMessages.set([]);
      return;
    }

    if (error.status === 400) {
      const messages = this.mapValidationErrors(error.error);
      this.serverMessages.set(
        messages.length > 0
          ? messages
          : ['The update was rejected. Check the values and try again.']
      );
      return;
    }

    if (error.status === 404) {
      this.serverMessages.set([
        'This movement no longer exists. Close the dialog and reload the board.'
      ]);
      return;
    }

    this.serverMessages.set([
      `Unable to update the movement (${error.status || 'network error'}). Try again.`
    ]);
  }

  private extractCurrent(payload: unknown): Movement | null {
    if (!payload || typeof payload !== 'object') {
      return null;
    }
    return (payload as ValidationProblemDetails).current ?? null;
  }

  private mapValidationErrors(payload: unknown): string[] {
    if (!payload || typeof payload !== 'object') {
      return [];
    }

    const errors = (payload as ValidationProblemDetails).errors;
    if (!errors) {
      return [];
    }

    const messages: string[] = [];
    for (const [field, fieldMessages] of Object.entries(errors)) {
      const message = fieldMessages.join(' ');
      messages.push(message);

      const control = this.controlFor(field);
      if (control) {
        control.setErrors({ ...(control.errors ?? {}), server: message });
        control.markAsTouched();
      }
    }

    return Array.from(new Set(messages));
  }

  private controlFor(field: string): AbstractControl | null {
    const key = Object.keys(this.form.controls).find(
      name => name.toLowerCase() === field.toLowerCase()
    );
    return key ? this.form.get(key) : null;
  }

  private clearServerErrors(): void {
    this.serverMessages.set([]);

    for (const control of Object.values(this.form.controls)) {
      const errors = control.errors;
      if (!errors || !errors['server']) {
        continue;
      }

      const remaining = { ...errors };
      delete remaining['server'];
      control.setErrors(Object.keys(remaining).length > 0 ? remaining : null);
    }
  }
}
