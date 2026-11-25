const $ = (selector) => document.querySelector(selector);
const money = new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'DOP' });
const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));

async function loadCategories() {
  const response = await fetch('/api/categorias');
  if (!response.ok) throw new Error('No se pudieron cargar las categorías.');
  for (const category of await response.json()) {
    $('#category').insertAdjacentHTML('beforeend', `<option value="${category.idCategoria}">${escapeHtml(category.nombre)}</option>`);
  }
}

async function loadProducts() {
  const params = new URLSearchParams({ proveedor: $('#provider').value });
  if ($('#search').value.trim()) params.set('texto', $('#search').value.trim());
  if ($('#category').value) params.set('categoria', $('#category').value);
  $('#products').innerHTML = '<tr><td colspan="7" class="empty">Buscando productos…</td></tr>';
  try {
    const response = await fetch(`/api/productos?${params}`);
    if (!response.ok) throw new Error('No se pudieron cargar los productos.');
    const products = await response.json();
    $('#count').textContent = products.length.toLocaleString('es-DO');
    $('#active-provider').textContent = $('#provider').value === 'ado' ? 'ADO.NET' : 'EF Core';
    $('#result-label').textContent = products.length ? `${products.length} producto(s)` : '';
    $('#products').innerHTML = products.length ? products.map(p => `<tr><td>${escapeHtml(p.nombre)}</td><td>${escapeHtml(p.codigo)}</td><td>${escapeHtml(p.marca)}</td><td>${escapeHtml(p.nombreCategoria || `Categoría ${p.idCategoria}`)}</td><td>${money.format(p.precio)}</td><td class="stock ${p.stock < 5 ? 'low' : ''}">${p.stock}</td><td><button class="choose" data-id="${p.idProducto}">Vender</button></td></tr>`).join('') : '<tr><td colspan="7" class="empty">No hay productos para esta búsqueda.</td></tr>';
    document.querySelectorAll('.choose').forEach(button => button.addEventListener('click', () => { $('#sale-product').value = button.dataset.id; $('#sale-product').focus(); }));
  } catch (error) {
    $('#products').innerHTML = `<tr><td colspan="7" class="empty">${escapeHtml(error.message)}</td></tr>`;
  }
}

$('#refresh').addEventListener('click', loadProducts);
$('#provider').addEventListener('change', loadProducts);
$('#category').addEventListener('change', loadProducts);
$('#search').addEventListener('keydown', event => { if (event.key === 'Enter') loadProducts(); });
const fileLink = (file) => `<a href="/api/archivo/${encodeURIComponent(file.nombre)}">${escapeHtml(file.nombre)}</a>`;
async function loadReview() {
  const response = await fetch('/api/revision');
  if (!response.ok) throw new Error('No se pudo cargar la revisión.');
  const data = await response.json();
  const files = Object.fromEntries(data.archivos.map(file => [file.nombre, file]));
  const show = names => names.map(name => files[name].existe ? `<div><span class="available">●</span> ${fileLink(files[name])}</div>` : `<div><span class="missing">●</span> ${escapeHtml(name)} <small>(pendiente)</small></div>`).join('');
  $('#db-files').innerHTML = show(['TechStoreDB.sql', 'SeedTechStoreDbEf.sql']);
  $('#report-files').innerHTML = show(['ResultadosRendimiento.csv', 'MetricasDesarrollo.csv', 'InformeTecnico.pdf', 'ConsultaReporteEF.sql']);
  $('#benchmarks').innerHTML = data.resultados.length ? data.resultados.map(line => {
    const values = line.match(/("(?:[^"]|"")*"|[^,]*)(,|$)/g)?.filter(Boolean).map(x => x.replace(/,$/, '').replace(/^"|"$/g, '').replace(/""/g, '"')) ?? [];
    return `<tr>${Array.from({length: 4}, (_, i) => `<td>${escapeHtml(values[i] || '—')}</td>`).join('')}</tr>`;
  }).join('') : '<tr><td colspan="4" class="empty">Aún no hay resultados. Ejecuta las pruebas de rendimiento en la consola.</td></tr>';
}
$('#reload-review').addEventListener('click', loadReview);
$('#sale-form').addEventListener('submit', async event => {
  event.preventDefault();
  const form = new FormData(event.currentTarget);
  const message = $('#message');
  message.className = 'message'; message.textContent = 'Procesando venta…';
  try {
    const response = await fetch('/api/ventas', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ proveedor: $('#provider').value, idCliente: Number(form.get('cliente')), idProducto: Number(form.get('producto')), cantidad: Number(form.get('cantidad')) }) });
    const result = await response.json();
    if (!response.ok) throw new Error(result.mensaje || 'No se pudo registrar la venta.');
    message.textContent = `Venta #${result.idVenta} registrada con ${result.proveedor === 'ado' ? 'ADO.NET' : 'EF Core'}.`;
    await loadProducts();
  } catch (error) { message.className = 'message error'; message.textContent = error.message; }
});

(async () => {
  try {
    await loadCategories();
    await loadReview();
    $('#status').textContent = 'Base de datos conectada';
    await loadProducts();
  } catch (error) {
    $('#status').textContent = 'No se pudo conectar'; $('#status').classList.add('error');
    $('#products').innerHTML = `<tr><td colspan="7" class="empty">${escapeHtml(error.message)} Verifica las bases de datos y vuelve a cargar.</td></tr>`;
  }
})();
