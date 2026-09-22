type EstadoSesion = { expiresAt: string; serverNow: string };

// El servidor impone el vencimiento. Este observador solo actualiza la pantalla.
export function vigilarSesion(sessionUrl: string, loginUrl: string): void {
  let timer: number | undefined;
  let consultando = false;
  let redirigiendo = false;

  function volverAlLogin(): void {
    redirigiendo = true;
    const login = new URL(loginUrl, window.location.origin);
    login.searchParams.set('sesionExpirada', 'true');
    login.searchParams.set('returnUrl', window.location.pathname + window.location.search);
    window.location.replace(login.href);
  }

  function programar(ms: number): void {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => { void comprobar(); }, ms);
  }

  async function comprobar(): Promise<void> {
    if (consultando || redirigiendo) return;
    consultando = true;
    try {
      const response = await fetch(sessionUrl, {
        credentials: 'same-origin', cache: 'no-store',
        headers: { Accept: 'application/json' }, signal: AbortSignal.timeout(10000)
      });
      if (response.status === 401) { volverAlLogin(); return; }
      // Un fallo de red o de SQL no es prueba de vencimiento: reintentar.
      if (!response.ok) { programar(15000); return; }
      const state = await response.json() as EstadoSesion;
      // Ambas fechas vienen del servidor; no dependemos del reloj del equipo.
      const remaining = Date.parse(state.expiresAt) - Date.parse(state.serverNow);
      if (!Number.isFinite(remaining)) { programar(15000); return; }
      // Se vuelve a consultar al vencer, para respetar un login nuevo en otra pestaña.
      programar(Math.min(30000, Math.max(250, remaining + 250)));
    } catch {
      programar(15000);
    } finally {
      consultando = false;
    }
  }

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') void comprobar();
  });
  window.addEventListener('focus', () => { void comprobar(); });
  window.addEventListener('pageshow', () => { void comprobar(); });
  void comprobar();
}
