import * as THREE from 'three';
import { XRControllerModelFactory } from 'three/addons/webxr/XRControllerModelFactory.js';
import { buildWorld, L } from './world.js';
import { ITEMS, ACTIVITIES, AREAS, TOUR } from './content.js';
import { Runner } from './activities.js';
import { Sound } from './audio.js';

const $ = (id) => document.getElementById(id);
const params = new URLSearchParams(location.search);
const CAPTURE = params.has('capture');

// ------------------------------------------------------------------ saved state
const store = {
  get(k, d) { try { return JSON.parse(localStorage.getItem('abyss.' + k)) ?? d; } catch { return d; } },
  set(k, v) { try { localStorage.setItem('abyss.' + k, JSON.stringify(v)); } catch { /* private mode */ } },
};
const found = new Set(CAPTURE ? [] : store.get('found', []));
const completed = new Set(CAPTURE ? [] : store.get('completed', []));
const settings = Object.assign({ reduced: false, light: false, contrast: false, muted: false }, CAPTURE ? {} : store.get('settings', {}));
const saveAll = () => { store.set('found', [...found]); store.set('completed', [...completed]); store.set('settings', settings); };

// ------------------------------------------------------------------ renderer
const canvas = $('view');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance', preserveDrawingBuffer: CAPTURE });
renderer.setPixelRatio(CAPTURE ? 1 : Math.min(devicePixelRatio, settings.light ? 1 : 1.75));
renderer.setSize(innerWidth, innerHeight);
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.05;
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.xr.enabled = true;
renderer.xr.setReferenceSpaceType('local-floor');

const camera = new THREE.PerspectiveCamera(70, innerWidth / innerHeight, 0.05, 180);
const rig = new THREE.Group();
rig.add(camera);
let yaw = 0, pitch = -0.05;

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight; camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight); moveInk();
});

const sound = new Sound();
sound.muted = settings.muted;

// ------------------------------------------------------------------ load
let world;
const loadFill = $('load-fill'), loadLabel = $('load-label');
const nice = (n) => n.replace(/^scan_/, '').replace(/_/g, ' ');
buildWorld(renderer, (p, name) => { loadFill.style.width = `${Math.round(p * 100)}%`; loadLabel.textContent = `Loading ${nice(name)}…`; })
  .then(async (w) => {
    world = w;
    world.scene.add(rig);
    placeAt(world.anchors.hub, 0);
    $('disc-total').textContent = world.interactables.size;
    updateFound();
    loadLabel.textContent = 'Ready';
    renderer.compile(world.scene, camera);
    applySettings();
    $('start-row').classList.add('ready');
    if (params.has('at')) { // debug: ?at=fwd,right,height,lookFwd,lookRight
      const [f, r, h, lf, lr] = params.get('at').split(',').map(Number);
      const p = L(f, r); rig.position.set(p.x, world.heightAt(p.x, p.z) + h, p.z);
      const lp = L(lf, lr); yaw = Math.atan2(-(lp.x - p.x), -(lp.z - p.z)); pitch = -0.08;
    }
    if (CAPTURE) { window.__abyss = { ready: true, step: captureStep, tourLength: TOUR.at(-1).t + 4 }; begin(true, true); }
    else if (params.has('tour')) begin(true);
    setupXR();
    if (!CAPTURE) renderer.setAnimationLoop(frame);
  })
  .catch((e) => { console.error(e); loadLabel.textContent = 'Something went wrong loading the scene. Try a refresh.'; });

function placeAt(p, newYaw) {
  if (params.has('at') && !world.scene.userData.placed) { world.scene.userData.placed = true; return; }
  rig.position.set(p.x, world.heightAt(p.x, p.z) + 1.7, p.z);
  if (newYaw !== undefined) yaw = newYaw;
}

function begin(tour = false, instant = false) {
  sound.start();
  $('loader').classList.add('gone');
  $('ui').classList.remove('hidden');
  if (params.has('clean')) { document.body.classList.add('clean'); $('ui').classList.add('ui-off'); }
  requestAnimationFrame(moveInk);
  if (tour) startTour(instant);
}
$('btn-dive').onclick = () => { sound.play('click'); begin(false); };
$('btn-tour-start').onclick = () => { sound.play('click'); begin(true); };

// ------------------------------------------------------------------ tabs + panels
const tabs = [...document.querySelectorAll('#tabs button')];
function moveInk() {
  const a = document.querySelector('#tabs button.active'), ink = $('tab-ink');
  if (!a) return;
  ink.style.left = a.offsetLeft + 'px'; ink.style.width = a.offsetWidth + 'px';
}
function setView(v) {
  tabs.forEach((b) => b.classList.toggle('active', b.dataset.view === v));
  moveInk();
  $('panel-guided').classList.toggle('open', v === 'guided');
  $('panel-about').classList.toggle('open', v === 'about');
  if (v === 'guided') renderActivities();
  if (v === 'tour') startTour();
  sound.play('menu');
}
tabs.forEach((b) => (b.onclick = () => setView(b.dataset.view)));

function renderActivities() {
  const list = $('activity-list');
  list.innerHTML = '';
  ACTIVITIES.forEach((a, i) => {
    const b = document.createElement('button');
    b.className = 'activity';
    b.style.transitionDelay = `${i * 60}ms`;
    const done = completed.has(a.id);
    b.innerHTML = `<b>${a.title}</b><span class="state ${done ? 'done' : ''}">${done ? '✓ Done' : a.minutes + ' min'}</span><small>${a.intro}</small>`;
    b.onclick = () => startActivity(a);
    list.appendChild(b);
  });
}

// ------------------------------------------------------------------ settings
function applySettings() {
  document.body.classList.toggle('reduced', settings.reduced);
  document.body.classList.toggle('contrast', settings.contrast);
  $('opt-motion').checked = settings.reduced; $('opt-quality').checked = settings.light; $('opt-contrast').checked = settings.contrast;
  $('btn-sound').classList.toggle('muted', settings.muted);
  sound.setMuted(settings.muted);
  if (world) world.setLight(settings.light);
  if (!CAPTURE) renderer.setPixelRatio(Math.min(devicePixelRatio, settings.light ? 1 : 1.75));
  saveAll();
}
$('opt-motion').onchange = (e) => { settings.reduced = e.target.checked; applySettings(); };
$('opt-quality').onchange = (e) => { settings.light = e.target.checked; applySettings(); };
$('opt-contrast').onchange = (e) => { settings.contrast = e.target.checked; applySettings(); };
$('btn-sound').onclick = () => { settings.muted = !settings.muted; applySettings(); };
$('btn-reset').onclick = () => { found.clear(); completed.clear(); saveAll(); updateFound(); renderActivities(); toast('Progress cleared', false); };

// ------------------------------------------------------------------ small helpers
let toastTimer;
function toast(text, positive = true) {
  const t = $('toast');
  t.textContent = (positive ? '✓  ' : '') + text;
  t.className = 'toast show' + (positive ? '' : ' neutral');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => (t.className = 'toast' + (positive ? '' : ' neutral')), 2200);
}
function updateFound(bump) {
  $('disc-num').textContent = found.size;
  if (bump) { const n = $('disc-num'); n.classList.remove('bump'); void n.offsetWidth; n.classList.add('bump'); }
}
const tweens = [];
function tween(dur, fn, done) { tweens.push({ t: 0, dur: settings.reduced ? Math.min(dur, 0.15) : dur, fn, done }); }
const ease = (k) => 1 - Math.pow(1 - k, 3);
async function fadeJump(fn) {
  $('fade').classList.add('on'); sound.play('teleport');
  await new Promise((r) => setTimeout(r, 380));
  fn();
  setTimeout(() => $('fade').classList.remove('on'), 120);
}

// ------------------------------------------------------------------ desktop look + swim
const keys = new Set();
let dragging = false, moved = 0, lastX = 0, lastY = 0, mouse = new THREE.Vector2(), mouseIn = false;
canvas.addEventListener('pointerdown', (e) => { dragging = true; moved = 0; lastX = e.clientX; lastY = e.clientY; canvas.setPointerCapture(e.pointerId); canvas.classList.add('grabbing'); });
canvas.addEventListener('pointermove', (e) => {
  mouse.set((e.clientX / innerWidth) * 2 - 1, -(e.clientY / innerHeight) * 2 + 1); mouseIn = true;
  if (!dragging) return;
  const dx = e.clientX - lastX, dy = e.clientY - lastY;
  moved += Math.abs(dx) + Math.abs(dy); lastX = e.clientX; lastY = e.clientY;
  if (mode === 'inspect') { inspectDrag(dx, dy); return; }
  if (mode !== 'explore') return;
  yaw -= dx * 0.0032; pitch = Math.max(-1.2, Math.min(1.2, pitch - dy * 0.0028));
  hideHint();
});
canvas.addEventListener('pointerup', () => {
  dragging = false; canvas.classList.remove('grabbing');
  if (moved < 6 && mode === 'explore' && hovered) select(hovered.userData.id);
});
addEventListener('keydown', (e) => {
  if (e.target.tagName === 'INPUT') return;
  keys.add(e.code);
  if (e.code === 'KeyH') $('ui').classList.toggle('ui-off');
  if (e.code === 'KeyR' && mode === 'explore') fadeJump(() => placeAt(world.anchors.hub, 0));
  if (e.code === 'Escape') { if (mode === 'tour') endTour(); else closeCard(); }
  if (/Key[WASDQE]|Arrow/.test(e.code)) hideHint();
});
addEventListener('keyup', (e) => keys.delete(e.code));
let hintGone = false;
function hideHint() { if (!hintGone) { hintGone = true; $('hint').classList.add('gone'); } }

function swim(dt) {
  const f = (keys.has('KeyW') || keys.has('ArrowUp') ? 1 : 0) - (keys.has('KeyS') || keys.has('ArrowDown') ? 1 : 0);
  const s = (keys.has('KeyD') || keys.has('ArrowRight') ? 1 : 0) - (keys.has('KeyA') || keys.has('ArrowLeft') ? 1 : 0);
  const u = (keys.has('KeyE') || keys.has('Space') ? 1 : 0) - (keys.has('KeyQ') ? 1 : 0);
  const speed = (keys.has('ShiftLeft') ? 6 : 3) * dt;
  const fwd = new THREE.Vector3(-Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), -Math.cos(yaw) * Math.cos(pitch));
  const right = new THREE.Vector3(Math.cos(yaw), 0, -Math.sin(yaw));
  vel.lerp(fwd.multiplyScalar(f).add(right.multiplyScalar(s)).add(new THREE.Vector3(0, u, 0)).multiplyScalar(speed / Math.max(dt, 1e-4)), Math.min(1, dt * 4));
  rig.position.addScaledVector(vel, dt);
  rig.position.x = THREE.MathUtils.clamp(rig.position.x, -62, 62);
  rig.position.z = THREE.MathUtils.clamp(rig.position.z, -78, 50);
  const floor = world.heightAt(rig.position.x, rig.position.z) + 0.7;
  rig.position.y = THREE.MathUtils.clamp(rig.position.y, floor, 12.5);
}
const vel = new THREE.Vector3();

// ------------------------------------------------------------------ hover + select
const ray = new THREE.Raycaster();
let hovered = null;
function updateHover(t) {
  let hit = null;
  if (mouseIn && mode === 'explore' && !dragging) {
    ray.setFromCamera(mouse, camera);
    hit = ray.intersectObjects([...world.interactables.values()], false)[0]?.object || null;
    if (hit && hit.position.distanceTo(rig.position) > 22) hit = null;
  }
  if (hit !== hovered) {
    if (hovered) hovered.material.emissive.setRGB(0, 0, 0);
    hovered = hit;
    canvas.classList.toggle('pointing', !!hit);
    if (hit) sound.play('hover');
  }
  // everything you can select breathes a little so people notice it, hovered glows properly
  for (const m of world.interactables.values()) {
    if (m === hovered) m.material.emissive.setRGB(0.32, 0.28, 0.17).multiplyScalar(0.8 + 0.2 * Math.sin(t * 6));
    else if (!found.has(m.userData.id)) m.material.emissive.setRGB(0.05, 0.045, 0.028).multiplyScalar(0.5 + 0.5 * Math.sin(t * 1.6 + m.position.x));
    else m.material.emissive.setRGB(0, 0, 0);
  }
}

let current = null;
function select(id) {
  const item = ITEMS[id];
  if (!item) return;
  let res = 'ignored';
  if (runner.state === 'running') {
    const k = runner.step.kind === 'inspect' ? null : (runner.step.kind === 'interact' ? 'interact' : 'discover');
    if (k) res = runner.report(k, id);
    else if (!runner.step.targets.includes(id)) res = 'wrong';
  }
  if (res === 'wrong') { sound.play('fail'); toast('Not part of this task', false); }
  else sound.play('select');
  if (!found.has(id)) { found.add(id); saveAll(); updateFound(true); sound.play('discover'); if (res !== 'wrong') toast('New discovery'); }
  current = id;
  $('card-title').textContent = item.title;
  $('card-sub').textContent = item.sub;
  $('card-body').textContent = item.body;
  $('card-inspect').style.display = item.inspect ? '' : 'none';
  $('card').classList.add('open');
  if (renderer.xr.isPresenting) showXRPanel(item);
}
function closeCard() { $('card').classList.remove('open'); current = null; }
$('card-close').onclick = () => { sound.play('click'); closeCard(); };
$('card-inspect').onclick = () => { sound.play('click'); if (current) startInspect(current); };

// ------------------------------------------------------------------ inspect: fly in close, drag turns the object
let mode = 'explore', inspectSaved = null, inspectObj = null;
function startInspect(id) {
  const obj = world.interactables.get(id);
  mode = 'inspect'; inspectObj = obj;
  inspectSaved = { pos: rig.position.clone(), yaw, pitch };
  const box = new THREE.Box3().setFromObject(obj), c = box.getCenter(new THREE.Vector3()), r = box.getSize(new THREE.Vector3()).length() * 0.5;
  const dir = rig.position.clone().sub(c).setY(0).normalize();
  const target = c.clone().add(dir.multiplyScalar(Math.max(0.45, r * 2.6))).add(new THREE.Vector3(0, r * 0.6, 0));
  const from = rig.position.clone(), y0 = yaw, p0 = pitch;
  const toYaw = Math.atan2(-(c.x - target.x), -(c.z - target.z));
  const toPitch = Math.atan2(c.y - target.y, Math.hypot(c.x - target.x, c.z - target.z));
  let dy = toYaw - y0; dy = Math.atan2(Math.sin(dy), Math.cos(dy));
  tween(1.1, (k) => { rig.position.lerpVectors(from, target, ease(k)); yaw = y0 + dy * ease(k); pitch = p0 + (toPitch - p0) * ease(k); });
  closeCard();
  $('inspect-bar').classList.add('open');
}
function inspectDrag(dx, dy) {
  inspectObj.rotateOnWorldAxis(new THREE.Vector3(0, 1, 0), dx * 0.01);
  inspectObj.rotateOnWorldAxis(new THREE.Vector3(Math.cos(yaw), 0, -Math.sin(yaw)), dy * 0.01);
}
$('inspect-done').onclick = () => {
  sound.play('click');
  const obj = inspectObj, q0 = obj.quaternion.clone(), from = rig.position.clone(), y0 = yaw, p0 = pitch, s = inspectSaved;
  tween(1.0, (k) => { obj.quaternion.slerpQuaternions(q0, obj.userData.home.q, ease(k)); rig.position.lerpVectors(from, s.pos, ease(k)); yaw = y0 + (s.yaw - y0) * ease(k); pitch = p0 + (s.pitch - p0) * ease(k); },
    () => { mode = 'explore'; });
  $('inspect-bar').classList.remove('open');
  if (runner.state === 'running') runner.report('inspect', obj.userData.id);
  toast('Inspection done');
};

// ------------------------------------------------------------------ guided activities
const runner = new Runner();
let activeActivity = null;
function startActivity(a) {
  sound.play('click');
  setView('explore');
  activeActivity = a;
  for (const g of world.markers.values()) g.visible = false;
  runner.start(a);
  $('hud-title').textContent = a.title;
  $('hud').classList.add('open'); document.body.classList.add('has-hud');
}
runner.onStep = (s, i) => {
  const el = $('hud-step');
  el.classList.add('swap');
  setTimeout(() => { el.textContent = s.text; el.classList.remove('swap'); }, 250);
  for (const g of world.markers.values()) g.visible = s.kind === 'reach' && s.targets.includes(g.userData.id);
  updateHudCount();
};
runner.onHit = () => { updateHudCount(); };
runner.onStepDone = (s) => { if (runner.index < runner.activity.steps.length - 1) { sound.play('step'); toast('Step done'); } };
runner.onDone = (a) => {
  for (const g of world.markers.values()) g.visible = false;
  completed.add(a.id); saveAll();
  sound.play('done');
  $('hud').classList.remove('open'); document.body.classList.remove('has-hud');
  $('done-title').textContent = a.title; $('done-body').textContent = a.summary;
  setTimeout(() => $('done-modal').classList.add('open'), 500);
};
function updateHudCount() {
  const s = runner.step;
  $('hud-fill').style.width = `${runner.progress * 100}%`;
  if (!s) return;
  $('hud-count').textContent = s.all && s.targets.length > 1 ? `${s.targets.filter((t) => runner.hits.has(t)).length} / ${s.targets.length}` : `Step ${runner.index + 1} of ${runner.activity.steps.length}`;
}
$('hud-stop').onclick = () => { runner.reset(); activeActivity = null; $('hud').classList.remove('open'); document.body.classList.remove('has-hud'); for (const g of world.markers.values()) g.visible = false; };
$('done-explore').onclick = () => $('done-modal').classList.remove('open');
$('done-again').onclick = () => { $('done-modal').classList.remove('open'); startActivity(activeActivity); };

function guideTarget() {
  const s = runner.step;
  if (!s) return null;
  let best = null, bd = Infinity;
  for (const id of s.targets) {
    if (runner.hits.has(id)) continue;
    const obj = world.markers.get(id) || world.interactables.get(id);
    if (!obj) continue;
    const d = obj.position.distanceTo(rig.position);
    if (d < bd) { bd = d; best = obj; }
  }
  return best;
}
function updateGuide(headPos, headYaw) {
  const s = runner.step;
  if (!s) return;
  if (s.kind === 'reach') {
    for (const id of s.targets) {
      const g = world.markers.get(id);
      if (g && Math.hypot(g.position.x - headPos.x, g.position.z - headPos.z) < 2.2) runner.report('reach', id);
    }
  }
  const t = guideTarget();
  if (!t) return;
  const dx = t.position.x - headPos.x, dz = t.position.z - headPos.z;
  const ang = Math.atan2(-dx, -dz) - headYaw;
  $('hud-arrow').style.transform = `rotate(${-ang}rad)`;
  $('hud-dist').textContent = `${Math.round(Math.hypot(dx, dz))} m away`;
}

// ------------------------------------------------------------------ area label
let shownArea = 'descent';
function updateArea() {
  if (world.area === shownArea) return;
  shownArea = world.area;
  const chip = $('chip-area');
  chip.style.opacity = 0;
  setTimeout(() => { chip.textContent = AREAS.find((a) => a.id === shownArea)?.name || ''; chip.style.opacity = 1; }, 300);
}

// ------------------------------------------------------------------ cinematic tour
const tourPts = TOUR.map((p) => { const v = L(p.at[0], p.at[1]); v.y = 0; return v; });
const lookPts = TOUR.map((p) => { const v = L(p.look[0], p.look[1]); return v; });
let tourCurve, lookCurve, tourT = 0, tourCapIdx = -1;
function buildTourCurves() {
  tourCurve = new THREE.CatmullRomCurve3(tourPts.map((v, i) => new THREE.Vector3(v.x, world.heightAt(v.x, v.z) + TOUR[i].at[2], v.z)), false, 'centripetal');
  lookCurve = new THREE.CatmullRomCurve3(lookPts.map((v, i) => new THREE.Vector3(v.x, world.heightAt(v.x, v.z) + TOUR[i].look[2], v.z)), false, 'centripetal');
}
function startTour(instant) {
  if (!world) return;
  if (!tourCurve) buildTourCurves();
  closeCard(); $('panel-guided').classList.remove('open'); $('panel-about').classList.remove('open');
  mode = 'tour'; tourT = 0; tourCapIdx = -1;
  document.body.classList.add('touring');
  $('ui').classList.add('ui-off');
  if (!instant) { $('fade').classList.add('on'); setTimeout(() => $('fade').classList.remove('on'), 500); }
}
function endTour() {
  mode = 'explore';
  document.body.classList.remove('touring');
  $('caption').style.opacity = 0;
  document.querySelectorAll('.letterbox').forEach((x) => (x.style.height = '0'));
  if (!params.has('clean')) $('ui').classList.remove('ui-off');
  tabs.forEach((b) => b.classList.toggle('active', b.dataset.view === 'explore')); moveInk();
}
$('tour-exit').onclick = endTour;
function tourU(t) {
  const times = TOUR.map((p) => p.t);
  if (t <= 0) return 0;
  if (t >= times.at(-1)) return 1;
  let i = 0; while (times[i + 1] < t) i++;
  const k = (t - times[i]) / (times[i + 1] - times[i]);
  const ks = k * k * (3 - 2 * k) * 0.35 + k * 0.65; // a bit of ease in each leg, no stops
  return (i + ks) / (times.length - 1);
}
function updateTour(dt) {
  tourT += dt;
  const end = TOUR.at(-1).t;
  const u = tourU(tourT);
  const p = tourCurve.getPoint(u), l = lookCurve.getPoint(Math.min(1, u + 0.004));
  // gentle drift so it never feels like a dolly on rails
  p.y += Math.sin(tourT * 0.4) * 0.12; p.x += Math.sin(tourT * 0.23) * 0.15;
  rig.position.copy(p);
  const d = l.clone().sub(p);
  yaw = Math.atan2(-d.x, -d.z); pitch = Math.atan2(d.y, Math.hypot(d.x, d.z));
  // captions + letterbox are driven by the tour clock, not css timers, so the frame-by-frame recording matches live playback
  const capIdx = TOUR.findLastIndex((st) => st.cap && st.t <= tourT);
  const el = $('caption');
  if (capIdx !== tourCapIdx) {
    tourCapIdx = capIdx;
    const c = TOUR[capIdx]?.cap;
    if (c) { $('cap-kicker').textContent = c[0]; $('cap-title').textContent = c[1]; $('cap-body').textContent = c[2]; }
  }
  const since = tourCapIdx >= 0 ? tourT - TOUR[tourCapIdx].t - 0.6 : -1;
  const a = since < 0 ? 0 : Math.min(1, since / 1.2) * Math.min(1, Math.max(0, (7.2 - since) / 1.2));
  el.style.opacity = a; el.style.transform = `translateY(${(1 - a) * 14}px)`;
  const lb = Math.min(1, tourT / 1.4) * Math.min(1, Math.max(0, (end + 2 - tourT) / 1.4));
  document.querySelectorAll('.letterbox').forEach((x) => (x.style.height = `${9 * ease(lb)}vh`));
  if (tourT > end + 2) { if (CAPTURE) return; endTour(); }
}

// ------------------------------------------------------------------ VR
let controllers = [], xrPanel = null, grabbed = null, turnLatch = [false, false];
async function setupXR() {
  if (!navigator.xr || CAPTURE) return;
  let ok = false;
  try { ok = await navigator.xr.isSessionSupported('immersive-vr'); } catch { /* no xr */ }
  if (!ok) return;
  const btn = $('btn-vr');
  btn.hidden = false;
  btn.onclick = async () => {
    const session = await navigator.xr.requestSession('immersive-vr', { optionalFeatures: ['local-floor', 'bounded-floor'] });
    if (mode === 'tour') endTour();
    rig.position.y = world.heightAt(rig.position.x, rig.position.z);
    camera.rotation.set(0, 0, 0);
    rig.rotation.set(0, yaw, 0);
    await renderer.xr.setSession(session);
    session.addEventListener('end', () => { rig.position.y += 1.7; rig.rotation.set(0, 0, 0); });
  };
  const factory = new XRControllerModelFactory();
  for (let i = 0; i < 2; i++) {
    const c = renderer.xr.getController(i);
    const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3(0, 0, -1)]), new THREE.LineBasicMaterial({ color: 0xdfcd9e, transparent: true, opacity: 0.7 }));
    line.scale.z = 8; c.add(line);
    c.addEventListener('selectstart', () => xrSelect(c));
    c.addEventListener('selectend', () => xrRelease(c));
    rig.add(c);
    const grip = renderer.xr.getControllerGrip(i);
    grip.add(factory.createControllerModel(grip));
    rig.add(grip);
    controllers.push({ c, line });
  }
}
const tmpM = new THREE.Matrix4();
function controllerRay(c) {
  tmpM.identity().extractRotation(c.matrixWorld);
  ray.ray.origin.setFromMatrixPosition(c.matrixWorld);
  ray.ray.direction.set(0, 0, -1).applyMatrix4(tmpM);
}
function xrSelect(c) {
  controllerRay(c);
  const hit = ray.intersectObjects([...world.interactables.values()], false)[0];
  if (hit && hit.distance < 15) {
    const obj = hit.object, id = obj.userData.id;
    select(id);
    // inspectable things you just pick up and turn in your hand
    if (ITEMS[id]?.inspect) { c.attach(obj); grabbed = { obj, c }; }
    return;
  }
  const g = ray.intersectObject(world.terrain, false)[0];
  if (g && g.distance < 25) {
    // teleport: keep the headset's offset inside the play space
    const head = new THREE.Vector3(); camera.getWorldPosition(head);
    rig.position.x += g.point.x - head.x; rig.position.z += g.point.z - head.z;
    rig.position.y = world.heightAt(g.point.x, g.point.z);
    sound.play('teleport');
  }
}
function xrRelease(c) {
  if (!grabbed || grabbed.c !== c) return;
  const { obj } = grabbed; grabbed = null;
  world.scene.attach(obj);
  const p0 = obj.position.clone(), q0 = obj.quaternion.clone(), h = obj.userData.home;
  tween(0.8, (k) => { obj.position.lerpVectors(p0, h.p, ease(k)); obj.quaternion.slerpQuaternions(q0, h.q, ease(k)); });
  if (runner.state === 'running') runner.report('inspect', obj.userData.id);
  toast('Inspection done');
}
function xrSnapTurn() {
  const session = renderer.xr.getSession();
  if (!session) return;
  for (const src of session.inputSources) {
    const gp = src.gamepad; if (!gp || gp.axes.length < 4) continue;
    const i = src.handedness === 'left' ? 0 : 1, x = gp.axes[2];
    if (src.handedness !== 'right') continue;
    if (Math.abs(x) > 0.7 && !turnLatch[i]) {
      turnLatch[i] = true;
      const head = new THREE.Vector3(); camera.getWorldPosition(head);
      const a = -Math.sign(x) * Math.PI / 4;
      rig.position.sub(head).applyAxisAngle(new THREE.Vector3(0, 1, 0), a).add(head);
      rig.rotateY(a);
    } else if (Math.abs(x) < 0.3) turnLatch[i] = false;
  }
}
function showXRPanel(item) {
  if (!xrPanel) {
    const cv = document.createElement('canvas'); cv.width = 1024; cv.height = 600;
    const tex = new THREE.CanvasTexture(cv); tex.colorSpace = THREE.SRGBColorSpace;
    xrPanel = new THREE.Mesh(new THREE.PlaneGeometry(0.85, 0.5), new THREE.MeshBasicMaterial({ map: tex, transparent: true, fog: false }));
    xrPanel.userData = { cv, tex };
    world.scene.add(xrPanel);
  }
  const { cv, tex } = xrPanel.userData, g = cv.getContext('2d');
  g.clearRect(0, 0, 1024, 600);
  g.fillStyle = 'rgba(7,24,34,0.9)'; g.beginPath(); g.roundRect(0, 0, 1024, 600, 40); g.fill();
  g.fillStyle = '#84beb6'; g.font = '600 26px Inter, sans-serif'; g.fillText('SAMPLE CONTENT', 56, 80);
  g.fillStyle = '#ecf2f0'; g.font = '64px "Instrument Serif", Georgia, serif'; g.fillText(item.title, 56, 160);
  g.fillStyle = '#84beb6'; g.font = 'italic 36px "Instrument Serif", Georgia, serif'; g.fillText(item.sub, 56, 215);
  g.fillStyle = '#c8d8d8'; g.font = '30px Inter, sans-serif';
  let line = '', y = 290;
  for (const w of item.body.split(' ')) { if (g.measureText(line + w).width > 900) { g.fillText(line, 56, y); line = ''; y += 44; } line += w + ' '; }
  g.fillText(line, 56, y);
  tex.needsUpdate = true;
  const head = new THREE.Vector3(), dir = new THREE.Vector3();
  camera.getWorldPosition(head); camera.getWorldDirection(dir); dir.y = 0; dir.normalize();
  xrPanel.position.copy(head).addScaledVector(dir, 1.1).add(new THREE.Vector3(0, -0.12, 0));
  xrPanel.lookAt(head.x, xrPanel.position.y, head.z);
  xrPanel.visible = true;
}

// ------------------------------------------------------------------ frame
const clock = new THREE.Clock();
let elapsed = 0;
function frame() { step(Math.min(clock.getDelta(), 0.05)); }
function step(dt) {
  elapsed += dt;
  for (let i = tweens.length - 1; i >= 0; i--) {
    const tw = tweens[i]; tw.t += dt;
    const k = Math.min(1, tw.t / tw.dur); tw.fn(k);
    if (k >= 1) { tweens.splice(i, 1); tw.done?.(); }
  }
  const xr = renderer.xr.isPresenting;
  if (!xr) {
    if (mode === 'tour') updateTour(dt);
    else if (mode === 'explore') swim(dt);
    rig.rotation.set(0, yaw, 0);
    camera.rotation.set(pitch, 0, 0);
  } else {
    xrSnapTurn();
    for (const { c, line } of controllers) {
      controllerRay(c);
      const h = ray.intersectObjects([...world.interactables.values()], false)[0];
      line.material.color.set(h && h.distance < 15 ? 0xffffff : 0xdfcd9e);
    }
  }
  const head = new THREE.Vector3(); camera.getWorldPosition(head);
  const dir = new THREE.Vector3(); camera.getWorldDirection(dir);
  world.update(dt, elapsed, head, settings.reduced);
  if (!xr) updateHover(elapsed);
  updateGuide(head, Math.atan2(-dir.x, -dir.z));
  updateArea();
  renderer.render(world.scene, camera);
}

// capture mode: the page doesn't run its own loop, the recorder steps it frame by frame
function captureStep(dt) { step(dt); return tourT; }
