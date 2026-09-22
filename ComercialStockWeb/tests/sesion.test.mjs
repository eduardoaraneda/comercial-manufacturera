import test from 'node:test';
import assert from 'node:assert/strict';
import { vigilarSesion } from '../src/site/sesion.ts';

test('Observador de sesion', async t => {
  const originalFetch = globalThis.fetch;
  const settle = () => new Promise(resolve => setImmediate(resolve));
  function environment(responses) {
    const redirects = [];
    const timers = [];
    const events = {};
    globalThis.window = {
      location: { origin: 'https://localhost', pathname: '/Home', search: '?vista=1', replace: value => redirects.push(value) },
      clearTimeout: () => {},
      setTimeout: (callback, ms) => { timers.push({ callback, ms }); return timers.length; },
      addEventListener: (name, handler) => { events[name] = handler; }
    };
    globalThis.document = { visibilityState: 'visible', addEventListener: (name, handler) => { events[name] = handler; } };
    globalThis.fetch = async () => {
      const response = responses.shift();
      if (response instanceof Error) throw response;
      assert.ok(response, 'Respuesta de prueba disponible');
      return response;
    };
    return { redirects, timers, events };
  }
  try {
    await t.test('401 redirige al login con ruta local de retorno', async () => {
      const state = environment([new Response('{}', { status: 401 })]);
      vigilarSesion('/Sesion/Estado', '/Login');
      await settle();
      const target = new URL(state.redirects[0]);
      assert.equal(target.pathname, '/Login');
      assert.equal(target.searchParams.get('sesionExpirada'), 'true');
      assert.equal(target.searchParams.get('returnUrl'), '/Home?vista=1');
    });
    await t.test('programa vencimiento usando fechas del servidor y lo comprueba al vencer', async () => {
      const state = environment([
        Response.json({ expiresAt: '2030-01-01T00:00:05Z', serverNow: '2030-01-01T00:00:00Z' }),
        new Response('{}', { status: 401 })
      ]);
      vigilarSesion('/Sesion/Estado', '/Login');
      await settle();
      assert.equal(state.timers[0].ms, 5250);
      assert.equal(state.redirects.length, 0);
      state.timers[0].callback();
      await settle();
      assert.equal(state.redirects.length, 1);
    });
    await t.test('fallo de red reintenta y recupera al volver a la pestaña', async () => {
      const state = environment([new Error('offline'), new Response('{}', { status: 401 })]);
      vigilarSesion('/Sesion/Estado', '/Login');
      await settle();
      assert.equal(state.redirects.length, 0);
      assert.equal(state.timers[0].ms, 15000);
      state.events.visibilitychange();
      await settle();
      assert.equal(state.redirects.length, 1);
    });
  } finally {
    globalThis.fetch = originalFetch;
    delete globalThis.window;
    delete globalThis.document;
  }
});
