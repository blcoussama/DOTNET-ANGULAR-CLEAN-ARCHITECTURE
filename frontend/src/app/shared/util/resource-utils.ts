import { Resource, Signal, linkedSignal } from '@angular/core';

/**
 * Dernière valeur connue d'une ressource : conservée pendant un rechargement (pagination, filtres)
 * pour éviter que le tableau ne se vide à chaque requête.
 */
export function latestValue<T>(resource: Resource<T | undefined>): Signal<T | undefined> {
  return linkedSignal<T | undefined, T | undefined>({
    source: () => (resource.hasValue() ? resource.value() : undefined),
    computation: (next, previous) => next ?? previous?.value,
  });
}
