import { InjectionToken, Signal, inject } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  scan,
  startWith,
  switchMap,
} from 'rxjs';
import { SimulationApi } from '../../core/api/simulation.api';
import { errorMessage } from '../../core/http/problem-details';
import { SimulationRequest, SimulationResult } from '../../core/models/api.models';
import { sameSimulationRequest } from '../../core/util/loan';

/** Délai d'attente avant d'appeler `POST /simulations` (évite un appel par pixel de curseur). */
export const SIMULATION_DEBOUNCE_MS = new InjectionToken<number>('SIMULATION_DEBOUNCE_MS', {
  factory: () => 300,
});

export interface LiveSimulationState {
  status: 'idle' | 'loading' | 'success' | 'error';
  result: SimulationResult | null;
  error: string | null;
}

const IDLE: LiveSimulationState = { status: 'idle', result: null, error: null };

/**
 * Simulation « en direct » pilotée par un signal de requête (`null` = paramètres invalides).
 * Les appels sont regroupés (debounce), les réponses obsolètes annulées (switchMap) et le dernier
 * résultat reste affiché pendant le recalcul. À appeler dans un contexte d'injection.
 */
export function liveSimulation(
  request: Signal<SimulationRequest | null>,
): Signal<LiveSimulationState> {
  const api = inject(SimulationApi);
  const debounceMs = inject(SIMULATION_DEBOUNCE_MS);
  const state$ = toObservable(request).pipe(
    debounceTime(debounceMs),
    distinctUntilChanged(sameSimulationRequest),
    switchMap((req) =>
      req
        ? api.simulate(req).pipe(
            map((result): LiveSimulationState => ({ status: 'success', result, error: null })),
            catchError((error: unknown) =>
              of<LiveSimulationState>({
                status: 'error',
                result: null,
                error: errorMessage(error),
              }),
            ),
            startWith<LiveSimulationState>({ status: 'loading', result: null, error: null }),
          )
        : of(IDLE),
    ),
    scan(
      (previous, next) => (next.status === 'loading' ? { ...next, result: previous.result } : next),
      IDLE,
    ),
  );
  return toSignal(state$, { initialValue: IDLE });
}
