import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

/** Titre de l'onglet : « Page · CreditFlow » (annoncé par les lecteurs d'écran à chaque navigation). */
@Injectable({ providedIn: 'root' })
export class CreditFlowTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const pageTitle = this.buildTitle(snapshot);
    this.title.setTitle(pageTitle ? `${pageTitle} · CreditFlow` : 'CreditFlow');
  }
}
