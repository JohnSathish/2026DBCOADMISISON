import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';
import { appRoutes } from './app.routes';
import { API_BASE_URL } from '@client/shared/util';
import { AuthService } from './auth/auth.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter(appRoutes),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: '/api' },
        {
          provide: AuthService,
          useValue: {
            profile: null,
            logout: jest.fn(),
          },
        },
      ],
    }).compileComponents();
  });

  it('should render registration with college name after navigating to /register', async () => {
    const fixture = TestBed.createComponent(App);
    const router = TestBed.inject(Router);
    await fixture.ngZone?.run(() => router.navigateByUrl('/register'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const h1 = compiled.querySelector('h1');
    expect(h1?.textContent).toContain('Don Bosco College');
    expect(compiled.querySelector('.app-shell__footer')?.textContent).toContain(
      'Don Bosco College Tura'
    );
  });
});
