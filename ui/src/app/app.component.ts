import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatToolbarModule } from '@angular/material/toolbar';
import { EMPTY, Subject, catchError, finalize, forkJoin, switchMap } from 'rxjs';
import { DispatchApiService } from './dispatch-api.service';
import { Equipment, Movement, MovementPriority, MovementStatus, ShiftKind, Site } from './models';
import {
  CreateMovementDialogData,
  MovementCreateDialogComponent
} from './movement-create-dialog.component';
import {
  ManageDialogResult,
  ManageMovementDialogData,
  MovementManageDialogComponent
} from './movement-manage-dialog.component';
import {
  formatSiteTime,
  isTerminal as isTerminalStatus,
  priorityIcon as priorityIconFor,
  siteShiftForInstant,
  statusIcon as statusIconFor,
  statusLabel as statusLabelFor
} from './movement-ui';

type PriorityFilter = 'all' | 'urgent';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
    MatTableModule,
    MatToolbarModule
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly api = inject(DispatchApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly refreshTrigger = new Subject<void>();
  private requestId = 0;

  readonly sites = signal<Site[]>([]);
  readonly equipment = signal<Equipment[]>([]);
  readonly movements = signal<Movement[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly search = signal('');
  readonly priorityFilter = signal<PriorityFilter>('all');

  readonly statuses: Array<MovementStatus | ''> = [
    '',
    'Requested',
    'Assigned',
    'InProgress',
    'Completed',
    'Cancelled'
  ];
  readonly displayedColumns = [
    'priority',
    'material',
    'route',
    'tonnes',
    'status',
    'equipment',
    'requested',
    'actions'
  ];

  siteId = 'IND-JSP';
  shiftDate = '2026-08-17';
  shift: ShiftKind = 'Day';
  status: MovementStatus | '' = '';

  readonly visibleMovements = computed(() => {
    const query = this.search().trim().toLowerCase();
    const urgentOnly = this.priorityFilter() === 'urgent';

    return this.movements().filter(movement => {
      if (urgentOnly && movement.priority !== 'Urgent') {
        return false;
      }

      if (!query) {
        return true;
      }

      return [
        movement.id,
        movement.materialType,
        movement.sourceLocation,
        movement.destinationLocation,
        movement.equipmentId ?? '',
        this.equipmentName(movement.equipmentId)
      ].some(value => value.toLowerCase().includes(query));
    });
  });

  readonly activeCount = computed(
    () => this.movements().filter(movement => !isTerminalStatus(movement.status)).length
  );

  readonly urgentCount = computed(
    () =>
      this.movements().filter(
        movement => movement.priority === 'Urgent' && !isTerminalStatus(movement.status)
      ).length
  );

  readonly unassignedCount = computed(
    () =>
      this.movements().filter(
        movement => !movement.equipmentId && !isTerminalStatus(movement.status)
      ).length
  );

  constructor() {
    this.refreshTrigger
      .pipe(
        switchMap(() => {
          const requestId = ++this.requestId;
          this.loading.set(true);

          return forkJoin({
            movements: this.api.getMovements(
              this.siteId,
              this.shiftDate,
              this.shift,
              this.status
            ),
            equipment: this.api.getEquipment(this.siteId)
          }).pipe(
            catchError(error => {
              if (requestId === this.requestId) {
                this.handleLoadError(error);
              }
              return EMPTY;
            }),
            finalize(() => {
              if (requestId === this.requestId) {
                this.loading.set(false);
              }
            })
          );
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(result => {
        this.movements.set(result.movements);
        this.equipment.set(result.equipment);
      });

    this.loadInitialData();
  }

  loadInitialData(): void {
    this.loading.set(true);
    this.error.set('');

    this.api
      .getSites()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: sites => {
          this.sites.set(sites);

          if (sites.length === 0) {
            this.loading.set(false);
            this.error.set('No sites are available for dispatch.');
            return;
          }

          if (!sites.some(site => site.id === this.siteId)) {
            this.siteId = sites[0].id;
          }

          this.refresh();
        },
        error: error => this.handleLoadError(error)
      });
  }

  refresh(): void {
    if (!this.siteId) {
      return;
    }
    this.refreshTrigger.next();
  }

  retry(): void {
    if (this.siteId) {
      this.refresh();
    } else {
      this.loadInitialData();
    }
  }

  selectedSite(): Site | undefined {
    return this.sites().find(site => site.id === this.siteId);
  }

  onSiteChanged(siteId: string): void {
    this.siteId = siteId;
    this.refresh();
  }

  onSearchChanged(value: string): void {
    this.search.set(value);
  }

  onPriorityFilterChanged(value: PriorityFilter): void {
    this.priorityFilter.set(value);
  }

  clearFilters(): void {
    this.search.set('');
    this.priorityFilter.set('all');
    this.status = '';
    this.refresh();
  }

  setCurrentShift(): void {
    const site = this.selectedSite();
    if (!site) {
      return;
    }

    const target = siteShiftForInstant(new Date().toISOString(), site.timeZoneId);
    this.shiftDate = target.shiftDate;
    this.shift = target.shift;
    this.refresh();
  }

  isFiltered(): boolean {
    return (
      this.search().trim().length > 0 ||
      this.priorityFilter() !== 'all' ||
      this.status !== ''
    );
  }

  equipmentName(equipmentId: string | null): string {
    if (!equipmentId) {
      return 'Unassigned';
    }
    return this.equipment().find(item => item.id === equipmentId)?.displayName ?? equipmentId;
  }

  siteTime(iso: string): string {
    return formatSiteTime(iso, this.selectedSite()?.timeZoneId ?? 'UTC');
  }

  statusLabel(status: MovementStatus): string {
    return statusLabelFor(status);
  }

  statusOptionLabel(status: MovementStatus | ''): string {
    return status ? statusLabelFor(status) : 'All statuses';
  }

  statusIcon(status: MovementStatus): string {
    return statusIconFor(status);
  }

  priorityIcon(priority: MovementPriority): string {
    return priorityIconFor(priority);
  }

  isTerminal(status: MovementStatus): boolean {
    return isTerminalStatus(status);
  }

  openCreate(): void {
    const site = this.selectedSite();
    if (!site) {
      return;
    }

    this.dialog
      .open<MovementCreateDialogComponent, CreateMovementDialogData, Movement>(
        MovementCreateDialogComponent,
        {
          width: '680px',
          maxWidth: '95vw',
          data: { site }
        }
      )
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(created => {
        if (!created) {
          return;
        }

        const target = siteShiftForInstant(created.requestedAtUtc, site.timeZoneId);
        const moved = target.shiftDate !== this.shiftDate || target.shift !== this.shift;

        if (moved) {
          this.shiftDate = target.shiftDate;
          this.shift = target.shift;
        }

        // Clear filters so the newly created movement is definitely visible.
        this.status = '';
        this.search.set('');
        this.priorityFilter.set('all');
        this.refresh();

        this.snackBar.open(
          moved
            ? `Movement created. Showing the ${target.shift.toLowerCase()} shift for ${target.shiftDate}.`
            : 'Movement created for the current shift.',
          'Dismiss',
          { duration: 5000, politeness: 'polite' }
        );
      });
  }

  openManage(movement: Movement): void {
    const site = this.selectedSite();
    if (!site) {
      return;
    }

    this.dialog
      .open<MovementManageDialogComponent, ManageMovementDialogData, ManageDialogResult>(
        MovementManageDialogComponent,
        {
          width: '720px',
          maxWidth: '95vw',
          data: { movement, equipment: this.equipment(), site }
        }
      )
      .afterClosed()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(result => {
        if (!result) {
          return;
        }

        if (result.conflict) {
          this.refresh();
          this.snackBar.open(
            'Another dispatcher changed this movement. The board is reloading.',
            'Dismiss',
            { duration: 6000, politeness: 'assertive' }
          );
          return;
        }

        if (!result.updated) {
          return;
        }

        const updated = result.updated;
        const activeStatus = this.status;

        if (activeStatus && updated.status !== activeStatus) {
          this.movements.update(list => list.filter(item => item.id !== updated.id));
          this.snackBar.open(
            `Movement updated to ${this.statusLabel(updated.status)}. It no longer matches the ${this.statusLabel(activeStatus)} filter.`,
            'Dismiss',
            { duration: 6000, politeness: 'polite' }
          );
          return;
        }

        this.movements.update(list =>
          list.map(item => (item.id === updated.id ? updated : item))
        );
        this.snackBar.open(
          `Movement updated to ${this.statusLabel(updated.status)}.`,
          'Dismiss',
          { duration: 4000, politeness: 'polite' }
        );
      });
  }

  private handleLoadError(error: unknown): void {
    this.loading.set(false);
    this.error.set(
      error instanceof HttpErrorResponse
        ? `Unable to load dispatch data (${error.status || 'network error'}).`
        : 'Unable to load dispatch data.'
    );
  }
}
