const APP_BASE = new URL('.', document.currentScript?.src || window.location.href);
const appPath = (path = '') => new URL(String(path).replace(/^\//, ''), APP_BASE).pathname;
const appUrl = (url = '') => {
  const value = String(url || '').trim();
  if (!value || /^[a-z][a-z0-9+.-]*:/i.test(value) || value.startsWith('//')) return value;
  return appPath(value);
};
const API = appPath('api/v1');
const state = { user: null, authMode: 'demo', people: [], verticals: [], organization: [], platforms: [], portfolios: [], demands: [], portfolioManagers: [], squads: [], squadRoles: [], squadSummary: {}, squadMode: 'board', squadExpanded: new Set(), orgExpanded: new Set(), activePortfolioManager: null, workSummary: {}, view: 'dashboard', canEdit: false };
const PHOTO_OUTPUT_WIDTH = 640;
const PHOTO_OUTPUT_HEIGHT = 800;
const PHOTO_BASE_OVERSCAN = 1.1;
const photoEditor = { source: null, file: null, width: 0, height: 0, minScale: 1, zoom: 1, offsetX: 0, offsetY: 0, dragging: false };

const $ = (selector, root = document) => root.querySelector(selector);
const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];

document.addEventListener('DOMContentLoaded', init);

async function init() {
  bindShellEvents();
  $('#currentDate').textContent = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date());
  try {
    const auth = await api('/auth/me', { allowAnonymous: true });
    state.authMode = auth.authMode;
    if (!auth.authenticated) return showLogin();
    state.user = auth.user;
    state.canEdit = ['super_admin', 'profile_admin', 'governance_editor'].includes(auth.user.role);
    await showApplication();
  } catch (error) {
    showLogin();
    toast(error.message, 'error');
  }
}

function showLogin() {
  $('#loginScreen').classList.remove('hidden');
  $('#appShell').classList.add('hidden');
  if (state.authMode === 'demo') {
    $('#loginButtonText').textContent = 'Enter local development portal';
    $('#loginDescription').textContent = 'Test the complete CET experience on this PC.';
    $('#demoNotice').classList.remove('hidden');
  } else if (state.authMode === 'windows') {
    $('#loginButtonText').textContent = 'Continue with corporate account';
    $('#loginDescription').textContent = 'Continue with your Windows corporate account.';
  }
}

async function showApplication() {
  $('#loginScreen').classList.add('hidden');
  $('#appShell').classList.remove('hidden');
  if (!state.canEdit) $$('.admin-only').forEach((element) => element.classList.add('hidden'));
  $('#userName').textContent = state.user.displayName;
  $('#userRole').textContent = titleCase(state.user.role.replaceAll('_', ' '));
  $('#userAvatar').textContent = initials(state.user.displayName);
  $('#welcomeName').textContent = state.user.displayName.split(' ')[0];
  $('#authModeLabel').textContent = { entra: 'Microsoft Entra ID', windows: 'Windows SSO', demo: 'Local development' }[state.authMode] || 'Secure session';
  await loadReferenceData();
  await loadDashboard();
  $('#loadingState').classList.add('hidden');
  navigate('dashboard');
}

async function loadReferenceData() {
  [state.people, state.verticals, state.organization, state.platforms, state.portfolios, state.demands, state.portfolioManagers, state.squads, state.squadRoles, state.squadSummary, state.workSummary] = await Promise.all([
    api('/people'), api('/units'), api('/organization'), api('/platforms?active=true'), api('/portfolios'), api('/demands'), api('/portfolio-managers'), api('/squads?active=true'), api('/squad-roles'), api('/squads/summary'), api('/work/summary')
  ]);
  populateSelects();
}

async function loadDashboard() {
  const [stats, owners] = await Promise.all([api('/dashboard/stats'), api('/dashboard/ownership')]);
  const cards = [
    ['people', 'People', '◎', '#34e3a4'], ['staff', 'Staff', '●', '#d5ad46'], ['consultants', 'Consultants', '◐', '#59a8ff'],
    ['verticals', 'Units', '⌘', '#aa91ff'], ['platforms', 'Platforms', '▣', '#087a59'], ['portfolios', 'Portfolios', '◇', '#d5ad46'], ['demands', 'Demands', '↗', '#a71930'],
    ['programs', 'Programs', '△', '#d5ad46'], ['squads', 'Squads', '◌', '#59a8ff']
  ];
  $('#statsGrid').innerHTML = cards.map(([key, label, icon, color]) => `
    <article class="stat-card" style="--stat-color:${color}"><span class="stat-icon">${icon}</span><strong>${stats[key]}</strong><span>${label}</span></article>`).join('');
  $('#ownershipGrid').innerHTML = owners.map((owner) => `
    <article class="ownership-card" data-person-id="${owner.id}">
      ${avatarHtml(owner, 'avatar')}
      <div><h4>${esc(owner.display_name)}</h4><p>${esc(owner.designation)}</p></div>
      <div class="ownership-metrics"><span><strong>${owner.direct_reports}</strong>Reports</span><span><strong>${owner.platforms.length}</strong>Platforms</span><span><strong>${owner.programs.length}</strong>Programs</span><span><strong>${owner.squads.length}</strong>Squads</span></div>
    </article>`).join('');
  $$('.ownership-card').forEach((card) => card.addEventListener('click', () => openPortfolioManager(card.dataset.personId)));
  const staffPct = stats.people ? Math.round(stats.staff / stats.people * 100) : 0;
  $('#workforceChart').innerHTML = `<div class="donut" style="background:conic-gradient(var(--accent) 0 ${staffPct}%, var(--blue) ${staffPct}% 100%)"><div class="donut-center"><strong>${stats.people}</strong><small>Total people</small></div></div><div class="chart-legend"><span><i style="background:var(--accent)"></i>Staff ${stats.staff}</span><span><i style="background:var(--blue)"></i>Consultants ${stats.consultants}</span></div>`;
}

function bindShellEvents() {
  $$('.nav-item[data-view]').forEach((button) => button.addEventListener('click', () => navigate(button.dataset.view)));
  $$('[data-go]').forEach((button) => button.addEventListener('click', () => navigate(button.dataset.go)));
  $('#themeToggle').addEventListener('click', () => {
    document.body.classList.toggle('light');
    localStorage.setItem('cet-theme', document.body.classList.contains('light') ? 'light' : 'dark');
  });
  if (localStorage.getItem('cet-theme') === 'light') document.body.classList.add('light');
  $('#menuButton').addEventListener('click', () => $('.sidebar').classList.toggle('open'));
  $('#userMenuButton').addEventListener('click', () => $('#userMenu').classList.toggle('hidden'));
  $('#logoutButton').addEventListener('click', logout);
  $('#peopleSearch').addEventListener('input', renderPeople);
  $('#verticalFilter').addEventListener('change', renderPeople);
  $('#typeFilter').addEventListener('change', renderPeople);
  $('#portfolioSearch').addEventListener('input', renderPortfolios);
  $('#portfolioStatusFilter').addEventListener('change', renderPortfolios);
  $('#portfolioHealthFilter').addEventListener('change', renderPortfolios);
  $('#demandSearch').addEventListener('input', renderDemands);
  $('#demandPortfolioFilter').addEventListener('change', renderDemands);
  $('#demandStatusFilter').addEventListener('change', renderDemands);
  $('#demandHealthFilter').addEventListener('change', renderDemands);
  $('#squadSearch').addEventListener('input', renderSquads);
  $('#squadPlatformFilter').addEventListener('change', renderSquads);
  $('#squadStatusFilter').addEventListener('change', renderSquads);
  $$('[data-squad-mode]').forEach((button) => button.addEventListener('click', () => { state.squadMode = button.dataset.squadMode; renderSquads(); }));
  $('#addSquadButton').addEventListener('click', () => openSquadEditor());
  $('#manageSquadRoles').addEventListener('click', openSquadRoleManager);
  $('#squadForm').addEventListener('submit', saveSquad);
  $('#squadPlatformSelect').addEventListener('change', syncSquadScope);
  $('#addSquadAssignment').addEventListener('click', () => addSquadAssignmentRow());
  $('#archiveSquad').addEventListener('click', archiveSquad);
  $('#closeSquadEditor').addEventListener('click', closeSquadEditor);
  $('#cancelSquadForm').addEventListener('click', closeSquadEditor);
  $('#squadEditorModal').addEventListener('click', (event) => { if (event.target.id === 'squadEditorModal') closeSquadEditor(); });
  $('#squadRoleForm').addEventListener('submit', saveSquadRole);
  $('#newSquadRole').addEventListener('click', resetSquadRoleForm);
  $('#archiveSquadRole').addEventListener('click', archiveSquadRole);
  $('#closeSquadRoles').addEventListener('click', closeSquadRoleManager);
  $('#squadRoleModal').addEventListener('click', (event) => { if (event.target.id === 'squadRoleModal') closeSquadRoleManager(); });
  $('#globalSearch').addEventListener('keydown', (event) => {
    if (event.key === 'Enter') {
      $('#peopleSearch').value = event.target.value;
      navigate('people');
    }
  });
  $('#closeDrawer').addEventListener('click', closeDrawer);
  $('#drawerBackdrop').addEventListener('click', closeDrawer);
  $('#photoInput').addEventListener('change', openPhotoEditor);
  $('#closePhotoEditor').addEventListener('click', closePhotoEditor);
  $('#cancelPhotoCrop').addEventListener('click', closePhotoEditor);
  $('#resetPhotoCrop').addEventListener('click', resetPhotoCrop);
  $('#applyPhotoCrop').addEventListener('click', applyPhotoCrop);
  $('#photoZoom').addEventListener('input', updatePhotoZoom);
  bindPhotoCanvasEvents();
  $('#personForm').addEventListener('submit', savePerson);
  $('#cancelForm').addEventListener('click', () => navigate('people'));
  $('#portfolioForm').addEventListener('submit', savePortfolio);
  $('#cancelPortfolioForm').addEventListener('click', () => navigate('portfolios'));
  $('#demandForm').addEventListener('submit', saveDemand);
  $('#cancelDemandForm').addEventListener('click', () => navigate('demands'));
  $('#backToPortfolioManagers').addEventListener('click', () => navigate('portfolios'));
  $('#platformForm').addEventListener('submit', savePlatform);
  $('#closePlatformEditor').addEventListener('click', closePlatformEditor);
  $('#cancelPlatformForm').addEventListener('click', closePlatformEditor);
  $('#platformOwnerSelect').addEventListener('change', syncPlatformAssignmentControls);
  $('#platformEditorModal').addEventListener('click', (event) => { if (event.target.id === 'platformEditorModal') closePlatformEditor(); });
  $('#unitSearch').addEventListener('input', renderUnits);
  $('#unitStatusFilter').addEventListener('change', renderUnits);
  $('#addUnitButton').addEventListener('click', openNewUnit);
  $('#unitForm').addEventListener('submit', saveUnit);
  $('#cancelUnitForm').addEventListener('click', closeUnitEditor);
  $('#closeUnitEditor').addEventListener('click', closeUnitEditor);
  $('#unitForm').elements.color.addEventListener('input', (event) => { $('#unitColorValue').textContent = event.target.value.toUpperCase(); });
  $('#managerSelect').addEventListener('change', updateHierarchyHeadControl);
  $('#collapseOrg').addEventListener('click', () => { state.orgExpanded.clear(); renderOrganization(); });
  window.addEventListener('resize', debounce(refreshOrganizationLayout, 120));
  if ('ResizeObserver' in window) {
    const scheduleOrganizationFit = debounce(refreshOrganizationLayout, 80);
    state.orgResizeObserver = new ResizeObserver(([entry]) => {
      const observedWidth = Math.round(entry.contentRect.width);
      if (!observedWidth || observedWidth === state.orgObservedWidth) return;
      state.orgObservedWidth = observedWidth;
      scheduleOrganizationFit();
    });
    state.orgResizeObserver.observe($('.org-stage'));
  }
}

function navigate(view) {
  state.view = view;
  const meta = {
    dashboard: ['Executive Home', 'CET / Executive overview'], organization: ['Organization', 'CET / People / Hierarchy'],
    people: ['People Directory', 'CET / People / Directory'], 'add-person': ['Add Profile', 'CET / Administration / Profiles'],
    portfolios: ['Portfolios', 'CET / Ownership / Manager Administration'], 'portfolio-manager': ['Manager Portfolio', 'CET / Ownership / Manager Administration'], 'portfolio-form': ['Portfolio Administration', 'CET / Administration / Portfolios'],
    demands: ['Demands', 'CET / Ownership / Demand Registry'], 'demand-form': ['Demand Administration', 'CET / Administration / Demands'],
    squads: ['Squads', 'CET / Delivery / Squad Operating Model'],
    units: ['Units', 'CET / Administration / Reference Data']
  }[view];
  $$('.view').forEach((element) => element.classList.add('hidden'));
  $(`#${camelView(view)}View`).classList.remove('hidden');
  $$('.nav-item[data-view]').forEach((item) => item.classList.toggle('active', item.dataset.view === view));
  $('#pageTitle').textContent = meta[0]; $('#breadcrumb').textContent = meta[1];
  $('.sidebar').classList.remove('open');
  if (view === 'organization') renderOrganization();
  if (view === 'people') renderPeople();
  if (view === 'add-person') resetPersonForm();
  if (view === 'portfolios') renderPortfolios();
  if (view === 'portfolio-manager') renderPortfolioManager();
  if (view === 'portfolio-form') resetPortfolioForm();
  if (view === 'demands') renderDemands();
  if (view === 'demand-form') resetDemandForm();
  if (view === 'squads') renderSquads();
  if (view === 'units') renderUnits();
  window.scrollTo({ top: 0, behavior: 'smooth' });
}

function camelView(view) {
  return view.replace(/-([a-z])/g, (_, c) => c.toUpperCase());
}

function populateSelects() {
  const verticalOptions = state.verticals.map((v) => `<option value="${v.id}"${Boolean(v.is_active) ? '' : ' disabled'}>${esc(v.name)}${Boolean(v.is_active) ? '' : ' (Inactive)'}</option>`).join('');
  const verticalFilterOptions = state.verticals.map((v) => `<option value="${v.id}">${esc(v.name)}${Boolean(v.is_active) ? '' : ' (Inactive)'}</option>`).join('');
  const peopleOptions = state.people.map((p) => `<option value="${p.id}">${esc(p.display_name)} — ${esc(displayRoleForHierarchy(p))}</option>`).join('');
  const managerOptions = state.people.filter((p) => hierarchyRoleForPerson(p) !== 'Team Member').map((p) => `<option value="${p.id}">${esc(p.display_name)} — ${esc(displayRoleForHierarchy(p))}</option>`).join('');
  const portfolioOptions = state.portfolios.map((p) => `<option value="${p.id}">${esc(p.code)} — ${esc(p.name)}</option>`).join('');
  const activePortfolioOptions = state.portfolios.filter((p) => Boolean(p.is_active)).map((p) => `<option value="${p.id}">${esc(p.code)} — ${esc(p.name)}</option>`).join('');
  $('#verticalFilter').innerHTML = `<option value="">All units</option>${verticalFilterOptions}`;
  $('#verticalSelect').innerHTML = `<option value="">Select unit</option>${verticalOptions}`;
  $('#managerSelect').innerHTML = `<option value="">Department root / no manager</option>${state.people.map((p) => `<option value="${p.id}">${esc(p.display_name)} — ${esc(p.designation)}</option>`).join('')}`;
  $('#portfolioOwnerSelect').innerHTML = `<option value="">Select accountable owner</option>${managerOptions}`;
  $('#portfolioVerticalSelect').innerHTML = `<option value="">Select unit</option>${verticalOptions}`;
  $('#demandPortfolioSelect').innerHTML = `<option value="">Select portfolio</option>${activePortfolioOptions}`;
  $('#demandPortfolioFilter').innerHTML = `<option value="">All portfolios</option>${portfolioOptions}`;
  $('#demandOwnerSelect').innerHTML = `<option value="">Select demand owner</option>${peopleOptions}`;
  $('#demandManagerSelect').innerHTML = `<option value="">Select accountable manager</option>${managerOptions}`;
  const platformOptions = state.platforms.map((platform) => `<option value="${platform.id}">${esc(platform.code)} — ${esc(platform.name)}</option>`).join('');
  $('#squadPlatformFilter').innerHTML = `<option value="">All platforms</option>${platformOptions}`;
  $('#squadPlatformSelect').innerHTML = `<option value="">Select platform</option>${platformOptions}`;
}

function renderPeople() {
  const search = $('#peopleSearch').value.trim().toLowerCase();
  const vertical = $('#verticalFilter').value;
  const type = $('#typeFilter').value;
  const people = state.people.filter((p) => {
    const haystack = `${p.full_name} ${p.employee_id} ${p.email} ${p.designation}`.toLowerCase();
    return (!search || haystack.includes(search)) && (!vertical || String(p.vertical_id) === vertical) && (!type || p.employment_type === type);
  });
  $('#peopleCount').textContent = people.length;
  $('#peopleGrid').innerHTML = people.map((person) => `
    <article class="person-card" data-person-id="${person.id}"><header>${avatarHtml(person, 'avatar')}<div><h3>${esc(person.display_name)}</h3><p>${esc(person.designation)}</p></div></header>
      <div class="person-meta"><div><span>Employee ID</span><strong>${esc(person.employee_id)}</strong></div><div><span>Unit</span><strong>${esc(person.vertical_name || 'Unassigned')}</strong></div><div><span>Reporting to</span><strong>${esc(person.manager_name || 'Department root')}</strong></div><div><span>Type</span><span class="type-badge ${person.employment_type === 'Consultant' ? 'consultant' : ''}">${esc(person.employment_type)}</span></div></div></article>`).join('');
  $('#peopleEmpty').classList.toggle('hidden', people.length > 0);
  $$('.person-card').forEach((card) => card.addEventListener('click', () => openProfile(card.dataset.personId)));
}

function renderPortfolios() {
  const search = $('#portfolioSearch').value.trim().toLowerCase();
  const status = $('#portfolioStatusFilter').value;
  const health = $('#portfolioHealthFilter').value;
  const managers = state.portfolioManagers.filter((manager) => {
    const platformText = manager.platforms.map((platform) => `${platform.code} ${platform.name}`).join(' ');
    const haystack = `${manager.display_name} ${manager.designation} ${manager.vertical_name || ''} ${platformText}`.toLowerCase();
    const matchesStatus = !status || manager.platforms.some((platform) => platform.status === status);
    const matchesHealth = !health || Number(manager.health[String(health).toLowerCase()] || 0) > 0 || manager.portfolios.some((portfolio) => portfolio.health === health);
    return (!search || haystack.includes(search)) && matchesStatus && matchesHealth;
  });
  const summary = state.workSummary;
  const resources = state.portfolioManagers.reduce((total, manager) => total + manager.workforce.total_resources, 0);
  const consultants = state.portfolioManagers.reduce((total, manager) => total + manager.workforce.consultants, 0);
  $('#portfolioHeroCount').textContent = state.portfolioManagers.length;
  $('#portfolioCount').textContent = managers.length;
  $('#portfolioSummary').innerHTML = [
    ['Direct reportees', state.portfolioManagers.length, '◎', 'gold'],
    ['Total platforms', summary.platforms || 0, '▣', 'green'],
    ['Team resources', resources, '●', 'green'],
    ['Consultants', consultants, '◐', 'maroon']
  ].map(workSummaryCard).join('');
  $('#portfolioGrid').innerHTML = managers.map((manager) => `
    <article class="manager-admin-card" data-manager-id="${manager.id}">
      <div class="manager-card-accent"></div>
      <header>${avatarHtml(manager, 'avatar manager-card-photo')}<div><p class="eyebrow">${esc(manager.vertical_name || 'CET')}</p><h3>${esc(manager.display_name)}</h3><span>${esc(manager.designation)}</span></div><button class="manager-open" aria-label="Open ${escAttr(manager.display_name)} portfolio">→</button></header>
      <div class="manager-work-grid"><span><strong>${manager.platform_count}</strong>Platforms</span><span><strong>${manager.demand_count}</strong>Demands</span><span><strong>${manager.program_count}</strong>Programs</span><span><strong>${manager.squad_count}</strong>Squads</span></div>
      <div class="manager-resource-line"><div><small>Team strength</small><strong>${manager.workforce.total_resources} resources</strong></div><div class="composition-bar"><i style="width:${percentage(manager.workforce.staff, manager.workforce.total_resources)}%"></i><b style="width:${percentage(manager.workforce.consultants, manager.workforce.total_resources)}%"></b></div><div class="composition-legend"><span>Staff ${manager.workforce.staff}</span><span>Consultants ${manager.workforce.consultants}</span></div></div>
      <div class="manager-demographics"><span><b>♂</b><strong>${manager.workforce.male}</strong><small>Men</small></span><span><b>♀</b><strong>${manager.workforce.female}</strong><small>Women</small></span><span><b>◎</b><strong>${manager.workforce.direct_reports}</strong><small>Direct reports</small></span><span class="rag-mini"><i class="green">${manager.health.green}</i><i class="amber">${manager.health.amber}</i><i class="red">${manager.health.red}</i><small>Demand health</small></span></div>
      <footer><span>${manager.platforms.length ? esc(manager.platforms.map((platform) => platform.code).join(' · ')) : 'No platform assigned'}</span><strong>View administration →</strong></footer>
    </article>`).join('');
  $('#portfolioEmpty').classList.toggle('hidden', managers.length > 0);
  $$('.manager-admin-card').forEach((card) => card.addEventListener('click', () => openPortfolioManager(card.dataset.managerId)));
}

async function openPortfolioManager(id) {
  try {
    state.activePortfolioManager = await api(`/portfolio-managers/${id}`);
    closeDrawer(); navigate('portfolio-manager');
  } catch (error) { toast(error.message, 'error'); }
}

function renderPortfolioManager() {
  const manager = state.activePortfolioManager;
  if (!manager) return navigate('portfolios');
  const workforce = manager.workforce;
  $('#portfolioManagerContent').innerHTML = `
    <section class="manager-detail-hero">
      <div class="manager-detail-profile">${avatarHtml(manager, 'avatar manager-detail-photo')}<div><p class="eyebrow">${esc(manager.vertical_name || 'CET')}</p><h2>${esc(manager.display_name)}</h2><p>${esc(manager.designation)}</p><span>Manager / Direct Reportee</span></div></div>
      <div class="manager-detail-command"><small>Under administration</small><strong>${manager.platform_count + manager.demand_count + manager.program_count + manager.squad_count}</strong><span>governed records</span></div>
    </section>
    <div class="manager-kpi-grid">
      ${managerKpi('Total resources', workforce.total_resources, '◎', 'green')}
      ${managerKpi('Direct reports', workforce.direct_reports, '↳', 'gold')}
      ${managerKpi('Staff', workforce.staff, '●', 'green')}
      ${managerKpi('Consultants', workforce.consultants, '◐', 'blue')}
      ${managerKpi('Men', workforce.male, '♂', 'maroon')}
      ${managerKpi('Women', workforce.female, '♀', 'gold')}
    </div>
    ${workforce.not_specified ? `<p class="demographic-note">${workforce.not_specified} resource${workforce.not_specified === 1 ? '' : 's'} with gender not specified.</p>` : ''}
    <div class="manager-detail-grid">
      <section class="manager-detail-panel platform-detail-panel"><header><div><p class="eyebrow">Platforms</p><h3>${manager.platform_count} platforms <small>· ${manager.platform_owned_count} owned · ${manager.platform_supported_count} supported</small></h3></div>${state.canEdit ? '<button class="secondary-button" data-manager-add-platform>Add Platform</button>' : ''}</header><div class="manager-record-list">${manager.platforms.length ? manager.platforms.map((platform) => managerPlatformItem(platform)).join('') : emptyManagerRecord('No platforms assigned')}</div></section>
      <section class="manager-detail-panel"><header><div><p class="eyebrow">Demand portfolio</p><h3>${manager.demand_count} demands</h3></div><div class="health-cluster"><span class="green">${manager.health.green}</span><span class="amber">${manager.health.amber}</span><span class="red">${manager.health.red}</span></div></header><div class="manager-demand-list">${manager.demands.length ? manager.demands.map(managerDemandItem).join('') : emptyManagerRecord('No active demands')}</div></section>
      <section class="manager-detail-panel"><header><div><p class="eyebrow">Major programs</p><h3>${manager.program_count} programs</h3></div></header><div class="manager-record-list">${manager.programs.length ? manager.programs.map((record) => managerSimpleItem(record, '△', 'Program')).join('') : emptyManagerRecord('No major programs')}</div></section>
      <section class="manager-detail-panel"><header><div><p class="eyebrow">Delivery squads</p><h3>${manager.squad_count} squads</h3></div></header><div class="manager-record-list">${manager.squads.length ? manager.squads.map((record) => managerSimpleItem(record, '◌', `Lead: ${record.lead_name || 'Unassigned'}`)).join('') : emptyManagerRecord('No squads assigned')}</div></section>
      <section class="manager-detail-panel manager-team-panel"><header><div><p class="eyebrow">Team resources</p><h3>${workforce.total_resources} people</h3></div><div class="team-type-key"><span><i></i>Staff</span><span><i></i>Consultant</span></div></header><div class="manager-team-grid">${manager.team.length ? manager.team.map(managerTeamMember).join('') : emptyManagerRecord('No team resources')}</div></section>
    </div>`;
  $$('[data-portfolio-open]').forEach((button) => button.addEventListener('click', () => openPortfolioDetails(button.dataset.portfolioOpen)));
  $$('[data-demand-open]').forEach((button) => button.addEventListener('click', () => openDemandDetails(button.dataset.demandOpen)));
  $$('[data-platform-edit]').forEach((button) => button.addEventListener('click', () => openPlatformEditor(button.dataset.platformEdit)));
  $('[data-manager-add-platform]')?.addEventListener('click', () => openPlatformEditor());
}

function managerKpi(label, value, icon, tone) {
  return `<article class="manager-kpi tone-${tone}"><span>${icon}</span><div><strong>${value}</strong><small>${label}</small></div></article>`;
}

function managerPlatformItem(platform) {
  const ownedBy = platform.assignments.filter((assignment) => assignment.relationship === 'Owned').map((assignment) => assignment.display_name);
  const supportedBy = platform.assignments.filter((assignment) => assignment.relationship === 'Supported').map((assignment) => assignment.display_name);
  const tag = platform.criticality === 'Critical' ? 'critical' : platform.criticality === 'High' ? 'high' : '';
  const content = `<span class="record-symbol platform-symbol">▦</span><span><strong>${esc(platform.name)}</strong><small>${esc(platform.code)} · ${esc(platform.category || 'Technology Platform')} · ${esc(platform.status)}</small><span class="platform-responsibility">${ownedBy.length ? `<i>Owned · ${esc(ownedBy.join(', '))}</i>` : ''}${supportedBy.length ? `<i>Supported · ${esc(supportedBy.join(', '))}</i>` : ''}</span></span><em class="platform-criticality ${tag}">${esc(platform.criticality)}</em>${state.canEdit ? '<b>→</b>' : ''}`;
  return state.canEdit ? `<button class="manager-record-item platform-record-item" data-platform-edit="${platform.id}">${content}</button>` : `<div class="manager-record-item platform-record-item static">${content}</div>`;
}

function openPlatformEditor(platformId = null) {
  const manager = state.activePortfolioManager;
  if (!manager || !state.canEdit) return;
  const platform = platformId ? manager.platforms.find((candidate) => Number(candidate.id) === Number(platformId)) : null;
  const people = [manager, ...manager.team];
  const form = $('#platformForm'); form.reset();
  $('#platformId').value = platform?.id || ''; $('#platformManagerContextId').value = manager.id;
  $('#platformEditorTitle').textContent = platform ? `Edit ${platform.name}` : 'Add Platform';
  $('#savePlatform').textContent = platform ? 'Update Platform' : 'Save Platform';
  $('#platformOwnerSelect').innerHTML = `<option value="">No single owner</option>${people.map((person) => `<option value="${person.id}">${esc(person.display_name)} — ${esc(person.designation)}</option>`).join('')}`;
  $('#platformSupportOptions').innerHTML = people.map((person) => `<label><input type="checkbox" value="${person.id}"><span>${avatarHtml(person, 'avatar')}<strong>${esc(person.display_name)}</strong><small>${esc(person.designation)}</small></span></label>`).join('');
  if (platform) {
    fillForm(form, platform);
    const owner = platform.assignments.find((assignment) => assignment.relationship === 'Owned');
    $('#platformOwnerSelect').value = owner ? String(owner.person_id) : '';
    const supporterIds = new Set(platform.assignments.filter((assignment) => assignment.relationship === 'Supported').map((assignment) => Number(assignment.person_id)));
    $$('#platformSupportOptions input').forEach((input) => { input.checked = supporterIds.has(Number(input.value)); });
  }
  syncPlatformAssignmentControls(); $('#platformFormErrors').classList.add('hidden');
  $('#platformEditorModal').classList.remove('hidden'); document.body.style.overflow = 'hidden';
}

function syncPlatformAssignmentControls() {
  const ownerId = $('#platformOwnerSelect').value;
  $$('#platformSupportOptions input').forEach((input) => {
    const isOwner = input.value === ownerId;
    if (isOwner) input.checked = false;
    input.disabled = isOwner;
    input.closest('label').classList.toggle('disabled', isOwner);
  });
}

function closePlatformEditor() {
  $('#platformEditorModal').classList.add('hidden'); document.body.style.overflow = '';
}

function managerDemandItem(demand) {
  return `<button class="manager-demand-item" data-demand-open="${demand.id}"><span><strong>${esc(demand.demand_id)}</strong><small>${esc(demand.title)}</small></span><span><em>${esc(demand.priority)}</em>${ragBadge(demand.health)}</span><span class="mini-progress"><i style="width:${Number(demand.progress_percent || 0)}%"></i></span><b>${Number(demand.progress_percent || 0)}%</b></button>`;
}

function managerSimpleItem(record, icon, context) {
  return `<div class="manager-record-item static"><span class="record-symbol">${icon}</span><span><strong>${esc(record.name)}</strong><small>${esc(record.code)} · ${esc(context)} · ${esc(record.status)}</small></span></div>`;
}

function managerTeamMember(person) {
  return `<article class="manager-team-member">${avatarHtml(person, 'avatar')}<div><strong>${esc(person.display_name)}</strong><small>${esc(person.designation)}</small></div><span class="type-badge ${person.employment_type === 'Consultant' ? 'consultant' : ''}">${esc(person.employment_type)}</span><em>${esc(person.gender || 'Not specified')}</em></article>`;
}

function emptyManagerRecord(message) { return `<div class="manager-empty">${esc(message)}</div>`; }

function percentage(value, total) { return total ? Math.round(Number(value) / Number(total) * 100) : 0; }

function renderDemands() {
  const search = $('#demandSearch').value.trim().toLowerCase();
  const portfolioId = $('#demandPortfolioFilter').value;
  const status = $('#demandStatusFilter').value;
  const health = $('#demandHealthFilter').value;
  const demands = state.demands.filter((demand) => {
    const haystack = `${demand.demand_id} ${demand.title} ${demand.category || ''} ${demand.owner_name || ''}`.toLowerCase();
    return (!search || haystack.includes(search)) && (!portfolioId || String(demand.portfolio_id) === portfolioId) &&
      (!status || demand.status === status) && (!health || demand.health === health);
  });
  const summary = state.workSummary;
  $('#demandHeroCount').textContent = summary.demands || 0;
  $('#demandCount').textContent = demands.length;
  $('#demandSummary').innerHTML = [
    ['Active demands', summary.demands || 0, '↗', 'green'],
    ['Critical priority', summary.critical || 0, '!', 'maroon'],
    ['Amber attention', summary.amber || 0, '●', 'amber'],
    ['Red escalation', summary.red || 0, '●', 'red']
  ].map(workSummaryCard).join('');
  $('#demandTable').innerHTML = `
    <div class="demand-row demand-header"><span>Demand</span><span>Portfolio</span><span>Owner</span><span>Stage</span><span>Health</span><span>Progress</span><span></span></div>
    ${demands.map((demand) => `
      <div class="demand-row" data-demand-id="${demand.id}">
        <span class="demand-title"><strong>${esc(demand.demand_id)}</strong><small>${esc(demand.title)}</small><em class="priority-${String(demand.priority).toLowerCase()}">${esc(demand.priority)}</em></span>
        <span><strong>${esc(demand.portfolio_code || '—')}</strong><small>${esc(demand.portfolio_name || 'Unassigned')}</small></span>
        <span><strong>${esc(demand.owner_name || 'Unassigned')}</strong><small>${esc(demand.accountable_manager_name || '')}</small></span>
        <span><strong>${esc(demand.stage)}</strong><small>${esc(demand.status)}</small></span>
        <span>${ragBadge(demand.health)}</span>
        <span class="progress-cell"><strong>${Number(demand.progress_percent || 0)}%</strong><i><b style="width:${Number(demand.progress_percent || 0)}%"></b></i></span>
        <span><button class="row-action demand-view" aria-label="View demand">→</button>${state.canEdit ? '<button class="row-action demand-edit" aria-label="Edit demand">✎</button>' : ''}</span>
      </div>`).join('')}`;
  $('#demandEmpty').classList.toggle('hidden', demands.length > 0);
  $('.demand-table-shell').classList.toggle('hidden', demands.length === 0);
  $$('.demand-row[data-demand-id]').forEach((row) => {
    row.querySelector('.demand-view').addEventListener('click', () => openDemandDetails(row.dataset.demandId));
    row.querySelector('.demand-edit')?.addEventListener('click', () => editDemand(row.dataset.demandId));
  });
}

function renderSquads() {
  const search = $('#squadSearch').value.trim().toLowerCase();
  const platformId = $('#squadPlatformFilter').value;
  const status = $('#squadStatusFilter').value;
  const squads = state.squads.filter((squad) => {
    const haystack = `${squad.code} ${squad.name} ${squad.platform_code || ''} ${squad.platform_name || ''} ${squad.manager_name || ''}`.toLowerCase();
    return (!search || haystack.includes(search)) && (!platformId || String(squad.platform_id) === platformId) && (!status || squad.status === status);
  });
  $('#squadHeroCount').textContent = state.squadSummary.squads || 0;
  $('#squadSummary').innerHTML = [
    ['Active squads', state.squadSummary.squads || 0, '◌', 'green'],
    ['Connected platforms', state.squadSummary.platforms || 0, '▣', 'gold'],
    ['Required headcount', state.squadSummary.required_headcount || 0, '◎', 'maroon'],
    ['Assigned resources', state.squadSummary.assigned_resources || 0, '●', 'green'],
    ['Allocated FTE', state.squadSummary.allocated_fte || 0, '◐', 'gold'],
    ['Capacity gap', state.squadSummary.capacity_gap || 0, '!', 'red']
  ].map(workSummaryCard).join('');
  $$('[data-squad-mode]').forEach((button) => button.classList.toggle('active', button.dataset.squadMode === state.squadMode));
  $('#squadBoard').classList.toggle('hidden', state.squadMode !== 'board' || squads.length === 0);
  $('#squadMatrixPanel').classList.toggle('hidden', state.squadMode !== 'matrix' || squads.length === 0);
  $('#squadEmpty').classList.toggle('hidden', squads.length > 0);
  renderSquadBoard(squads);
  renderSquadMatrix(squads);
}

function renderSquadBoard(squads) {
  const groups = new Map();
  for (const squad of squads) {
    const key = squad.platform_id || 'unassigned';
    if (!groups.has(key)) groups.set(key, { id: key, name: squad.platform_name || 'Unassigned platform', code: squad.platform_code || '—', category: squad.platform_category || 'Delivery', squads: [] });
    groups.get(key).squads.push(squad);
  }
  const body = [...groups.values()].map(squadPlatformSection).join('');
  $('#squadBoard').innerHTML = `<div class="squad-reference-register">
    <div class="squad-reference-heading" aria-hidden="true"><span>Platform</span><div><span>Squad / Track</span><span>Squad Lead</span><span>Status</span><span>Required</span><span>People</span><span>FTE</span><span>Gap</span><span></span></div></div>
    ${body}
  </div>`;
  $$('.squad-expand-button', $('#squadBoard')).forEach((button) => button.addEventListener('click', () => toggleSquadDetails(Number(button.dataset.squadId))));
  $$('.squad-edit-inline', $('#squadBoard')).forEach((button) => button.addEventListener('click', () => openSquadEditor(Number(button.dataset.squadId))));
}

function squadPlatformSection(group) {
  const required = group.squads.reduce((sum, squad) => sum + Number(squad.required_total || 0), 0);
  const allocated = group.squads.reduce((sum, squad) => sum + Number(squad.allocated_fte || 0), 0);
  const resources = new Set(group.squads.flatMap((squad) => squad.assignments.map((assignment) => Number(assignment.person_id)))).size;
  const gap = Number(Math.max(0, required - allocated).toFixed(2));
  return `<section class="squad-platform-section">
    <aside class="squad-platform-rail" aria-label="${escAttr(group.name)} summary">
      <span class="squad-platform-vertical">${esc(group.name)}</span>
      <strong>${esc(group.code)}</strong>
      <small>${esc(group.category)}</small>
      <dl><div><dt>Squads</dt><dd>${group.squads.length}</dd></div><div><dt>People</dt><dd>${resources}</dd></div><div class="${gap > 0 ? 'has-gap' : ''}"><dt>Gap</dt><dd>${capacityValue(gap)}</dd></div></dl>
    </aside>
    <div class="squad-platform-rows">${group.squads.map(squadRegisterRow).join('')}</div>
  </section>`;
}

function squadRegisterRow(squad) {
  const gap = Number(squad.capacity_gap || 0);
  const statusClass = `status-${String(squad.status).toLowerCase().replaceAll(' ', '-')}`;
  const expanded = state.squadExpanded.has(Number(squad.id));
  return `<article class="squad-reference-item ${expanded ? 'expanded' : ''}" data-squad-id="${squad.id}">
    <div class="squad-reference-row ${gap > 0 ? 'has-gap' : 'covered'}">
      <div class="squad-name-cell"><span class="squad-row-accent"></span><div><strong>${esc(squad.name)}</strong><small>${esc(squad.code)} · ${esc(squad.track_type)}</small></div></div>
      <div class="squad-lead-cell"><span>${squad.lead_name ? initials(squad.lead_name) : '—'}</span><div><strong>${esc(squad.lead_name || 'Unassigned')}</strong><small>${esc(squad.manager_name || 'No accountable manager')}</small></div></div>
      <span class="squad-status ${statusClass}">${esc(squad.status)}</span>
      ${squadMetric(squad.required_total, 'required')}
      ${squadMetric(squad.assigned_resources, 'people')}
      ${squadMetric(squad.allocated_fte, 'FTE')}
      <span class="squad-metric squad-gap-metric ${gap > 0 ? 'open' : 'covered'}"><strong>${capacityValue(gap)}</strong><small>${gap > 0 ? 'open' : 'covered'}</small></span>
      <button class="squad-expand-button" data-squad-id="${squad.id}" type="button" aria-expanded="${expanded}" aria-controls="squadDetails${squad.id}" aria-label="${expanded ? 'Collapse' : 'Expand'} ${escAttr(squad.name)} details"><span>⌄</span></button>
    </div>
    <div id="squadDetails${squad.id}" class="squad-expanded-detail" ${expanded ? '' : 'hidden'}>${expanded ? squadExpandedDetails(squad) : ''}</div>
  </article>`;
}

function squadMetric(value, label) {
  return `<span class="squad-metric"><strong>${capacityValue(value)}</strong><small>${esc(label)}</small></span>`;
}

function squadExpandedDetails(squad) {
  const roleAssignments = new Map();
  squad.assignments.forEach((assignment) => {
    const key = Number(assignment.role_id);
    if (!roleAssignments.has(key)) roleAssignments.set(key, []);
    roleAssignments.get(key).push(assignment);
  });
  const plannedRoles = squad.staffing_plan.filter((row) => Number(row.required_count) > 0);
  const roleRows = plannedRoles.length ? plannedRoles.map((role) => {
    const assigned = roleAssignments.get(Number(role.role_id)) || [];
    const allocated = assigned.reduce((sum, assignment) => sum + Number(assignment.allocation_percent || 0), 0) / 100;
    return `<div class="squad-role-detail"><b>${esc(role.role_code)}</b><span><strong>${esc(role.role_name)}</strong><small>${assigned.length} named · ${capacityValue(allocated)} FTE</small></span><em>${Number(role.required_count)} required</em></div>`;
  }).join('') : '<p class="squad-detail-empty">No required role plan has been configured.</p>';
  const assignmentRows = squad.assignments.length ? squad.assignments.map((assignment) => {
    const dateRange = assignment.start_date || assignment.end_date ? `${formatDate(assignment.start_date)} – ${formatDate(assignment.end_date)}` : 'No assignment dates';
    return `<div class="squad-resource-detail">${avatarHtml(assignment, 'squad-resource-avatar')}<span><strong>${esc(assignment.display_name)}</strong><small>${esc(assignment.role_name)} · ${esc(assignment.employment_type || 'Type not set')}</small></span><em>${Number(assignment.allocation_percent || 0)}%</em><i>${assignment.is_primary ? 'Primary' : 'Shared'} · ${esc(dateRange)}</i></div>`;
  }).join('') : '<p class="squad-detail-empty">No named resources have been assigned.</p>';
  return `<div class="squad-detail-intro"><div><p class="eyebrow">Squad detail</p><strong>${esc(squad.description || `${squad.name} delivery track`)}</strong></div><span>Accountable manager <b>${esc(squad.manager_name || 'Unassigned')}</b></span></div>
    <div class="squad-detail-columns"><section><header><strong>Required role plan</strong><small>${plannedRoles.length} configured roles</small></header><div class="squad-detail-list">${roleRows}</div></section><section><header><strong>Named resource assignments</strong><small>${squad.assignments.length} assignments</small></header><div class="squad-detail-list">${assignmentRows}</div></section></div>
    ${state.canEdit ? `<footer class="squad-detail-actions"><button class="squad-edit-inline secondary-button" data-squad-id="${squad.id}" type="button">Edit squad, staffing & assignments</button></footer>` : ''}`;
}

function toggleSquadDetails(id) {
  if (state.squadExpanded.has(id)) state.squadExpanded.delete(id); else state.squadExpanded.add(id);
  renderSquads();
  requestAnimationFrame(() => document.querySelector(`.squad-expand-button[data-squad-id="${id}"]`)?.focus());
}

function capacityValue(value) { const number = Number(value || 0); return Number.isInteger(number) ? String(number) : number.toFixed(2); }

function renderSquadMatrix(squads) {
  const roles = state.squadRoles.filter((role) => Boolean(role.is_active));
  $('#squadMatrix').innerHTML = `<thead><tr><th>Platform</th><th>Squad / Track</th>${roles.map((role) => `<th title="${escAttr(role.name)}">${esc(role.code)}</th>`).join('')}<th>Total</th><th>Assigned FTE</th><th>Gap</th></tr></thead><tbody>${squads.map((squad) => {
    const plan = new Map(squad.staffing_plan.map((row) => [Number(row.role_id), Number(row.required_count)]));
    return `<tr data-squad-id="${squad.id}"><td><strong>${esc(squad.platform_code || '—')}</strong><small>${esc(squad.platform_name || 'Unassigned')}</small></td><td><strong>${esc(squad.name)}</strong><small>${esc(squad.track_type)}</small></td>${roles.map((role) => `<td><span class="matrix-count ${plan.get(Number(role.id)) ? 'filled' : ''}">${plan.get(Number(role.id)) || 0}</span></td>`).join('')}<td><b>${squad.required_total}</b></td><td>${squad.allocated_fte}</td><td><b class="${Number(squad.capacity_gap) > 0 ? 'matrix-gap' : 'matrix-covered'}">${squad.capacity_gap}</b></td></tr>`;
  }).join('')}</tbody>`;
  $$('#squadMatrix tbody tr').forEach((row) => row.addEventListener('click', () => openSquadEditor(Number(row.dataset.squadId))));
}

function openSquadEditor(id = null) {
  if (!state.canEdit) return;
  const squad = id ? state.squads.find((item) => Number(item.id) === Number(id)) : null;
  const form = $('#squadForm'); form.reset(); $('#squadId').value = squad?.id || '';
  $('#squadEditorTitle').textContent = squad ? `Edit ${squad.name}` : 'Add Squad / Track';
  $('#archiveSquad').classList.toggle('hidden', !squad);
  if (squad) fillForm(form, squad);
  $('#squadPlatformSelect').value = squad?.platform_id ? String(squad.platform_id) : '';
  renderSquadStaffingPlan(squad?.staffing_plan || []);
  syncSquadScope(squad?.assignments || [], squad?.manager_person_id, squad?.lead_person_id);
  $('#squadFormErrors').classList.add('hidden'); $('#squadEditorModal').classList.remove('hidden'); document.body.style.overflow = 'hidden';
}

function renderSquadStaffingPlan(plan = []) {
  const values = new Map(plan.map((row) => [Number(row.role_id), Number(row.required_count)]));
  $('#squadStaffingPlan').innerHTML = state.squadRoles.filter((role) => Boolean(role.is_active) || values.has(Number(role.id))).map((role) => `<label><span><b>${esc(role.code)}</b><strong>${esc(role.name)}</strong><small>${esc(role.category)}</small></span><input type="number" min="0" max="999" value="${values.get(Number(role.id)) || 0}" data-staffing-role="${role.id}" aria-label="Required ${escAttr(role.name)}"></label>`).join('');
}

function syncSquadScope(assignments, selectedManagerId, selectedLeadId) {
  const platform = state.platforms.find((item) => String(item.id) === $('#squadPlatformSelect').value);
  const managerId = Number(selectedManagerId || platform?.manager_person_id || 0);
  const manager = state.people.find((person) => Number(person.id) === managerId);
  $('#squadManagerSelect').innerHTML = manager ? `<option value="${manager.id}">${esc(manager.display_name)} — ${esc(manager.designation)}</option>` : '<option value="">Select a platform first</option>';
  const people = scopedSquadPeople(managerId);
  $('#squadLeadSelect').innerHTML = `<option value="">No named lead</option>${people.map((person) => `<option value="${person.id}">${esc(person.display_name)} — ${esc(person.designation)}</option>`).join('')}`;
  if (selectedLeadId) $('#squadLeadSelect').value = String(selectedLeadId);
  const rows = Array.isArray(assignments) ? assignments : collectSquadAssignmentRows();
  $('#squadAssignmentRows').innerHTML = '';
  rows.forEach((assignment) => addSquadAssignmentRow(assignment, people));
  if (!rows.length) addSquadAssignmentRow({}, people);
}

function scopedSquadPeople(managerId) {
  const allowed = new Set([Number(managerId)]); const queue = [Number(managerId)];
  while (queue.length) {
    const parentId = queue.shift();
    state.people.filter((person) => Number(person.manager_id) === parentId).forEach((person) => { if (!allowed.has(Number(person.id))) { allowed.add(Number(person.id)); queue.push(Number(person.id)); } });
  }
  return state.people.filter((person) => allowed.has(Number(person.id)) && Boolean(person.is_active));
}

function addSquadAssignmentRow(assignment = {}, people = null) {
  const managerId = Number($('#squadManagerSelect').value || 0); const scopedPeople = people || scopedSquadPeople(managerId);
  const roles = state.squadRoles.filter((role) => Boolean(role.is_active) || Number(role.id) === Number(assignment.role_id));
  const row = document.createElement('div'); row.className = 'squad-assignment-row';
  row.innerHTML = `<select data-assignment="person_id" aria-label="Assigned resource"><option value="">Select resource</option>${scopedPeople.map((person) => `<option value="${person.id}">${esc(person.display_name)} · ${esc(person.employment_type)}</option>`).join('')}</select><select data-assignment="role_id" aria-label="Squad role"><option value="">Select role</option>${roles.map((role) => `<option value="${role.id}">${esc(role.code)} — ${esc(role.name)}</option>`).join('')}</select><label class="allocation-input"><input data-assignment="allocation_percent" type="number" min="1" max="100" value="${Number(assignment.allocation_percent || 100)}"><span>%</span></label><label class="primary-check"><input data-assignment="is_primary" type="checkbox" ${assignment.is_primary === false || Number(assignment.is_primary) === 0 ? '' : 'checked'}><span>Primary</span></label><span class="assignment-dates"><input data-assignment="start_date" type="date" value="${dateInputValue(assignment.start_date)}" aria-label="Assignment start"><input data-assignment="end_date" type="date" value="${dateInputValue(assignment.end_date)}" aria-label="Assignment end"></span><button class="assignment-remove" type="button" aria-label="Remove assignment">×</button>`;
  row.querySelector('[data-assignment="person_id"]').value = assignment.person_id ? String(assignment.person_id) : '';
  row.querySelector('[data-assignment="role_id"]').value = assignment.role_id ? String(assignment.role_id) : '';
  row.querySelector('.assignment-remove').addEventListener('click', () => row.remove()); $('#squadAssignmentRows').appendChild(row);
}

function collectSquadAssignmentRows() {
  return $$('.squad-assignment-row').map((row) => ({
    person_id: row.querySelector('[data-assignment="person_id"]').value,
    role_id: row.querySelector('[data-assignment="role_id"]').value,
    allocation_percent: row.querySelector('[data-assignment="allocation_percent"]').value,
    is_primary: row.querySelector('[data-assignment="is_primary"]').checked,
    start_date: row.querySelector('[data-assignment="start_date"]').value,
    end_date: row.querySelector('[data-assignment="end_date"]').value
  })).filter((row) => row.person_id || row.role_id);
}

function closeSquadEditor() { $('#squadEditorModal').classList.add('hidden'); document.body.style.overflow = ''; }

async function saveSquad(event) {
  event.preventDefault(); const form = event.currentTarget; const submit = form.querySelector('[type="submit"]'); const id = $('#squadId').value;
  const payload = Object.fromEntries(new FormData(form).entries());
  payload.lead_person_id = payload.lead_person_id || null;
  payload.vertical_id = state.platforms.find((platform) => Number(platform.id) === Number(payload.platform_id))?.vertical_id || null;
  payload.staffing_plan = $$('[data-staffing-role]').map((input) => ({ role_id: Number(input.dataset.staffingRole), required_count: Number(input.value || 0) }));
  payload.assignments = collectSquadAssignmentRows().map((row) => ({ ...row, person_id: Number(row.person_id), role_id: Number(row.role_id), allocation_percent: Number(row.allocation_percent) }));
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#squadFormErrors').classList.add('hidden');
  try {
    await api(id ? `/squads/${id}` : '/squads', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    await refreshSquadData(); closeSquadEditor(); renderSquads(); toast(id ? 'Squad updated successfully.' : 'Squad added successfully.', 'success');
  } catch (error) { showFormError('#squadFormErrors', error); toast(error.message, 'error'); }
  finally { submit.disabled = false; submit.textContent = 'Save Squad'; }
}

async function archiveSquad() {
  const id = $('#squadId').value; if (!id || !window.confirm('Archive this squad? Historical records remain available for audit.')) return;
  try { await api(`/squads/${id}`, { method: 'DELETE' }); await refreshSquadData(); closeSquadEditor(); renderSquads(); toast('Squad archived.', 'success'); }
  catch (error) { showFormError('#squadFormErrors', error); toast(error.message, 'error'); }
}

async function refreshSquadData() {
  [state.squads, state.squadRoles, state.squadSummary, state.portfolioManagers, state.workSummary] = await Promise.all([api('/squads?active=true'), api('/squad-roles'), api('/squads/summary'), api('/portfolio-managers'), api('/work/summary')]);
}

function openSquadRoleManager() { renderSquadRoleList(); resetSquadRoleForm(); $('#squadRoleModal').classList.remove('hidden'); document.body.style.overflow = 'hidden'; }
function closeSquadRoleManager() { $('#squadRoleModal').classList.add('hidden'); document.body.style.overflow = ''; }
function renderSquadRoleList() {
  $('#squadRoleList').innerHTML = state.squadRoles.map((role) => `<button type="button" data-role-id="${role.id}" class="${Boolean(role.is_active) ? '' : 'inactive'}"><span><b>${esc(role.code)}</b><strong>${esc(role.name)}</strong><small>${esc(role.category)}</small></span><em>${Boolean(role.is_active) ? 'Active' : 'Inactive'}</em></button>`).join('');
  $$('#squadRoleList [data-role-id]').forEach((button) => button.addEventListener('click', () => editSquadRole(Number(button.dataset.roleId))));
}
function resetSquadRoleForm() { const form = $('#squadRoleForm'); form.reset(); $('#squadRoleId').value = ''; form.elements.display_order.value = 90; form.elements.is_active.checked = true; $('#squadRoleFormTitle').textContent = 'Add role'; $('#archiveSquadRole').classList.add('hidden'); $('#squadRoleErrors').classList.add('hidden'); }
function editSquadRole(id) { const role = state.squadRoles.find((item) => Number(item.id) === Number(id)); if (!role) return; fillForm($('#squadRoleForm'), role); $('#squadRoleId').value = role.id; $('#squadRoleForm').elements.is_active.checked = Boolean(role.is_active); $('#squadRoleFormTitle').textContent = `Edit ${role.name}`; $('#archiveSquadRole').classList.toggle('hidden', !Boolean(role.is_active)); }
async function saveSquadRole(event) {
  event.preventDefault(); const form = event.currentTarget; const id = $('#squadRoleId').value; const payload = Object.fromEntries(new FormData(form).entries()); payload.is_active = form.elements.is_active.checked;
  try { await api(id ? `/squad-roles/${id}` : '/squad-roles', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } }); state.squadRoles = await api('/squad-roles'); renderSquadRoleList(); resetSquadRoleForm(); toast(id ? 'Squad role updated.' : 'Squad role added.', 'success'); }
  catch (error) { showFormError('#squadRoleErrors', error); toast(error.message, 'error'); }
}
async function archiveSquadRole() {
  const id = $('#squadRoleId').value; if (!id || !window.confirm('Archive this role? Existing staffing records will retain it.')) return;
  try { await api(`/squad-roles/${id}`, { method: 'DELETE' }); state.squadRoles = await api('/squad-roles'); renderSquadRoleList(); resetSquadRoleForm(); toast('Squad role archived.', 'success'); }
  catch (error) { showFormError('#squadRoleErrors', error); toast(error.message, 'error'); }
}

function workSummaryCard([label, value, icon, tone]) {
  return `<article class="work-summary-card tone-${tone}"><span>${icon}</span><div><strong>${value}</strong><small>${label}</small></div></article>`;
}

function ragBadge(health = 'Green') {
  return `<span class="rag-badge rag-${String(health).toLowerCase()}"><i></i>${esc(health)}</span>`;
}

async function openPortfolioDetails(id) {
  try {
    const portfolio = await api(`/portfolios/${id}`);
    $('#drawerContent').innerHTML = `
      <div class="drawer-hero work-drawer-hero"><span class="drawer-record-icon">◇</span><p class="eyebrow">${esc(portfolio.code)}</p><h2>${esc(portfolio.name)}</h2><p>${esc(portfolio.business_area || portfolio.vertical_name || 'CET Portfolio')}</p><div class="drawer-tags">${ragBadge(portfolio.health)}<span class="type-badge">${esc(portfolio.status)}</span></div></div>
      <section class="drawer-section"><h3>Strategic purpose</h3><p>${esc(portfolio.description || 'No description provided.')}</p><p>${esc(portfolio.strategic_objectives || 'No strategic objectives provided.')}</p></section>
      <section class="drawer-section"><h3>Governance</h3><div class="drawer-details"><div class="drawer-detail"><small>Owner</small><strong>${esc(portfolio.owner_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Unit</small><strong>${esc(portfolio.vertical_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Start</small><strong>${formatDate(portfolio.start_date)}</strong></div><div class="drawer-detail"><small>Target</small><strong>${formatDate(portfolio.target_end_date)}</strong></div></div></section>
      <section class="drawer-section"><h3>Connected demands</h3><div class="ownership-list">${portfolio.demands.length ? portfolio.demands.map((d) => `<div><span>${esc(d.demand_id)} · ${esc(d.title)}</span><small>${esc(d.status)} · ${esc(d.health)}</small></div>`).join('') : '<p>No active demands are connected.</p>'}</div></section>
      ${state.canEdit ? '<div class="drawer-actions"><button class="primary-button maroon-button" id="editPortfolioButton">Edit portfolio</button></div>' : ''}`;
    if (state.canEdit) $('#editPortfolioButton').addEventListener('click', () => editPortfolio(portfolio.id));
    openDrawer();
  } catch (error) { toast(error.message, 'error'); }
}

async function openDemandDetails(id) {
  try {
    const demand = await api(`/demands/${id}`);
    $('#drawerContent').innerHTML = `
      <div class="drawer-hero work-drawer-hero"><span class="drawer-record-icon">↗</span><p class="eyebrow">${esc(demand.demand_id)}</p><h2>${esc(demand.title)}</h2><p>${esc(demand.classification)} · ${esc(demand.priority)} priority</p><div class="drawer-tags">${ragBadge(demand.health)}<span class="type-badge">${esc(demand.status)}</span></div></div>
      <section class="drawer-section"><h3>Demand purpose</h3><p>${esc(demand.description || 'No description provided.')}</p></section>
      <section class="drawer-section"><h3>Accountability</h3><div class="drawer-details"><div class="drawer-detail"><small>Portfolio</small><strong>${esc(demand.portfolio_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Demand owner</small><strong>${esc(demand.owner_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Accountable manager</small><strong>${esc(demand.accountable_manager_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Stage</small><strong>${esc(demand.stage)}</strong></div></div></section>
      <section class="drawer-section"><h3>Delivery</h3><div class="drawer-details"><div class="drawer-detail"><small>Progress</small><strong>${Number(demand.progress_percent || 0)}%</strong></div><div class="drawer-detail"><small>Planned start</small><strong>${formatDate(demand.planned_start_date)}</strong></div><div class="drawer-detail"><small>Planned end</small><strong>${formatDate(demand.planned_end_date)}</strong></div><div class="drawer-detail"><small>Requesting area</small><strong>${esc(demand.requesting_department || '—')}</strong></div></div></section>
      ${state.canEdit ? '<div class="drawer-actions"><button class="primary-button maroon-button" id="editDemandButton">Edit demand</button></div>' : ''}`;
    if (state.canEdit) $('#editDemandButton').addEventListener('click', () => editDemand(demand.id));
    openDrawer();
  } catch (error) { toast(error.message, 'error'); }
}

function openDrawer() {
  $('#profileDrawer').classList.add('open'); $('#profileDrawer').setAttribute('aria-hidden', 'false'); $('#drawerBackdrop').classList.remove('hidden');
}

function renderOrganization() {
  const stage = $('.org-stage');
  const scrollPosition = { left: stage.scrollLeft, top: stage.scrollTop };
  const host = $('#orgNodes');
  host.innerHTML = '';
  for (const root of state.organization) host.appendChild(buildOrganizationTree(root));
  requestAnimationFrame(() => requestAnimationFrame(() => {
    refreshOrganizationLayout();
    stage.scrollTo(scrollPosition);
  }));
}

function buildOrganizationTree(root) {
  const rootBranch = buildBranch(root, 0);
  rootBranch.classList.add('org-root-branch');
  const managers = root.children || [];
  if (!managers.length) return rootBranch;

  const managerRow = document.createElement('div');
  managerRow.className = 'org-children org-manager-row';
  managers.forEach((manager) => {
    const managerBranch = buildBranch(manager, 1);
    if (state.orgExpanded.has(Number(manager.id))) managerBranch.classList.add('is-selected');
    managerRow.appendChild(managerBranch);
  });
  rootBranch.appendChild(managerRow);

  const expandedManager = managers.find((manager) => state.orgExpanded.has(Number(manager.id)));
  if (!expandedManager?.children?.length) return rootBranch;
  appendOrganizationLevels(rootBranch, expandedManager, 2, [root.display_name, expandedManager.display_name]);
  return rootBranch;
}

const ORGANIZATION_CONNECTOR_LIMIT = 5;

function appendOrganizationLevels(container, parent, childDepth, trail) {
  if (!parent?.children?.length) return;
  const levelPanel = document.createElement('section');
  levelPanel.className = `org-team-panel org-hierarchy-level${childDepth > 2 ? ' is-nested' : ''}`;
  levelPanel.dataset.orgLevel = childDepth;
  const breadcrumb = trail.map((name) => `<span>${esc(name)}</span>`).join('<b aria-hidden="true">›</b>');
  levelPanel.innerHTML = `
    <header class="org-team-heading">
      <div><p class="eyebrow">Hierarchy level ${childDepth}</p><h3>${esc(parent.display_name)}'s direct reportees</h3><nav class="org-level-trail" aria-label="Reporting path">${breadcrumb}</nav></div>
      <span><strong>${parent.children.length}</strong> direct resource${parent.children.length === 1 ? '' : 's'} · fixed parent row${parent.children.length > ORGANIZATION_CONNECTOR_LIMIT ? ` · connectors: first ${ORGANIZATION_CONNECTOR_LIMIT}` : ''}</span>
    </header>`;

  const resourceMatrix = document.createElement('div');
  resourceMatrix.className = 'org-children org-reportee-grid org-resource-matrix';
  const expandedChild = parent.children.find((child) => state.orgExpanded.has(Number(child.id)));
  parent.children.forEach((resource, index) => {
    const resourceBranch = buildBranch(resource, childDepth);
    resourceBranch.dataset.connectorVisible = index < ORGANIZATION_CONNECTOR_LIMIT ? 'true' : 'false';
    if (expandedChild && Number(resource.id) === Number(expandedChild.id)) resourceBranch.classList.add('is-selected');
    resourceMatrix.appendChild(resourceBranch);
  });
  levelPanel.appendChild(resourceMatrix);
  container.appendChild(levelPanel);

  if (expandedChild?.children?.length) {
    appendOrganizationLevels(container, expandedChild, childDepth + 1, [...trail, expandedChild.display_name]);
  }
}

function buildBranch(person, depth) {
  const branch = document.createElement('div');
  branch.className = 'org-branch';
  const card = document.createElement('article');
  const levelClass = depth === 0 ? 'head' : depth === 1 ? 'manager' : 'member';
  card.className = `org-card ${levelClass}`;
  card.dataset.personId = person.id;
  card.dataset.managerId = person.manager_id || '';
  card.dataset.hierarchyRole = hierarchyRoleForPerson(person);
  const hasChildren = person.children.length > 0;
  const expanded = depth === 0 || state.orgExpanded.has(Number(person.id));
  const reportLabel = `${person.children.length} report${person.children.length === 1 ? '' : 's'}`;
  const expandControl = hasChildren && depth > 0
    ? `<button class="org-expand" type="button" aria-expanded="${expanded}" aria-label="${expanded ? 'Collapse' : 'Expand'} ${escAttr(person.display_name)} reportees"><span>${reportLabel}</span><b aria-hidden="true">${expanded ? '⌄' : '›'}</b></button>`
    : `<span class="org-report-count">${reportLabel}</span>`;
  card.innerHTML = `${avatarHtml(person, 'avatar')}<div class="org-card-copy"><h4>${esc(person.display_name)}</h4><p>${esc(displayRoleForHierarchy(person))}</p></div><span class="node-status"></span><footer><span>${esc(person.employment_type)}</span>${expandControl}</footer>`;
  card.addEventListener('click', () => openProfile(person.id));
  $('.org-expand', card)?.addEventListener('click', (event) => {
    event.stopPropagation();
    toggleOrganizationBranch(person);
  });
  branch.appendChild(card);
  return branch;
}

function toggleOrganizationBranch(person) {
  const id = Number(person.id);
  if (state.orgExpanded.has(id)) {
    clearOrganizationExpansion(person);
  } else {
    organizationPeople()
      .filter((candidate) => Number(candidate.manager_id) === Number(person.manager_id))
      .forEach(clearOrganizationExpansion);
    state.orgExpanded.add(id);
  }
  renderOrganization();
}

function organizationPeople() {
  const people = [];
  const visit = (person) => {
    people.push(person);
    (person.children || []).forEach(visit);
  };
  state.organization.forEach(visit);
  return people;
}

function clearOrganizationExpansion(person) {
  state.orgExpanded.delete(Number(person.id));
  (person.children || []).forEach(clearOrganizationExpansion);
}

function drawFiberLines() {
  if (state.view !== 'organization') return;
  const canvas = $('#orgCanvas'); const svg = $('#fiberLines');
  const width = canvas.scrollWidth; const height = canvas.scrollHeight;
  svg.setAttribute('viewBox', `0 0 ${width} ${height}`);
  svg.setAttribute('width', width); svg.setAttribute('height', height);
  svg.innerHTML = '';
  $$('.org-card[data-manager-id]:not([data-manager-id=""])', canvas).forEach((child) => {
    const branch = child.closest('.org-branch');
    if (branch?.dataset.connectorVisible === 'false') return;
    const parent = $(`.org-card[data-person-id="${child.dataset.managerId}"]`, canvas);
    if (!parent) return;
    const a = offsetWithin(parent, canvas); const b = offsetWithin(child, canvas);
    const x1 = a.x + parent.offsetWidth / 2;
    const y1 = a.y + parent.offsetHeight;
    const x2 = b.x + child.offsetWidth / 2;
    const y2 = b.y;
    const mid = y1 + (y2 - y1) * .52;
    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', `M ${x1} ${y1} C ${x1} ${mid}, ${x2} ${mid}, ${x2} ${y2}`);
    path.setAttribute('class', `fiber-line ${parent.classList.contains('head') ? 'head' : ''}`);
    svg.appendChild(path);
  });
}

function offsetWithin(element, ancestor) {
  let x = 0; let y = 0; let current = element;
  while (current && current !== ancestor) {
    x += current.offsetLeft; y += current.offsetTop;
    current = current.offsetParent;
  }
  return { x, y };
}

function refreshOrganizationLayout() {
  if (state.view !== 'organization') return;
  requestAnimationFrame(drawFiberLines);
}

function hierarchyRoleForPerson(person) {
  if (!person.manager_id) return 'Department / Unit Head';
  const manager = state.people.find((candidate) => Number(candidate.id) === Number(person.manager_id));
  return manager && !manager.manager_id ? 'Manager / Direct Reportee' : 'Team Member';
}

function displayRoleForHierarchy(person) {
  const designation = String(person.designation || '').trim();
  if (hierarchyRoleForPerson(person) !== 'Manager / Direct Reportee') return designation;
  if (/^vertical head\b/i.test(designation)) {
    return designation.replace(/^vertical head\s*[–—-]?\s*/i, 'Manager – ');
  }
  if (/^unit head\b/i.test(designation)) {
    return designation.replace(/^unit head\s*[–—-]?\s*/i, 'Manager – ');
  }
  return designation || 'Manager / Direct Reportee';
}

async function openProfile(id) {
  try {
    const person = await api(`/people/${id}`);
    const isPortfolioManager = hierarchyRoleForPerson(person) === 'Manager / Direct Reportee';
    const lists = [...person.portfolios.map((x) => ['Portfolio', x.name]), ...person.programs.map((x) => ['Program', x.name]), ...person.squads.map((x) => ['Squad', `${x.name} · ${x.role || 'Member'}`])];
    $('#drawerContent').innerHTML = `
      <div class="drawer-hero">${avatarHtml(person, 'avatar')}<h2>${esc(person.display_name)}</h2><p>${esc(displayRoleForHierarchy(person))}</p><div class="drawer-tags"><span class="type-badge ${person.employment_type === 'Consultant' ? 'consultant' : ''}">${esc(person.employment_type)}</span><span class="type-badge hierarchy-badge">${esc(hierarchyRoleForPerson(person))}</span></div></div>
      <section class="drawer-section"><h3>Profile</h3><p>${esc(person.profile_summary || 'No profile summary has been added yet.')}</p></section>
      <section class="drawer-section"><h3>Organization</h3><div class="drawer-details"><div class="drawer-detail"><small>Employee ID</small><strong>${esc(person.employee_id)}</strong></div><div class="drawer-detail"><small>Unit</small><strong>${esc(person.vertical_name || 'Unassigned')}</strong></div><div class="drawer-detail"><small>Reporting to</small><strong>${esc(person.manager_name || 'Department root')}</strong></div><div class="drawer-detail"><small>Location</small><strong>${esc(person.location || '—')}</strong></div><div class="drawer-detail"><small>Email</small><strong>${esc(person.email)}</strong></div><div class="drawer-detail"><small>Employer</small><strong>${esc(person.employer || '—')}</strong></div></div></section>
      <section class="drawer-section"><h3>Ownership & squads</h3><div class="ownership-list">${lists.length ? lists.map(([type, name]) => `<div><span>${esc(name)}</span><small>${type}</small></div>`).join('') : '<p>No ownership assignments linked yet.</p>'}</div></section>
      <section class="drawer-section"><h3>Skills</h3><p>${esc(person.skills || 'Not provided')}</p></section>
      ${(state.canEdit || isPortfolioManager) ? `<div class="drawer-actions">${isPortfolioManager ? '<button class="secondary-button" id="viewAdministrationButton">View administration</button>' : ''}${state.canEdit ? '<button class="primary-button" id="editProfileButton">Edit profile</button>' : ''}</div>` : ''}`;
    if (state.canEdit) $('#editProfileButton').addEventListener('click', () => editPerson(person));
    if (isPortfolioManager) $('#viewAdministrationButton').addEventListener('click', () => openPortfolioManager(person.id));
    $('#profileDrawer').classList.add('open'); $('#profileDrawer').setAttribute('aria-hidden', 'false'); $('#drawerBackdrop').classList.remove('hidden');
  } catch (error) { toast(error.message, 'error'); }
}

function closeDrawer() {
  $('#profileDrawer').classList.remove('open'); $('#profileDrawer').setAttribute('aria-hidden', 'true'); $('#drawerBackdrop').classList.add('hidden');
}

function resetPersonForm() {
  const form = $('#personForm'); form.reset(); $('#personId').value = ''; $('#photoUrl').value = '';
  $$('#managerSelect option').forEach((option) => option.disabled = false);
  $('#formTitle').textContent = 'Add staff or consultant'; $('#savePerson').textContent = 'Save profile';
  $('#photoPreview').style.backgroundImage = ''; $('#photoPreview').textContent = '+'; $('#formErrors').classList.add('hidden');
  updateHierarchyHeadControl();
}

function renderUnits() {
  const search = $('#unitSearch').value.trim().toLowerCase();
  const status = $('#unitStatusFilter').value;
  const units = state.verticals.filter((unit) => {
    const haystack = `${unit.name} ${unit.code} ${unit.description || ''}`.toLowerCase();
    const active = Boolean(unit.is_active);
    return (!search || haystack.includes(search)) && (!status || (status === 'active' ? active : !active));
  });
  $('#unitCount').textContent = state.verticals.length;
  $('#unitHeroCount').textContent = state.verticals.filter((unit) => Boolean(unit.is_active)).length;
  $('#unitGrid').innerHTML = units.map((unit) => {
    const peopleCount = state.people.filter((person) => Number(person.vertical_id) === Number(unit.id)).length;
    const portfolioCount = state.portfolios.filter((portfolio) => Number(portfolio.vertical_id) === Number(unit.id)).length;
    return `<article class="unit-card ${Boolean(unit.is_active) ? '' : 'inactive'}">
      <div class="unit-accent" style="--unit-color:${escAttr(unit.color || '#087a59')}"><span>${esc(unit.code)}</span></div>
      <div class="unit-card-copy"><header><div><h3>${esc(unit.name)}</h3><span class="unit-status ${Boolean(unit.is_active) ? 'active' : ''}">${Boolean(unit.is_active) ? 'Active' : 'Inactive'}</span></div>${state.canEdit ? `<button class="unit-edit-button" data-unit-id="${unit.id}" type="button">Edit</button>` : ''}</header>
      <p>${esc(unit.description || 'No description has been added.')}</p>
      <footer><span><strong>${peopleCount}</strong> People</span><span><strong>${portfolioCount}</strong> Portfolios</span></footer></div>
    </article>`;
  }).join('');
  $('#unitEmpty').classList.toggle('hidden', units.length > 0);
  $$('.unit-edit-button').forEach((button) => button.addEventListener('click', () => editUnit(button.dataset.unitId)));
}

function openNewUnit() {
  const form = $('#unitForm');
  form.reset(); $('#unitId').value = ''; form.elements.is_active.checked = true; form.elements.color.value = '#087a59';
  $('#unitColorValue').textContent = '#087A59'; $('#unitFormTitle').textContent = 'Add Unit'; $('#saveUnit').textContent = 'Save Unit';
  $('#unitFormErrors').classList.add('hidden'); $('#unitEditorPanel').classList.remove('hidden');
  form.elements.code.focus();
}

function editUnit(id) {
  const unit = state.verticals.find((candidate) => Number(candidate.id) === Number(id));
  if (!unit || !state.canEdit) return;
  const form = $('#unitForm');
  form.elements.code.value = unit.code || ''; form.elements.name.value = unit.name || '';
  form.elements.description.value = unit.description || ''; form.elements.color.value = unit.color || '#087a59';
  form.elements.is_active.checked = Boolean(unit.is_active); $('#unitId').value = unit.id;
  $('#unitColorValue').textContent = form.elements.color.value.toUpperCase(); $('#unitFormTitle').textContent = `Edit ${unit.name}`;
  $('#saveUnit').textContent = 'Update Unit'; $('#unitFormErrors').classList.add('hidden'); $('#unitEditorPanel').classList.remove('hidden');
  form.elements.name.focus();
}

function closeUnitEditor() {
  $('#unitEditorPanel').classList.add('hidden'); $('#unitFormErrors').classList.add('hidden');
}

function resetPortfolioForm() {
  const form = $('#portfolioForm'); form.reset(); $('#portfolioId').value = '';
  $('#portfolioFormTitle').textContent = 'Add portfolio'; $('#savePortfolio').textContent = 'Save portfolio';
  $('#portfolioFormErrors').classList.add('hidden');
}

async function editPortfolio(id) {
  try {
    const portfolio = await api(`/portfolios/${id}`);
    closeDrawer(); navigate('portfolio-form');
    $('#portfolioId').value = portfolio.id;
    $('#portfolioFormTitle').textContent = `Edit ${portfolio.code}`;
    $('#savePortfolio').textContent = 'Update portfolio';
    fillForm($('#portfolioForm'), portfolio);
  } catch (error) { toast(error.message, 'error'); }
}

function resetDemandForm() {
  const form = $('#demandForm'); form.reset(); $('#demandRecordId').value = '';
  $('#demandFormTitle').textContent = 'Add demand'; $('#saveDemand').textContent = 'Save demand';
  $('#demandFormErrors').classList.add('hidden');
}

async function editDemand(id) {
  try {
    const demand = await api(`/demands/${id}`);
    closeDrawer(); navigate('demand-form');
    $('#demandRecordId').value = demand.id;
    $('#demandFormTitle').textContent = `Edit ${demand.demand_id}`;
    $('#saveDemand').textContent = 'Update demand';
    fillForm($('#demandForm'), demand);
  } catch (error) { toast(error.message, 'error'); }
}

function fillForm(form, record) {
  for (const [key, value] of Object.entries(record)) {
    const control = form.elements.namedItem(key);
    if (!control) continue;
    if (control.type === 'date') control.value = dateInputValue(value);
    else control.value = value ?? '';
  }
}

function editPerson(person) {
  closeDrawer(); navigate('add-person');
  $('#personId').value = person.id; $('#formTitle').textContent = `Edit ${person.display_name}`; $('#savePerson').textContent = 'Update profile';
  const form = $('#personForm');
  for (const [key, value] of Object.entries(person)) {
    const control = form.elements.namedItem(key); if (!control) continue;
    if (control.type === 'checkbox') control.checked = Boolean(value); else control.value = value ?? '';
  }
  if (person.photo_url) { $('#photoPreview').style.backgroundImage = `url("${appUrl(person.photo_url)}")`; $('#photoPreview').textContent = ''; }
  else $('#photoPreview').textContent = initials(person.display_name);
  $$('#managerSelect option').forEach((option) => option.disabled = Number(option.value) === Number(person.id));
  updateHierarchyHeadControl();
}

function updateHierarchyHeadControl() {
  const checkbox = $('#personForm').elements.is_vertical_head;
  const hasManager = Boolean($('#managerSelect').value);
  checkbox.checked = !hasManager;
  checkbox.disabled = hasManager;
}

async function openPhotoEditor(event) {
  const file = event.target.files[0];
  if (!file) return;
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
    event.target.value = '';
    return toast('Choose a JPG, PNG or WebP image.', 'error');
  }
  if (file.size > 30 * 1024 * 1024) {
    event.target.value = '';
    return toast('The original image must be 30 MB or smaller.', 'error');
  }

  try {
    releasePhotoSource();
    const source = await decodeImageFile(file);
    photoEditor.source = source;
    photoEditor.file = file;
    photoEditor.width = source.naturalWidth || source.width;
    photoEditor.height = source.naturalHeight || source.height;
    photoEditor.minScale = Math.max(PHOTO_OUTPUT_WIDTH / photoEditor.width, PHOTO_OUTPUT_HEIGHT / photoEditor.height) * PHOTO_BASE_OVERSCAN;
    resetPhotoCrop();
    $('#photoOriginalDetails').textContent = `${photoEditor.width} × ${photoEditor.height} · ${formatBytes(file.size)}`;
    $('#photoEditorModal').classList.remove('hidden');
    document.body.style.overflow = 'hidden';
    renderPhotoCanvas();
  } catch (error) {
    event.target.value = '';
    toast('The selected image could not be opened.', 'error');
  }
}

function decodeImageFile(file) {
  if ('createImageBitmap' in window) {
    return createImageBitmap(file, { imageOrientation: 'from-image' }).catch(() => decodeImageFallback(file));
  }
  return decodeImageFallback(file);
}

function decodeImageFallback(file) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    const url = URL.createObjectURL(file);
    image.onload = () => { URL.revokeObjectURL(url); resolve(image); };
    image.onerror = () => { URL.revokeObjectURL(url); reject(new Error('Image decoding failed.')); };
    image.src = url;
  });
}

function bindPhotoCanvasEvents() {
  const canvas = $('#photoEditorCanvas');
  const workspace = $('.crop-workspace');
  let startX = 0; let startY = 0; let originalX = 0; let originalY = 0;

  canvas.addEventListener('pointerdown', (event) => {
    if (!photoEditor.source) return;
    canvas.setPointerCapture(event.pointerId);
    photoEditor.dragging = true;
    workspace.classList.add('dragging');
    startX = event.clientX; startY = event.clientY;
    originalX = photoEditor.offsetX; originalY = photoEditor.offsetY;
  });
  canvas.addEventListener('pointermove', (event) => {
    if (!photoEditor.dragging) return;
    const rect = canvas.getBoundingClientRect();
    photoEditor.offsetX = originalX + (event.clientX - startX) * (PHOTO_OUTPUT_WIDTH / rect.width);
    photoEditor.offsetY = originalY + (event.clientY - startY) * (PHOTO_OUTPUT_HEIGHT / rect.height);
    clampPhotoPosition(); renderPhotoCanvas();
  });
  const stopDrag = () => { photoEditor.dragging = false; workspace.classList.remove('dragging'); };
  canvas.addEventListener('pointerup', stopDrag);
  canvas.addEventListener('pointercancel', stopDrag);
  canvas.addEventListener('wheel', (event) => {
    if (!photoEditor.source) return;
    event.preventDefault();
    const slider = $('#photoZoom');
    slider.value = String(Math.min(3, Math.max(.9, Number(slider.value) + (event.deltaY > 0 ? -.08 : .08))));
    updatePhotoZoom();
  }, { passive: false });
  $('#photoEditorModal').addEventListener('click', (event) => {
    if (event.target.id === 'photoEditorModal') closePhotoEditor();
  });
}

function updatePhotoZoom() {
  photoEditor.zoom = Number($('#photoZoom').value);
  $('#photoZoomValue').textContent = `${Math.round(photoEditor.zoom * 100)}%`;
  clampPhotoPosition(); renderPhotoCanvas();
}

function resetPhotoCrop() {
  photoEditor.zoom = 1;
  photoEditor.offsetX = 0;
  photoEditor.offsetY = 0;
  $('#photoZoom').value = '1';
  $('#photoZoomValue').textContent = '100%';
  if (photoEditor.source) renderPhotoCanvas();
}

function clampPhotoPosition() {
  if (!photoEditor.source) return;
  const scale = photoEditor.minScale * photoEditor.zoom;
  const maxX = Math.max(0, (photoEditor.width * scale - PHOTO_OUTPUT_WIDTH) / 2);
  const maxY = Math.max(0, (photoEditor.height * scale - PHOTO_OUTPUT_HEIGHT) / 2);
  photoEditor.offsetX = Math.min(maxX, Math.max(-maxX, photoEditor.offsetX));
  photoEditor.offsetY = Math.min(maxY, Math.max(-maxY, photoEditor.offsetY));
}

function renderPhotoCanvas({ guides = true } = {}) {
  if (!photoEditor.source) return;
  const canvas = $('#photoEditorCanvas');
  const ctx = canvas.getContext('2d');
  const width = PHOTO_OUTPUT_WIDTH;
  const height = PHOTO_OUTPUT_HEIGHT;
  const scale = photoEditor.minScale * photoEditor.zoom;
  clampPhotoPosition();

  ctx.clearRect(0, 0, width, height);
  const background = ctx.createLinearGradient(0, 0, width, height);
  background.addColorStop(0, '#eef5f1'); background.addColorStop(1, '#d9e8e1');
  ctx.fillStyle = background; ctx.fillRect(0, 0, width, height);
  ctx.imageSmoothingEnabled = true; ctx.imageSmoothingQuality = 'high';
  const drawWidth = photoEditor.width * scale;
  const drawHeight = photoEditor.height * scale;
  ctx.drawImage(
    photoEditor.source,
    width / 2 + photoEditor.offsetX - drawWidth / 2,
    height / 2 + photoEditor.offsetY - drawHeight / 2,
    drawWidth,
    drawHeight
  );

  if (guides) {
    ctx.save();
    ctx.strokeStyle = 'rgba(173,134,31,.9)'; ctx.lineWidth = 4;
    ctx.strokeRect(3, 3, width - 6, height - 6);
    ctx.setLineDash([13, 10]); ctx.strokeStyle = 'rgba(0,107,76,.88)'; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.arc(width / 2, height * .36, width * .3, 0, Math.PI * 2); ctx.stroke();
    ctx.setLineDash([]); ctx.strokeStyle = 'rgba(255,255,255,.46)'; ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.moveTo(width / 2, 0); ctx.lineTo(width / 2, height);
    ctx.moveTo(0, height * .36); ctx.lineTo(width, height * .36);
    ctx.stroke(); ctx.restore();
  }
}

async function applyPhotoCrop() {
  if (!photoEditor.source) return;
  const button = $('#applyPhotoCrop');
  button.disabled = true; button.textContent = 'Optimizing & uploading…';
  try {
    const blob = await createCompressedPhoto();
    const body = new FormData();
    body.append('photo', blob, `profile-${Date.now()}.webp`);
    const result = await api('/uploads/profile-photo', { method: 'POST', body });
    $('#photoUrl').value = result.url;
    $('#photoPreview').style.backgroundImage = `url("${appUrl(result.url)}")`;
    $('#photoPreview').textContent = '';
    closePhotoEditor();
    toast(`Photo cropped and compressed to ${formatBytes(blob.size)}.`, 'success');
  } catch (error) {
    toast(error.message || 'The photo could not be uploaded.', 'error');
  } finally {
    button.disabled = false; button.textContent = 'Crop, compress & use photo';
  }
}

async function createCompressedPhoto() {
  renderPhotoCanvas({ guides: false });
  let quality = .9;
  let blob = await canvasToBlob($('#photoEditorCanvas'), quality);
  while (blob.size > 450 * 1024 && quality > .62) {
    quality -= .08;
    blob = await canvasToBlob($('#photoEditorCanvas'), quality);
  }
  renderPhotoCanvas();
  return blob;
}

function canvasToBlob(canvas, quality) {
  return new Promise((resolve, reject) => canvas.toBlob(
    (blob) => blob ? resolve(blob) : reject(new Error('Photo compression failed.')),
    'image/webp', quality
  ));
}

function closePhotoEditor() {
  $('#photoEditorModal').classList.add('hidden');
  document.body.style.overflow = '';
  $('#photoInput').value = '';
  releasePhotoSource();
}

function releasePhotoSource() {
  if (photoEditor.source && typeof photoEditor.source.close === 'function') photoEditor.source.close();
  photoEditor.source = null; photoEditor.file = null;
}

function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

async function savePerson(event) {
  event.preventDefault();
  const form = event.currentTarget; const submit = $('#savePerson');
  const payload = Object.fromEntries(new FormData(form).entries());
  payload.is_vertical_head = form.elements.is_vertical_head.checked;
  payload.is_active = true;
  ['manager_id', 'vertical_id'].forEach((key) => { if (!payload[key]) payload[key] = null; });
  const id = $('#personId').value;
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#formErrors').classList.add('hidden');
  try {
    await api(id ? `/people/${id}` : '/people', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    toast(id ? 'Profile updated successfully.' : 'Profile added successfully.', 'success');
    await loadReferenceData(); await loadDashboard(); navigate('people');
  } catch (error) {
    const box = $('#formErrors'); box.textContent = error.details?.map((x) => `${humanize(x.field)}: ${x.message}`).join(' · ') || error.message; box.classList.remove('hidden');
    toast(error.message, 'error');
  } finally { submit.disabled = false; submit.textContent = id ? 'Update profile' : 'Save profile'; }
}

async function savePortfolio(event) {
  event.preventDefault();
  const form = event.currentTarget; const submit = $('#savePortfolio'); const id = $('#portfolioId').value;
  const payload = Object.fromEntries(new FormData(form).entries()); payload.is_active = payload.status !== 'Archived';
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#portfolioFormErrors').classList.add('hidden');
  try {
    await api(id ? `/portfolios/${id}` : '/portfolios', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    toast(id ? 'Portfolio updated successfully.' : 'Portfolio created successfully.', 'success');
    await loadReferenceData(); await loadDashboard(); navigate('portfolios');
  } catch (error) {
    showFormError('#portfolioFormErrors', error); toast(error.message, 'error');
  } finally { submit.disabled = false; submit.textContent = id ? 'Update portfolio' : 'Save portfolio'; }
}

async function saveDemand(event) {
  event.preventDefault();
  const form = event.currentTarget; const submit = $('#saveDemand'); const id = $('#demandRecordId').value;
  const payload = Object.fromEntries(new FormData(form).entries()); payload.is_active = !['Archived', 'Cancelled'].includes(payload.status);
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#demandFormErrors').classList.add('hidden');
  try {
    await api(id ? `/demands/${id}` : '/demands', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    toast(id ? 'Demand updated successfully.' : 'Demand registered successfully.', 'success');
    await loadReferenceData(); await loadDashboard(); navigate('demands');
  } catch (error) {
    showFormError('#demandFormErrors', error); toast(error.message, 'error');
  } finally { submit.disabled = false; submit.textContent = id ? 'Update demand' : 'Save demand'; }
}

async function savePlatform(event) {
  event.preventDefault();
  const form = event.currentTarget; const submit = $('#savePlatform'); const id = $('#platformId').value;
  const payload = Object.fromEntries(new FormData(form).entries());
  payload.owner_person_id = payload.owner_person_id || null;
  payload.support_person_ids = $$('#platformSupportOptions input:checked').map((input) => Number(input.value));
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#platformFormErrors').classList.add('hidden');
  try {
    await api(id ? `/platforms/${id}` : '/platforms', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    state.activePortfolioManager = await api(`/portfolio-managers/${payload.manager_context_id}`);
    renderPortfolioManager(); closePlatformEditor();
    toast(id ? 'Platform updated successfully.' : 'Platform added successfully.', 'success');
  } catch (error) {
    showFormError('#platformFormErrors', error); toast(error.message, 'error');
  } finally { submit.disabled = false; submit.textContent = id ? 'Update Platform' : 'Save Platform'; }
}

async function saveUnit(event) {
  event.preventDefault();
  const form = event.currentTarget; const submit = $('#saveUnit'); const id = $('#unitId').value;
  const payload = Object.fromEntries(new FormData(form).entries()); payload.is_active = form.elements.is_active.checked;
  submit.disabled = true; submit.textContent = id ? 'Updating…' : 'Saving…'; $('#unitFormErrors').classList.add('hidden');
  try {
    await api(id ? `/units/${id}` : '/units', { method: id ? 'PUT' : 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } });
    toast(id ? 'Unit updated successfully.' : 'Unit added successfully.', 'success');
    await loadReferenceData(); await loadDashboard(); closeUnitEditor(); renderUnits();
  } catch (error) {
    showFormError('#unitFormErrors', error); toast(error.message, 'error');
  } finally { submit.disabled = false; submit.textContent = id ? 'Update Unit' : 'Save Unit'; }
}

function showFormError(selector, error) {
  const box = $(selector);
  box.textContent = error.details?.map((item) => `${humanize(item.field)}: ${item.message}`).join(' · ') || error.message;
  box.classList.remove('hidden');
}

async function logout() {
  try { const result = await apiRaw(appPath('auth/logout'), { method: 'POST' }); window.location.href = result.redirectUrl || appPath(); }
  catch (error) { toast(error.message, 'error'); }
}

async function api(path, options = {}) {
  const response = await fetch(`${API}${path}`, options);
  const payload = await response.json().catch(() => ({}));
  if (!response.ok) {
    if (response.status === 401 && !options.allowAnonymous) return showLogin();
    const error = new Error(payload.error?.message || `Request failed (${response.status})`); error.details = payload.error?.details; throw error;
  }
  return payload;
}

async function apiRaw(path, options = {}) {
  const response = await fetch(path, options); const payload = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(payload.error?.message || 'Request failed.'); return payload;
}

function avatarHtml(person, className) {
  const style = person.photo_url ? ` style="background-image:url('${escAttr(appUrl(person.photo_url))}')"` : '';
  return `<span class="${className}"${style}>${person.photo_url ? '' : initials(person.display_name)}</span>`;
}
function initials(name = '') { return name.split(/\s+/).slice(0, 2).map((word) => word[0]).join('').toUpperCase(); }
function esc(value = '') { return String(value).replace(/[&<>'"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[c]); }
function escAttr(value = '') { return esc(value).replace(/`/g, '&#96;'); }
function titleCase(value) { return value.replace(/\b\w/g, (c) => c.toUpperCase()); }
function humanize(value) { return titleCase(value.replaceAll('_', ' ')); }
function dateInputValue(value) { return value ? String(value).slice(0, 10) : ''; }
function formatDate(value) { return value ? new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(`${dateInputValue(value)}T00:00:00`)) : '—'; }
function debounce(fn, delay) { let timer; return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), delay); }; }
function toast(message, type = 'success') { const node = document.createElement('div'); node.className = `toast ${type}`; node.textContent = message; $('#toastHost').appendChild(node); setTimeout(() => node.remove(), 3800); }
