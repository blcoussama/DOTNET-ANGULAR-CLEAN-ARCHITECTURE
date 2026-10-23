import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideTestEnvironment, settle, signIn, text } from '../../testing/test-helpers';
import { navItemsFor } from './nav-items';
import { Shell } from './shell';

describe('Shell — navigation adaptée au rôle', () => {
  let fixture: ComponentFixture<Shell>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [Shell], providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    document.querySelector('.cdk-overlay-container')?.replaceChildren();
  });

  it('calcule les entrées de menu par rôle', () => {
    expect(navItemsFor('Client').map((i) => i.link)).toContain('/client/profil');
    expect(navItemsFor('Analyst').map((i) => i.link)).toEqual([
      '/analyste/file',
      '/analyste/tableau-de-bord',
    ]);
    expect(navItemsFor('Admin').map((i) => i.link)).toContain('/admin/produits');
    expect(navItemsFor(null)).toEqual([]);
  });

  it('montre les liens publics au visiteur', async () => {
    fixture = TestBed.createComponent(Shell);
    await settle(fixture);
    expect(text(fixture)).toContain('Connexion');
    expect(text(fixture)).toContain('Créer un compte');
    expect(
      fixture.nativeElement.querySelector('nav[aria-label="Navigation principale"]'),
    ).toBeNull();
    expect(fixture.nativeElement.querySelector('a.cf-skip-link')).not.toBeNull();
  });

  it('montre la navigation de l’analyste, son nom et la déconnexion', async () => {
    signIn('Analyst');
    const navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    fixture = TestBed.createComponent(Shell);
    await settle(fixture);
    const links = [
      ...fixture.nativeElement.querySelectorAll('nav[aria-label="Navigation principale"] a span'),
    ].map((s) => (s as HTMLElement).textContent?.trim());
    expect(links).toEqual(["File d'attente", 'Tableau de bord']);
    expect(text(fixture)).toContain('Youssef Alaoui');

    (fixture.nativeElement.querySelector('.cf-user-button') as HTMLButtonElement).click();
    await settle(fixture);
    const logout = [...document.querySelectorAll('.cdk-overlay-container button')].find((b) =>
      b.textContent?.includes('Se déconnecter'),
    ) as HTMLButtonElement;
    logout.click();
    http.expectOne('/api/v1/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);
    expect(navigateByUrl).toHaveBeenCalledWith('/connexion');
    expect(text(fixture)).toContain('Créer un compte');
  });
});
