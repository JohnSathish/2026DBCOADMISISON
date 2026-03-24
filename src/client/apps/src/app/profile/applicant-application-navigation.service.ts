import { Injectable, signal } from '@angular/core';

export interface ApplicantApplicationStep {
  index: number;
  title: string;
  description: string;
}

@Injectable({ providedIn: 'root' })
export class ApplicantApplicationNavigationService {
  private readonly stepsSignal = signal<ApplicantApplicationStep[]>([]);
  private readonly currentIndexSignal = signal(0);

  readonly steps = this.stepsSignal.asReadonly();
  readonly currentIndex = this.currentIndexSignal.asReadonly();

  registerSteps(steps: ApplicantApplicationStep[]): void {
    this.stepsSignal.set(steps);
    this.currentIndexSignal.update((current) => {
      if (!steps.length) {
        return 0;
      }
      return Math.min(current, steps.length);
    });
  }

  clear(): void {
    this.stepsSignal.set([]);
    this.currentIndexSignal.set(0);
  }

  setCurrentIndex(index: number): void {
    const steps = this.stepsSignal();
    if (!steps.length) {
      const prev = this.currentIndexSignal();
      this.currentIndexSignal.set(0);
      if (prev !== 0) {
        this.scheduleScrollFormToTop();
      }
      return;
    }
    const clamped = Math.max(0, Math.min(index, steps.length));
    const prev = this.currentIndexSignal();
    this.currentIndexSignal.set(clamped);
    if (clamped !== prev) {
      this.scheduleScrollFormToTop();
    }
  }

  /**
   * Scrolls the admission form back to the top when the step changes so users do not stay
   * scrolled to the bottom (Next/Save) after moving to the next section.
   */
  private scheduleScrollFormToTop(): void {
    queueMicrotask(() => {
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          const el = document.getElementById('application-form-top');
          if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
          } else {
            window.scrollTo({ top: 0, behavior: 'smooth' });
          }
        });
      });
    });
  }
}





