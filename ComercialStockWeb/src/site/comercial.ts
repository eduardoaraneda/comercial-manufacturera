export function iniciarComercial(): void {
  document.querySelectorAll<HTMLButtonElement>('[data-print]').forEach(b => b.addEventListener('click', () => window.print()));
  document.querySelectorAll<HTMLButtonElement>('[data-back]').forEach(b => b.addEventListener('click', () => window.history.back()));
  document.querySelectorAll<HTMLFormElement>('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => { if (!window.confirm(form.dataset.confirm)) event.preventDefault(); });
  });
  document.querySelector<HTMLInputElement>('[data-table-search]')?.addEventListener('input', event => {
    const term = (event.target as HTMLInputElement).value.toLocaleLowerCase();
    document.querySelectorAll<HTMLTableRowElement>('[data-search-table] tbody tr').forEach(row => {
      row.hidden = !(row.textContent ?? '').toLocaleLowerCase().includes(term);
    });
  });
  const form = document.querySelector<HTMLFormElement>('[data-document-form]');
  const lines = form?.querySelector<HTMLTableSectionElement>('[data-lines]');
  if (!form || !lines) return;
  const total = form.querySelector<HTMLElement>('[data-total]');
  function calcular(): void {
    let amount = 0;
    lines!.querySelectorAll<HTMLTableRowElement>('[data-line]').forEach(row => {
      const read = (name: string) => Number(row.querySelector<HTMLInputElement>(`input[data-${name}]`)?.value ?? 0);
      const net = Math.round(read('qty') * (read('price') - read('discount')));
      amount += net + Math.round(net * read('tax') / 100);
    });
    if (total) total.textContent = `Total estimado: ${new Intl.NumberFormat('es-CL', { style: 'currency', currency: 'CLP' }).format(amount)}`;
  }
  function numerar(): void {
    lines!.querySelectorAll<HTMLTableRowElement>('[data-line]').forEach((row, index) => {
      row.querySelectorAll<HTMLInputElement | HTMLSelectElement>('input,select').forEach(input => {
        input.name = input.name.replace(/Lineas\[\d+\]/, `Lineas[${index}]`);
        input.removeAttribute('id');
      });
    });
    calcular();
  }
  form.querySelector('[data-add-line]')?.addEventListener('click', () => {
    if (lines.rows.length >= 100) return;
    const row = lines.rows[0]?.cloneNode(true) as HTMLTableRowElement | undefined;
    if (!row) return;
    row.querySelector<HTMLSelectElement>('select')!.selectedIndex = 0;
    row.querySelectorAll<HTMLInputElement>('input').forEach(input => {
      input.value = input.hasAttribute('data-qty') ? '1' : input.hasAttribute('data-tax') ? '19' : '0';
    });
    lines.append(row); numerar();
  });
  lines.addEventListener('click', event => {
    const button = (event.target as Element).closest('[data-remove-line]');
    if (button && lines.rows.length > 1) { button.closest('tr')?.remove(); numerar(); }
  });
  lines.addEventListener('change', event => {
    const input = event.target as HTMLSelectElement;
    if (input.matches('[data-product]')) {
      const price = input.closest('tr')?.querySelector<HTMLInputElement>('input[data-price]');
      if (price) price.value = input.selectedOptions[0]?.dataset.price ?? '0';
    }
    calcular();
  });
  lines.addEventListener('input', calcular);
  calcular();
}
