import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators
} from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { finalize } from 'rxjs';
import { DispatchApiService } from './dispatch-api.service';
import {
  CreateMovementRequest,
  Movement,
  MovementPriority,
  Site,
  ValidationProblemDetails
} from './models';

export interface CreateMovementDialogData {
  site: Site;
}

function positiveTonnes(control: AbstractControl): ValidationErrors | null {
  const value = Number(control.value);
  return Number.isFinite(value) && value > 0 ? null : { positive: true };
}

function differentLocations(group: AbstractControl): ValidationErrors | null {
  const source = String(group.get('sourceLocation')?.value ?? '').trim().toLowerCase();
  const destination = String(group.get('destinationLocation')?.value ?? '').trim().toLowerCase();
  return source.length > 0 && destination.length > 0 && source === destination
    ? { sameRoute: true }
    : null;
}

@Component({
  selector: 'app-movement-create-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule
  ],
  templateUrl: './movement-create-dialog.component.html',
  styleUrl: './movement-create-dialog.component.scss'
})
export class MovementCreateDialogComponent {
  readonly data = inject<CreateMovementDialogData>(MAT_DIALOG_DATA);
  readonly site = this.data.site;
  readonly saving = signal(false);
  readonly serverMessages = signal<string[]>([]);
  readonly priorities: MovementPriority[] = ['Normal', 'Urgent'];

  private readonly api = inject(DispatchApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialogRef = inject(MatDialogRef) as MatDialogRef<
    MovementCreateDialogComponent,
    Movement
  >;
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    materialType: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(100)
    ]),
    sourceLocation: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(120)
    ]),
    destinationLocation: this.fb.nonNullable.control('', [
      Validators.required,
      Validators.minLength(2),
      Validators.maxLength(120)
    ]),
    estimatedTonnes: this.fb.control<number | null>(null, [
      Validators.required,
      positiveTonnes,
      Validators.max(500)
    ]),
    priority: this.fb.nonNullable.control<MovementPriority>('Normal', [
      Validators.required
    ])
  }, { validators: differentLocations });

  constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.clearServerErrors());
  }

  submit(): void {
    if (this.saving() || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.clearServerErrors();
    const value = this.form.getRawValue();
    if (value.estimatedTonnes === null) {
      return;
    }

    const request: CreateMovementRequest = {
      materialType: value.materialType.trim(),
      sourceLocation: value.sourceLocation.trim(),
      destinationLocation: value.destinationLocation.trim(),
      estimatedTonnes: Number(value.estimatedTonnes),
      priority: value.priority
    };

    this.saving.set(true);
    this.api
      .createMovement(this.site.id, request)
      .pipe(
        finalize(() => this.saving.set(false)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: movement => this.dialogRef.close(movement),
        error: error => this.applyServerError(error)
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  showSameRouteError(): boolean {
    const source = this.form.controls.sourceLocation;
    const destination = this.form.controls.destinationLocation;
    return this.form.hasError('sameRoute') && (source.touched || destination.touched);
  }

  private applyServerError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.serverMessages.set([
        'Unable to create the movement. Check your connection and try again.'
      ]);
      return;
    }

    if (error.status === 400) {
      const messages = this.mapValidationErrors(error.error);
      this.serverMessages.set(
        messages.length > 0
          ? messages
          : ['The movement could not be created. Check the fields and try again.']
      );
      return;
    }

    if (error.status === 404) {
      this.serverMessages.set([
        'This site is no longer available. Close the dialog and reload the board.'
      ]);
      return;
    }

    if (error.status === 409) {
      this.serverMessages.set([
        'The movement could not be created because of a conflict. Close the dialog and reload the board.'
      ]);
      return;
    }

    this.serverMessages.set([
      `Unable to create the movement (${error.status || 'network error'}). Try again.`
    ]);
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
