import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

// layout space: forward runs toward the deep (-z), right is +x. same numbers as the unity scene builder
export const L = (f, r) => new THREE.Vector3(r, 0, -f);

export const U = {
  uTime: { value: 0 },
  uCaustics: { value: null },
  uCausticStrength: { value: 1 },
  uSurfaceY: { value: 14 },
  uCausticDepth: { value: 26 },
  uSand: { value: null },
  uGlow: { value: 1.3 },
  uFogColor: { value: new THREE.Color() },
  uFogDensity: { value: 0.03 },
  uShaft: { value: 1 },
};

// one patch for every lit material: caustics on upward faces, optional sway / sand / glow / fish wiggle
export function patch(mat, o = {}) {
  const key = ['abyss', o.sway || 0, o.swayH || 0, !!o.sand, !!o.glow, o.fish ? 1 : 0, o.ray ? 1 : 0].join(':');
  mat.customProgramCacheKey = () => key;
  mat.onBeforeCompile = (s) => {
    Object.assign(s.uniforms, U, { uSway: { value: o.sway || 0 }, uSwayH: { value: o.swayH || 1 } });
    s.vertexShader = 'varying vec3 vAbyssWp;\nuniform float uTime, uSway, uSwayH;\n' + s.vertexShader
      .replace('#include <begin_vertex>', `#include <begin_vertex>
        vec4 abyssBase = vec4(transformed, 1.0);
        #ifdef USE_INSTANCING
          abyssBase = instanceMatrix * abyssBase;
        #endif
        abyssBase = modelMatrix * abyssBase;
        ${o.sway ? `float swk = pow(clamp(position.y / uSwayH, 0.0, 1.0), 1.5);
        transformed.x += sin(uTime * 0.9 + abyssBase.x * 0.35 + abyssBase.z * 0.2) * uSway * swk;
        transformed.z += cos(uTime * 0.7 + abyssBase.x * 0.21) * uSway * 0.6 * swk;` : ''}
        ${o.fish ? `transformed.x += sin(uTime * 9.0 + float(gl_InstanceID) * 1.7 + position.z * 24.0) * 0.014 * smoothstep(0.04, -0.14, position.z);` : ''}
        ${o.ray ? `transformed.y += sin(uTime * 1.1 - position.z * 0.6) * 0.32 * pow(abs(position.x) / 2.0, 1.4);` : ''}`)
      .replace('#include <project_vertex>', `#include <project_vertex>
        vec4 abyssW = vec4(transformed, 1.0);
        #ifdef USE_INSTANCING
          abyssW = instanceMatrix * abyssW;
        #endif
        vAbyssWp = (modelMatrix * abyssW).xyz;`);
    s.fragmentShader = `varying vec3 vAbyssWp;
      uniform float uTime, uCausticStrength, uSurfaceY, uCausticDepth, uGlow;
      uniform sampler2D uCaustics, uSand;\n` + s.fragmentShader
      .replace('#include <color_fragment>', `#include <color_fragment>
        ${o.sand ? `{
          vec3 sa = texture2D(uSand, vAbyssWp.xz * 0.32).rgb;
          vec3 sb = texture2D(uSand, vAbyssWp.xz * 0.074 + 0.37).rgb;
          float vl = dot(vColor.rgb, vec3(0.3, 0.59, 0.11));
          diffuseColor.rgb = mix(diffuseColor.rgb, mix(sa, sb, 0.35) * vl * 1.6, 0.9);
        }` : ''}`)
      .replace('#include <emissivemap_fragment>', `#include <emissivemap_fragment>
        ${o.glow ? `{
          float gl = dot(vColor.rgb, vec3(0.3, 0.59, 0.11));
          totalEmissiveRadiance += vec3(0.44, 0.88, 0.82) * uGlow * clamp(gl * 2.0 - 0.6, 0.0, 1.0) * (0.7 + 0.3 * sin(uTime * 0.6 + vAbyssWp.x));
        }` : ''}`)
      .replace('#include <lights_fragment_end>', `#include <lights_fragment_end>
        {
          vec3 wn = normalize(inverseTransformDirection(normal, viewMatrix));
          float up = clamp(wn.y, 0.0, 1.0);
          float c1 = texture2D(uCaustics, vAbyssWp.xz * 0.14 + vec2(uTime * 0.021, uTime * 0.013)).r;
          float c2 = texture2D(uCaustics, vAbyssWp.xz * 0.11 - vec2(uTime * 0.017, -uTime * 0.02)).r;
          float c = clamp((min(c1, c2) - 0.55) / 0.45, 0.0, 1.0);
          float fade = clamp(1.0 - (uSurfaceY - vAbyssWp.y) / uCausticDepth, 0.0, 1.0);
          reflectedLight.directDiffuse += diffuseColor.rgb * pow(c, 1.6) * up * uCausticStrength * fade * 1.6;
        }`);
  };
  return mat;
}

function rng(seed) {
  return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let t = Math.imul(seed ^ seed >>> 15, 1 | seed); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
}

export async function buildWorld(renderer, onProgress) {
  const scene = new THREE.Scene();
  const loader = new GLTFLoader();
  const texLoader = new THREE.TextureLoader();

  const manifest = await (await fetch('assets/models/manifest.json')).json();
  const heights = new Float32Array(await (await fetch('assets/models/seafloor_height.bin')).arrayBuffer());
  // the edge walls in the source terrain go above the water line, keep them well under it
  const RIM = 7;
  const soft = (y) => (y > RIM - 2 ? RIM - 2 + (1 - Math.exp(-(y - RIM + 2) / 2)) * 2 : y);
  for (let i = 0; i < heights.length; i++) heights[i] = soft(heights[i]);
  const N = manifest.height.n, HALF = manifest.height.half;
  const heightAt = (x, z) => {
    const fx = Math.min(N - 2, Math.max(1, (x + HALF) / (2 * HALF) * (N - 1)));
    const fz = Math.min(N - 2, Math.max(1, (z + HALF) / (2 * HALF) * (N - 1)));
    const i = Math.floor(fx), j = Math.floor(fz), a = fx - i, b = fz - j;
    const h = (ii, jj) => heights[jj * N + ii];
    return (h(i, j) * (1 - a) + h(i + 1, j) * a) * (1 - b) + (h(i, j + 1) * (1 - a) + h(i + 1, j + 1) * a) * b;
  };

  const caustics = texLoader.load('assets/tex/caustics.jpg');
  caustics.wrapS = caustics.wrapT = THREE.RepeatWrapping;
  const sand = texLoader.load('assets/tex/sand_diff.jpg');
  sand.wrapS = sand.wrapT = THREE.RepeatWrapping; sand.colorSpace = THREE.SRGBColorSpace;
  sand.anisotropy = renderer.capabilities.getMaxAnisotropy();
  U.uCaustics.value = caustics; U.uSand.value = sand;

  // load every model, keep geometry + material of its first mesh
  const names = [...manifest.generated, ...manifest.scans];
  const assets = {};
  let done = 0;
  await Promise.all(names.map(async (n) => {
    const g = await loader.loadAsync(`assets/models/${n}.glb`);
    let mesh = null;
    g.scene.traverse((o) => { if (!mesh && o.isMesh) mesh = o; });
    mesh.updateWorldMatrix(true, false);
    const geo = mesh.geometry.clone().applyMatrix4(mesh.matrixWorld);
    const mat = mesh.material;
    mat.roughness = 0.88; mat.metalness = 0;
    if (mat.map) mat.map.anisotropy = 4;
    assets[n] = { geo, mat };
    onProgress?.(++done / names.length, n);
  }));

  // ---------------------------------------------------------------- materials
  const mats = {};
  const matFor = (name) => {
    if (mats[name]) return mats[name];
    const base = assets[name].mat.clone();
    const o = {};
    if (name === 'seafloor_terrain') o.sand = true;
    if (name === 'kelp_strand') { o.sway = 0.45; o.swayH = 5.5; base.side = THREE.DoubleSide; }
    if (name === 'seagrass_clump') { o.sway = 0.08; o.swayH = 0.8; base.side = THREE.DoubleSide; }
    if (name === 'coral_seafan') { o.sway = 0.05; o.swayH = 1.6; base.side = THREE.DoubleSide; }
    if (name === 'anemone_glow') o.glow = true;
    if (name === 'fish_small') o.fish = true;
    if (name === 'ray_large') o.ray = true;
    return (mats[name] = patch(base, o));
  };

  // ---------------------------------------------------------------- terrain
  const tpos = assets.seafloor_terrain.geo.attributes.position;
  for (let i = 0; i < tpos.count; i++) tpos.setY(i, soft(tpos.getY(i)));
  assets.seafloor_terrain.geo.computeVertexNormals();
  const terrain = new THREE.Mesh(assets.seafloor_terrain.geo, matFor('seafloor_terrain'));
  terrain.name = 'seafloor';
  scene.add(terrain);

  // ---------------------------------------------------------------- placement
  const rand = rng(1234);
  const R = (a, b) => a + rand() * (b - a);
  const keepClear = [];
  const clearPath = (a, b, r) => { const n = Math.ceil(a.distanceTo(b) / r); for (let i = 0; i <= n; i++) keepClear.push([a.clone().lerp(b, i / n), r]); };
  const blocked = (p) => keepClear.some(([c, r]) => (p.x - c.x) ** 2 + (p.z - c.z) ** 2 < r * r);
  const ground = (p, off = 0) => new THREE.Vector3(p.x, heightAt(p.x, p.z) + off, p.z);

  const batches = {};
  const add = (name, pos, rot, scale) => {
    (batches[name] ||= []).push(new THREE.Matrix4().compose(pos, new THREE.Quaternion().setFromEuler(rot), new THREE.Vector3(scale, scale, scale)));
  };
  const scatter = (name, n, c, rmin, rmax, smin, smax, sink = 0) => {
    let placed = 0, tries = 0;
    while (placed < n && tries++ < n * 12) {
      const a = R(0, Math.PI * 2), r = Math.sqrt(R(rmin * rmin, rmax * rmax));
      const p = new THREE.Vector3(c.x + Math.cos(a) * r, 0, c.z + Math.sin(a) * r);
      if (blocked(p)) continue;
      const s = R(smin, smax);
      add(name, ground(p, sink * s), new THREE.Euler(R(-0.1, 0.1), R(0, Math.PI * 2), R(-0.1, 0.1)), s);
      keepClear.push([p, 0.5 * s]);
      placed++;
    }
  };
  const corals = manifest.scans.filter((s) => s.startsWith('scan_coral_'));
  const bommie = (c, radius) => {
    add('scan_boulder_flat', ground(c, -0.25), new THREE.Euler(0, R(0, 6.28), 0), radius * 0.45);
    const n = Math.round(radius * 10);
    for (let i = 0; i < n; i++) {
      const a = R(0, Math.PI * 2), d = Math.pow(R(0, 1), 0.8) * radius;
      const p = new THREE.Vector3(c.x + Math.cos(a) * d, 0, c.z + Math.sin(a) * d);
      if (blocked(p)) continue;
      add(corals[Math.floor(rand() * corals.length)], ground(p, R(-0.15, 0.2)), new THREE.Euler(R(-0.2, 0.2), R(0, 6.28), R(-0.2, 0.2)), R(1.0, 2.0));
    }
    keepClear.push([c, radius]);
  };

  const anchors = { hub: L(0, 0), reef: L(17, 0), exhibition: L(27, -20), wreck: L(38, 16), deep: L(62, 0) };
  clearPath(L(0, 0), L(30, 0), 2.6); clearPath(L(18, 0), L(29, -23), 2.4); clearPath(L(30, 0), L(40, 19), 2.4); clearPath(L(40, 19), L(66, 0), 2.6);
  Object.values(anchors).forEach((a) => keepClear.push([a, 3]));

  // interactables are their own meshes so they can glow on hover
  const interactables = new Map();
  const interactable = (id, name, at, scale, rot) => {
    const mat = matFor(name).clone();
    mat.onBeforeCompile = matFor(name).onBeforeCompile; mat.customProgramCacheKey = matFor(name).customProgramCacheKey;
    mat.emissive = new THREE.Color(0, 0, 0);
    const m = new THREE.Mesh(assets[name].geo, mat);
    m.position.copy(ground(at, 0));
    m.rotation.copy(rot || new THREE.Euler(0, R(0, 6.28), 0));
    m.scale.setScalar(scale);
    m.userData = { id, home: { p: m.position.clone(), q: m.quaternion.clone(), s: scale } };
    scene.add(m);
    interactables.set(id, m);
    keepClear.push([m.position, 1.6]);
    return m;
  };

  // area 1: the descent
  add('scan_rock_outcrop', ground(L(13, -5.5), -0.4), new THREE.Euler(0, Math.PI / 2, 0), 0.9);
  add('scan_rock_outcrop', ground(L(14, 5.5), -0.6), new THREE.Euler(0, -Math.PI / 2 + 0.4, 0), 0.75);
  keepClear.push([L(13, -5.5), 4], [L(14, 5.5), 3.5]);
  scatter('scan_boulder', 6, L(0, 0), 9, 16, 0.6, 1.3, -0.15);
  scatter('scan_boulder_flat', 4, L(0, 0), 8, 15, 0.7, 1.3, -0.1);
  scatter('scan_rock_coast', 3, L(-4, 0), 12, 18, 1.0, 1.6, -0.2);
  scatter('seagrass_clump', 34, L(2, 0), 3, 16, 0.8, 1.4, 0);
  scatter('scan_coral_brain_a', 3, L(6, 6), 0, 5, 0.7, 1.1, -0.1);
  for (let i = 0; i < 30; i++) {
    const p = L(8 + R(-4, 4), -12 + R(-4, 4));
    if (!blocked(p)) add('kelp_strand', ground(p, -0.05), new THREE.Euler(0, R(0, 6.28), 0), R(0.7, 1.3));
  }

  // area 2: the reef
  interactable('reef_brain_coral', 'scan_coral_brain_b', L(22, -3), 1.1);
  interactable('reef_sea_fan', 'coral_seafan', L(27, 5), 1.3);
  interactable('reef_conch', 'scan_conch', L(25, -1.5), 2.2);
  interactable('reef_staghorn', 'scan_coral_staghorn', L(25, 3.5), 1.0);
  for (const [f, r, rad] of [[19, -6, 3.4], [23, 6.5, 3.2], [31, -7, 3.6], [33, 6, 3.0], [27, -11, 2.4], [21, 11, 2.2], [36, 0, 2.6]]) bommie(L(f, r), rad);
  scatter('coral_seafan', 9, L(26, 0), 4, 12, 0.8, 1.3, -0.05);
  scatter('scan_boulder', 6, L(26, 0), 8, 14, 0.7, 1.4, -0.2);
  scatter('seagrass_clump', 22, L(26, 0), 2, 13, 0.7, 1.2, 0);

  const exhibits = [];
  const exhibitBeam = (m) => { exhibits.push(m); return m; };
  // area 3: exhibition clearing
  exhibitBeam(interactable('exhibit_light', 'info_marker', L(27.5, -21.5), 3.2));
  exhibitBeam(interactable('exhibit_pressure', 'info_marker', L(30.5, -26.5), 3.2));
  exhibitBeam(interactable('exhibit_currents', 'info_marker', L(33, -22), 3.2));
  scatter('scan_rock_coast', 5, L(30, -24), 6, 10, 1.0, 1.6, -0.2);
  scatter('scan_boulder_flat', 4, L(30, -24), 7, 11, 0.9, 1.4, -0.1);
  scatter('seagrass_clump', 10, L(30, -24), 4, 10, 0.7, 1.1, 0);

  // area 4: the wreck, on its side, bow a little down
  const ship = new THREE.Mesh(assets.scan_shipwreck.geo, matFor('scan_shipwreck'));
  const shipYaw = Math.atan2(L(1, 0.6).x, L(1, 0.6).z);
  ship.position.copy(ground(L(49, 24), -1.6));
  ship.rotation.set(0.07, shipYaw, -0.42, 'YXZ');
  scene.add(ship);
  keepClear.push([ship.position, 9]);
  for (let i = 0; i < 4; i++) add('scan_crate', ground(L(43 + R(-3, 4), 30 + R(-3, 3)), -0.15), new THREE.Euler(R(-0.15, 0.15), R(0, 6.28), R(-0.25, 0.25)), 1);
  for (let i = 0; i < 3; i++) add('scan_barrel', ground(L(44 + R(-3, 3), 17 + R(-2, 2)), 0.3), new THREE.Euler(Math.PI / 2, 0, R(0, 6.28)), 1);
  interactable('wreck_anchor', 'wreck_anchor', L(40.5, 24.5), 1.0, new THREE.Euler(1.25, 0.4, 0));
  interactable('wreck_compass', 'scan_compass', L(42, 18.5), 2.2);
  scatter('scan_boulder', 5, L(46, 22), 11, 15, 0.8, 1.4, -0.2);
  bommie(L(41, 28), 2.2);
  scatter('coral_seafan', 4, L(46, 22), 6, 11, 0.6, 1.0, -0.05);

  // area 5: the deep
  add('basalt_columns', ground(L(64, -7), -0.3), new THREE.Euler(0, 0.35, 0), 1.3);
  interactable('deep_columns', 'basalt_columns', L(73, 9), 1.6);
  interactable('deep_anemone', 'anemone_glow', L(67.5, 3), 1.8);
  scatter('anemone_glow', 16, L(70, 0), 3, 12, 0.8, 1.6, -0.05);
  scatter('tube_worms', 9, L(70, 0), 4, 12, 0.8, 1.4, -0.05);
  scatter('scan_rock_outcrop', 3, L(72, 0), 10, 18, 0.8, 1.2, -0.5);
  scatter('scan_rock_coast', 6, L(68, 0), 7, 15, 1.2, 2.0, -0.3);

  for (const [name, list] of Object.entries(batches)) {
    const im = new THREE.InstancedMesh(assets[name].geo, matFor(name), list.length);
    list.forEach((m, i) => im.setMatrixAt(i, m));
    im.computeBoundingSphere();
    im.name = name;
    scene.add(im);
  }

  // ---------------------------------------------------------------- destination markers
  const markers = new Map();
  const beamMat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: { uTime: U.uTime },
    vertexShader: 'varying vec2 vUv; varying float vE; void main(){ vUv=uv; vec4 mv=modelViewMatrix*vec4(position,1.); vE=abs(dot(normalize(normalMatrix*normal),normalize(-mv.xyz))); gl_Position=projectionMatrix*mv; }',
    fragmentShader: 'varying vec2 vUv; varying float vE; uniform float uTime; void main(){ float a=(1.-vUv.y)*(1.-vUv.y)*pow(vE,1.5)*(0.75+0.25*sin(uTime*2.2)); gl_FragColor=vec4(vec3(0.95,0.85,0.6)*a*0.55,1.); }',
  });
  const marker = (id, at) => {
    const g = new THREE.Group();
    g.position.copy(ground(at, 0.03));
    const ringMat = patch(new THREE.MeshStandardMaterial({ color: 0xe6d6a8, emissive: 0x8a7a4a, emissiveIntensity: 1 }));
    const ring = new THREE.Mesh(assets.destination_ring.geo, ringMat);
    ring.scale.setScalar(1.3);
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(0.9, 1.25, 5, 32, 1, true).translate(0, 2.5, 0), beamMat);
    g.add(ring, beam);
    g.visible = false;
    g.userData = { id, ring };
    scene.add(g);
    markers.set(id, g);
  };
  for (const m of exhibits) {
    const b = new THREE.Mesh(new THREE.CylinderGeometry(0.35, 0.55, 3.2, 24, 1, true).translate(0, 1.6, 0), beamMat);
    b.position.copy(m.position); scene.add(b);
  }
  marker('dest_exhibition', L(29, -23));
  marker('dest_wreck', L(40, 19));
  marker('dest_deep', L(66, 0));

  // ---------------------------------------------------------------- light
  const hemi = new THREE.HemisphereLight(0x6fb3ba, 0x1e2b27, 1.5);
  const sun = new THREE.DirectionalLight(0xd8f3f5, 2.4);
  sun.position.set(6, 30, 10);
  scene.add(hemi, sun);
  scene.fog = new THREE.FogExp2(0x0c4652, 0.03);
  scene.background = new THREE.Color(0x0c4652);

  // ---------------------------------------------------------------- god rays
  const shaftMat = new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
    uniforms: { uTime: U.uTime, uShaft: U.uShaft, uFogDensity: U.uFogDensity },
    vertexShader: `varying vec2 vUv; varying float vE; varying float vD; attribute float seed; varying float vSeed;
      void main(){ vUv=uv; vSeed=seed; vec4 mv=modelViewMatrix*instanceMatrix*vec4(position,1.); vD=-mv.z;
        vE=abs(dot(normalize(normalMatrix*mat3(instanceMatrix)*normal),normalize(-mv.xyz))); gl_Position=projectionMatrix*mv; }`,
    fragmentShader: `varying vec2 vUv; varying float vE; varying float vD; varying float vSeed; uniform float uTime, uShaft, uFogDensity;
      void main(){ float a = smoothstep(0.0,0.45,vUv.y)*smoothstep(1.0,0.75,vUv.y)*pow(vE,2.2);
        a *= 0.55 + 0.45*sin(uTime*0.35 + vSeed*6.0) * sin(uTime*0.21 + vSeed*3.0);
        a *= smoothstep(1.5, 6.0, vD) * exp(-pow(vD*uFogDensity*0.7, 2.0));
        gl_FragColor = vec4(vec3(0.74,0.93,0.92)*a*0.09*uShaft, 1.0); }`,
  });
  const shaftGeo = new THREE.CylinderGeometry(1.6, 0.5, 30, 20, 1, true).translate(0, -15, 0);
  const shaftCount = 22;
  shaftGeo.setAttribute('seed', new THREE.InstancedBufferAttribute(new Float32Array(shaftCount).map(() => Math.random()), 1));
  const shafts = new THREE.InstancedMesh(shaftGeo, shaftMat, shaftCount);
  for (let i = 0; i < shaftCount; i++) {
    const p = L(R(-6, 42), R(-16, 16)); p.y = 14;
    const m = new THREE.Matrix4().compose(p, new THREE.Quaternion().setFromEuler(new THREE.Euler(R(0.18, 0.32), R(-0.3, 0.3), R(-0.1, 0.1))), new THREE.Vector3(R(0.7, 1.6), R(0.8, 1.1), R(0.7, 1.6)));
    shafts.setMatrixAt(i, m);
  }
  shafts.frustumCulled = false;
  scene.add(shafts);

  // ---------------------------------------------------------------- water surface seen from below
  const surface = new THREE.Mesh(new THREE.PlaneGeometry(500, 500), new THREE.ShaderMaterial({
    side: THREE.DoubleSide, depthWrite: false,
    uniforms: { uTime: U.uTime, uCaustics: U.uCaustics, uFogColor: U.uFogColor, uFogDensity: U.uFogDensity, uShaft: U.uShaft },
    vertexShader: 'varying vec3 vW; void main(){ vec4 w=modelMatrix*vec4(position,1.); vW=w.xyz; gl_Position=projectionMatrix*viewMatrix*w; }',
    fragmentShader: `varying vec3 vW; uniform float uTime, uFogDensity; uniform sampler2D uCaustics; uniform vec3 uFogColor;
      uniform float uShaft;
      void main(){ float c = texture2D(uCaustics, vW.xz*0.035 + uTime*0.01).r * texture2D(uCaustics, vW.xz*0.05 - uTime*0.013).r;
        vec3 col = mix(vec3(0.35,0.75,0.78), vec3(0.85,1.0,0.98), smoothstep(0.35,0.85,c));
        float d = length(vW - cameraPosition);
        float f = 1.0 - exp(-pow(d*uFogDensity*0.75, 2.0));
        f = max(f, 1.0 - clamp(uShaft * 1.4, 0.0, 1.0));
        gl_FragColor = vec4(mix(col * mix(0.55, 1.0, uShaft), uFogColor, f), 1.0);
        #include <colorspace_fragment>
      }`,
  }));
  surface.rotation.x = Math.PI / 2;
  surface.position.y = 14;
  scene.add(surface);

  // ---------------------------------------------------------------- marine snow, wrapped around the camera
  const snowN = 2200;
  const snowGeo = new THREE.BufferGeometry();
  snowGeo.setAttribute('position', new THREE.BufferAttribute(new Float32Array(snowN * 3).map(() => Math.random()), 3));
  snowGeo.setAttribute('size', new THREE.BufferAttribute(new Float32Array(snowN).map(() => 0.4 + Math.random()), 1));
  const snow = new THREE.Points(snowGeo, new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: { uTime: U.uTime, uFogDensity: U.uFogDensity, uBox: { value: 22 } },
    vertexShader: `attribute float size; uniform float uTime, uBox, uFogDensity; varying float vA;
      void main(){ vec3 drift = vec3(uTime*0.05, -uTime*0.03, uTime*0.025) + vec3(sin(uTime*0.3+position.y*20.)*0.02);
        vec3 p = cameraPosition + (fract(position + drift/uBox - cameraPosition/uBox) - 0.5) * uBox;
        vec4 mv = viewMatrix * vec4(p,1.); gl_Position = projectionMatrix*mv;
        float d = -mv.z; gl_PointSize = size * 32.0 / max(d, 0.3);
        vA = smoothstep(0.3, 1.5, d) * exp(-pow(d*uFogDensity*1.2, 2.0)); }`,
    fragmentShader: 'varying float vA; void main(){ float r=length(gl_PointCoord-0.5); float a=smoothstep(0.5,0.05,r)*vA*0.4; gl_FragColor=vec4(vec3(0.85,0.95,0.93)*a,1.); }',
  }));
  snow.frustumCulled = false;
  scene.add(snow);

  // bubbles rising from the wreck
  const bubN = 160;
  const bubGeo = new THREE.BufferGeometry();
  const bp = new Float32Array(bubN * 3);
  const vents = [ground(L(47, 21), 1.2), ground(L(51, 26), 0.8), ground(L(68, 5), 0.2)];
  for (let i = 0; i < bubN; i++) { const v = vents[i % 3]; bp.set([v.x + (Math.random() - 0.5) * 0.4, Math.random(), v.z + (Math.random() - 0.5) * 0.4], i * 3); }
  bubGeo.setAttribute('position', new THREE.BufferAttribute(bp, 3));
  const baseYs = new Float32Array(bubN).map((_, i) => vents[i % 3].y);
  bubGeo.setAttribute('baseY', new THREE.BufferAttribute(baseYs, 1));
  const bubbles = new THREE.Points(bubGeo, new THREE.ShaderMaterial({
    transparent: true, depthWrite: false, blending: THREE.AdditiveBlending,
    uniforms: { uTime: U.uTime, uFogDensity: U.uFogDensity },
    vertexShader: `attribute float baseY; uniform float uTime, uFogDensity; varying float vA;
      void main(){ float h = fract(position.y + uTime*0.07) * 16.0; vec3 p = vec3(position.x + sin(h*2.0+position.y*30.)*0.15*min(h,1.5), baseY + h, position.z + cos(h*1.7)*0.12*min(h,1.5));
        vec4 mv = viewMatrix*vec4(p,1.); gl_Position = projectionMatrix*mv; gl_PointSize = (2.0 + fract(position.y*13.)*3.0) * 30.0 / max(-mv.z,0.5);
        vA = smoothstep(0.0,0.5,h)*smoothstep(16.0,12.0,h)*exp(-(-mv.z)*uFogDensity); }`,
    fragmentShader: 'varying float vA; void main(){ float r=length(gl_PointCoord-0.5); float a=(smoothstep(0.5,0.38,r)-smoothstep(0.38,0.2,r)*0.7)*vA; gl_FragColor=vec4(vec3(0.85,0.97,1.)*a,1.); }',
  }));
  bubbles.frustumCulled = false;
  scene.add(bubbles);

  // ---------------------------------------------------------------- fish
  const fishGeo = assets.fish_small.geo.clone().rotateY(Math.PI);
  const schools = [
    { c: ground(L(6, 6), 3.2), n: 40, area: new THREE.Vector3(10, 3, 10), speed: 0.8 },
    { c: ground(L(26, 0), 2.6), n: 60, area: new THREE.Vector3(12, 3, 10), speed: 0.9 },
    { c: ground(L(46, 22), 2.5), n: 34, area: new THREE.Vector3(9, 2.5, 9), speed: 0.6 },
  ].map((s, k) => {
    const im = new THREE.InstancedMesh(fishGeo, matFor('fish_small'), s.n);
    im.frustumCulled = false;
    scene.add(im);
    const fish = Array.from({ length: s.n }, () => ({
      off: new THREE.Vector3(R(-1, 1), R(-0.5, 0.5), R(-1, 1)).multiplyScalar(1.4),
      pos: s.c.clone(), vel: new THREE.Vector3(), scale: R(0.8, 1.25), phase: R(0, 10),
    }));
    return { ...s, im, fish, seed: k * 17.3 };
  });
  const ray = new THREE.Mesh(assets.ray_large.geo.clone().rotateY(Math.PI), matFor('ray_large'));
  scene.add(ray);

  const tmpM = new THREE.Matrix4(), tmpQ = new THREE.Quaternion(), tmpS = new THREE.Vector3(), up = new THREE.Vector3(0, 1, 0);
  const look = new THREE.Matrix4();
  const noise = (a, b) => Math.sin(a * 1.7 + b) * 0.5 + Math.sin(a * 0.63 + b * 2.1) * 0.35 + Math.sin(a * 2.9 + b * 0.4) * 0.15;
  function updateFish(dt, t, cam, reduced) {
    for (const s of schools) {
      const sp = reduced ? s.speed * 0.5 : s.speed;
      const tt = t * sp * 0.15;
      const leader = new THREE.Vector3(s.c.x + noise(tt, s.seed) * s.area.x * 0.5, s.c.y + noise(tt * 0.8, s.seed + 4) * s.area.y * 0.5, s.c.z + noise(tt, s.seed + 9) * s.area.z * 0.5);
      s.fish.forEach((f, i) => {
        const goal = leader.clone().add(f.off).add(new THREE.Vector3(Math.sin(t * 1.3 * sp + f.phase), Math.sin(t * 0.9 * sp + f.phase * 2) * 0.4, Math.cos(t * 1.1 * sp + f.phase)).multiplyScalar(0.35));
        const away = f.pos.clone().sub(cam); const d = away.length();
        if (d < 1.4 && d > 0.001) goal.add(away.multiplyScalar((1.4 - d) * 3 / d));
        const floor = heightAt(f.pos.x, f.pos.z) + 0.5; if (goal.y < floor) goal.y = floor;
        f.vel.lerp(goal.sub(f.pos).multiplyScalar(1.2), Math.min(1, dt * 1.5));
        const max = sp * 1.7; if (f.vel.length() > max) f.vel.setLength(max);
        f.pos.addScaledVector(f.vel, dt);
        const dir = f.vel.lengthSq() > 1e-4 ? f.vel : new THREE.Vector3(0, 0, 1);
        look.lookAt(new THREE.Vector3(), dir.clone().negate(), up);
        tmpQ.setFromRotationMatrix(look);
        s.im.setMatrixAt(i, tmpM.compose(f.pos, tmpQ, tmpS.setScalar(f.scale)));
      });
      s.im.instanceMatrix.needsUpdate = true;
    }
    // the ray circles slowly at the edge of the deep
    const a = t * (reduced ? 0.02 : 0.04);
    const c = L(70, 0);
    const p = new THREE.Vector3(c.x + Math.cos(a) * 14, 0, c.z + Math.sin(a) * 14);
    p.y = heightAt(p.x, p.z) + 4.5 + Math.sin(t * 0.2) * 0.6;
    const dir = new THREE.Vector3(-Math.sin(a), 0, Math.cos(a));
    ray.position.copy(p);
    ray.quaternion.setFromRotationMatrix(look.lookAt(new THREE.Vector3(), dir.negate(), up));
    ray.rotateZ(Math.sin(t * 0.3) * 0.15);
  }

  // ---------------------------------------------------------------- area looks, blended by where the camera is
  const P = {
    descent: { fog: 0x0f5260, d: 0.043, sky: 0x74bcc2, gnd: 0x23322c, hi: 1.55, sun: 2.5, c: 1.0, shaft: 1 },
    reef: { fog: 0x0e5562, d: 0.046, sky: 0x7cc2c0, gnd: 0x27342c, hi: 1.6, sun: 2.6, c: 1.0, shaft: 1 },
    exhibition: { fog: 0x0b4652, d: 0.047, sky: 0x66a9b4, gnd: 0x1c2a28, hi: 1.35, sun: 2.1, c: 0.8, shaft: 0.7 },
    wreck: { fog: 0x093a46, d: 0.052, sky: 0x4f8f9c, gnd: 0x16221f, hi: 1.15, sun: 1.6, c: 0.55, shaft: 0.4 },
    deep: { fog: 0x03141c, d: 0.062, sky: 0x2a5a6c, gnd: 0x0a1012, hi: 0.75, sun: 0.55, c: 0.0, shaft: 0 },
  };
  const centres = { descent: L(0, 0), reef: L(26, 0), exhibition: L(30, -24), wreck: L(46, 22), deep: L(74, 0) };
  const cur = { fog: new THREE.Color(0x0f5260), sky: new THREE.Color(0x74bcc2), gnd: new THREE.Color(0x23322c), d: 0.043, hi: 1.55, sun: 2.5, c: 1, shaft: 1 };
  let currentArea = 'descent';
  function updateLook(dt, cam) {
    let tot = 0; const acc = { fog: new THREE.Color(0, 0, 0), sky: new THREE.Color(0, 0, 0), gnd: new THREE.Color(0, 0, 0), d: 0, hi: 0, sun: 0, c: 0, shaft: 0 };
    let best = 0;
    for (const [k, c] of Object.entries(centres)) {
      const dd = Math.hypot(cam.x - c.x, cam.z - c.z);
      let w = Math.exp(-(dd * dd) / (2 * 13 * 13));
      if (k === 'deep') w += Math.max(0, (-cam.z - 56) / 10) * 1.5;
      if (w > best) { best = w; currentArea = k; }
      const p = P[k];
      acc.fog.add(new THREE.Color(p.fog).multiplyScalar(w)); acc.sky.add(new THREE.Color(p.sky).multiplyScalar(w)); acc.gnd.add(new THREE.Color(p.gnd).multiplyScalar(w));
      acc.d += p.d * w; acc.hi += p.hi * w; acc.sun += p.sun * w; acc.c += p.c * w; acc.shaft += p.shaft * w; tot += w;
    }
    if (tot < 1e-4) return;
    const k = 1 - Math.exp(-dt * 1.8);
    cur.fog.lerp(acc.fog.multiplyScalar(1 / tot), k); cur.sky.lerp(acc.sky.multiplyScalar(1 / tot), k); cur.gnd.lerp(acc.gnd.multiplyScalar(1 / tot), k);
    for (const key of ['d', 'hi', 'sun', 'c', 'shaft']) cur[key] += (acc[key] / tot - cur[key]) * k;
    scene.fog.color.copy(cur.fog); scene.fog.density = cur.d; scene.background.copy(cur.fog);
    hemi.color.copy(cur.sky); hemi.groundColor.copy(cur.gnd); hemi.intensity = Math.max(cur.hi, 0.6);
    sun.intensity = cur.sun;
    U.uCausticStrength.value = cur.c; U.uShaft.value = cur.shaft;
    U.uFogColor.value.copy(cur.fog); U.uFogDensity.value = cur.d;
  }

  function update(dt, t, cam, reduced) {
    U.uTime.value = reduced ? t * 0.4 : t;
    updateLook(dt, cam);
    updateFish(dt, t, cam, reduced);
    for (const g of markers.values()) if (g.visible) g.userData.ring.scale.setScalar(1.3 * (1 + 0.06 * Math.sin(t * 2.2)));
  }

  return {
    scene, terrain, interactables, markers, anchors, heightAt, update,
    get area() { return currentArea; },
    setLight(light) { shafts.visible = !light; bubbles.visible = !light; snow.material.uniforms.uBox.value = light ? 16 : 22; },
  };
}
