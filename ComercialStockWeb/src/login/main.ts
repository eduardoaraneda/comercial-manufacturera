document.querySelectorAll<HTMLButtonElement>('[data-toggle-password]').forEach(button => {
  button.addEventListener('click', () => {
    const input = document.getElementById(button.dataset.togglePassword ?? '') as HTMLInputElement | null;
    if (!input) return;
    const show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    button.textContent = show ? 'Ocultar' : 'Mostrar';
    button.setAttribute('aria-pressed', String(show));
    button.setAttribute('aria-label', show ? 'Ocultar contraseña' : 'Mostrar contraseña');
  });
});

document.querySelectorAll<HTMLFormElement>('[data-auth-form]').forEach(form => {
  form.addEventListener('submit', () => {
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]');
    if (button && form.checkValidity()) {
      button.disabled = true;
      button.textContent = 'Un momento…';
      form.setAttribute('aria-busy', 'true');
    }
  });
});

window.addEventListener('pageshow', () => {
  document.querySelectorAll<HTMLButtonElement>('[data-submit-label]').forEach(button => {
    button.disabled = false;
    button.textContent = button.dataset.submitLabel ?? 'Continuar';
    button.closest('form')?.removeAttribute('aria-busy');
  });
});

export {};
