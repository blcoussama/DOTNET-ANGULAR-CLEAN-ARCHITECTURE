// Configuration Vitest complémentaire, chargée par le builder Angular (option runnerConfig).
// Délais relevés : sur une machine (ou un runner de CI) saturée, le premier test d'un fichier peut
// dépasser les 5 s par défaut le temps de compiler et d'initialiser TestBed, sans être en échec.
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    testTimeout: 30_000,
    hookTimeout: 30_000,
  },
});
