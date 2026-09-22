const toggle = document.querySelector<HTMLButtonElement>('[data-menu-toggle]');
const navigation = document.getElementById('sidebar');
toggle?.addEventListener('click', () => {
  const open = navigation?.classList.toggle('is-open') ?? false;
  toggle.setAttribute('aria-expanded', String(open));
});
const sessionUrl = document.body.dataset.sessionUrl;
const loginUrl = document.body.dataset.loginUrl;
if (sessionUrl && loginUrl) vigilarSesion(sessionUrl, loginUrl);
import { vigilarSesion } from './sesion';
import { iniciarComercial } from './comercial';

iniciarComercial();
