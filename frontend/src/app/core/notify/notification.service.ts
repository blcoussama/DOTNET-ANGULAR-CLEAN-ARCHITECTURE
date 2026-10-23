import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorMessage } from '../http/problem-details';

/** Notifications courtes (snackbar). Annoncées aux lecteurs d'écran par le LiveAnnouncer du CDK. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Fermer', {
      duration: 4000,
      panelClass: ['cf-snack', 'cf-snack-success'],
      politeness: 'polite',
    });
  }

  info(message: string): void {
    this.snackBar.open(message, 'Fermer', {
      duration: 5000,
      panelClass: ['cf-snack', 'cf-snack-info'],
      politeness: 'polite',
    });
  }

  error(messageOrError: unknown): void {
    const message =
      typeof messageOrError === 'string' ? messageOrError : errorMessage(messageOrError);
    this.snackBar.open(message, 'Fermer', {
      duration: 7000,
      panelClass: ['cf-snack', 'cf-snack-error'],
      politeness: 'assertive',
    });
  }
}
