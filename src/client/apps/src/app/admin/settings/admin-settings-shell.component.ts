import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Hosts Settings child routes (users, roles, programs, courses, …) in a nested outlet. */
@Component({
  selector: 'app-admin-settings-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: '<router-outlet />',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminSettingsShellComponent {}
