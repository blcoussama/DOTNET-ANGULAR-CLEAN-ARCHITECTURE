import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { COMPLETE_PROFILE } from '../../../../testing/fixtures';
import {
  provideTestEnvironment,
  query,
  settle,
  signIn,
  testId,
  text,
  typeInto,
} from '../../../../testing/test-helpers';
import { adultAgeValidator, isProfileComplete, positiveValidator } from './profile-form';
import { ProfilePage } from './profile-page';
import { FormControl } from '@angular/forms';

describe('ProfilePage — profil financier', () => {
  let fixture: ComponentFixture<ProfilePage>;
  let http: HttpTestingController;

  async function create(profile: object | null, status = 200): Promise<void> {
    fixture = TestBed.createComponent(ProfilePage);
    await settle(fixture);
    http
      .expectOne('/api/v1/clients/me/profile')
      .flush(profile, { status, statusText: status === 200 ? 'OK' : 'Not Found' });
    await settle(fixture);
  }

  function submit(): void {
    query<HTMLFormElement>(fixture, 'form')!.dispatchEvent(new Event('submit'));
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ProfilePage], providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
    signIn('Client');
  });

  afterEach(() => http.verify());

  it('charge le profil et affiche l’indicateur « profil complet »', async () => {
    await create(COMPLETE_PROFILE);
    expect(testId(fixture, 'profile-complete')).not.toBeNull();
    expect(query<HTMLInputElement>(fixture, 'input[formcontrolname="city"]')!.value).toBe('Tanger');
    expect(text(fixture)).toContain('100 % des informations obligatoires');
    expect(testId(fixture, 'current-debt')?.textContent).toContain('13,3');
  });

  it('présente un profil vide (404) comme incomplet', async () => {
    await create(null, 404);
    expect(testId(fixture, 'profile-incomplete')?.textContent).toContain('Profil incomplet');
    expect(text(fixture)).toContain('0 % des informations obligatoires');
  });

  it('bloque l’enregistrement si le revenu n’est pas strictement positif', async () => {
    await create(COMPLETE_PROFILE);
    typeInto(query(fixture, 'input[formcontrolname="monthlyIncome"]') as HTMLInputElement, '0');
    submit();
    await settle(fixture);
    expect(text(fixture)).toContain('Le revenu doit être strictement positif.');
    http.expectNone('/api/v1/clients/me/profile');
  });

  it('enregistre le profil en PUT avec des valeurs typées', async () => {
    await create(null, 404);
    const fields: Record<string, string> = {
      dateOfBirth: '1988-02-01',
      phone: '+212 612345678',
      city: ' Rabat ',
      yearsAtCurrentJob: '3',
      monthlyIncome: '12000',
      monthlyCharges: '1500',
    };
    for (const [name, value] of Object.entries(fields)) {
      typeInto(query(fixture, `input[formcontrolname="${name}"]`) as HTMLInputElement, value);
    }
    fixture.componentInstance['form'].controls.employmentType.setValue('FONCTIONNAIRE');
    submit();
    await settle(fixture);
    const req = http.expectOne('/api/v1/clients/me/profile');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      dateOfBirth: '1988-02-01',
      phone: '+212 612345678',
      city: 'Rabat',
      employmentType: 'FONCTIONNAIRE',
      employer: null,
      monthlyIncome: 12000,
      monthlyCharges: 1500,
      yearsAtCurrentJob: 3,
    });
    req.flush({ ...req.request.body, isComplete: true });
    await settle(fixture);
    expect(testId(fixture, 'profile-complete')).not.toBeNull();
  });

  it('affiche les erreurs de validation renvoyées par l’API sur les champs', async () => {
    await create(COMPLETE_PROFILE);
    typeInto(query(fixture, 'input[formcontrolname="city"]') as HTMLInputElement, 'Casa');
    submit();
    await settle(fixture);
    http.expectOne('/api/v1/clients/me/profile').flush(
      {
        status: 400,
        title: 'One or more validation errors occurred.',
        errors: { City: ['Ville non desservie.'] },
      },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle(fixture);
    expect(text(fixture)).toContain('Ville non desservie.');
  });

  it('valide l’âge, la positivité et la complétude', () => {
    expect(adultAgeValidator(new FormControl('2020-01-01') as never)).toEqual({ minAge: 18 });
    expect(adultAgeValidator(new FormControl('1990-01-01') as never)).toBeNull();
    expect(positiveValidator(new FormControl(0) as never)).toEqual({ positive: true });
    expect(isProfileComplete({ ...COMPLETE_PROFILE, isComplete: undefined })).toBe(true);
    expect(isProfileComplete({ ...COMPLETE_PROFILE, isComplete: undefined, city: null })).toBe(
      false,
    );
  });
});
