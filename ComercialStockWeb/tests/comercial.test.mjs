import test from 'node:test';
import assert from 'node:assert/strict';
import { iniciarComercial } from '../src/site/comercial.ts';

test('cotización usa el precio del input y no el ID de la opción del producto', () => {
  const previous = globalThis.document;
  const total = { textContent: '' };
  const events = {};
  const values = [
    { product: '6', qty: '1', price: '54990.0000', discount: '0', tax: '19' },
    { product: '8', qty: '1', price: '29990.0000', discount: '0', tax: '19' },
    { product: '', qty: '1', price: '0', discount: '0', tax: '19' }
  ];
  const rows = values.map(fields => ({
    querySelector(selector) {
      // En el DOM la opción con data-price precede al input de precio.
      if (selector === '[data-price]') return { value: fields.product };
      const match = selector.match(/^(?:input)?\[data-(qty|price|discount|tax)\]$/);
      return match ? { value: fields[match[1]] } : null;
    }
  }));
  const lines = { querySelectorAll: () => rows, addEventListener: (name, fn) => { events[name] = fn; } };
  const form = { querySelector: selector => selector === '[data-lines]' ? lines : selector === '[data-total]' ? total : null };
  globalThis.document = {
    querySelectorAll: () => [],
    querySelector: selector => selector === '[data-document-form]' ? form : null
  };
  const expected = amount => `Total estimado: ${new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP' }).format(amount)}`;
  try {
    iniciarComercial();
    assert.equal(total.textContent, expected(101126));
    values[0].qty = '2';
    values[0].discount = '1000';
    events.input();
    assert.equal(total.textContent, expected(164184));
  } finally {
    globalThis.document = previous;
  }
});
