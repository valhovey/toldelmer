const THREE_URL = "https://esm.sh/three@0.170.0";
const ORBIT_URL = "https://esm.sh/three@0.170.0/examples/jsm/controls/OrbitControls.js";
const C = {
  brass: "#d8a657", parchment: "#e6d3a3", ember: "#c9774a", moss: "#8a9a5b", verdigris: "#6f9e8c",
  cavern: "#3f7f86", rust: "#b5654a", link: "#7f8fc0", bronze: "#a7865a",
};
const num = (v) => (v === null || v === undefined || v === "" || isNaN(Number(v)) ? null : Number(v));
const DEG = Math.PI / 180;

class NinaMountCard extends HTMLElement {
  setConfig(config) {
    if (!config.entity) throw new Error("entity (the NINA pointing sensor) is required");
    this._config = { height: 248, latitude: 45, ...config };
  }
  getCardSize() { return 6; }
  getGridOptions() { return { columns: 12, rows: 4, min_rows: 3 }; }

  set hass(hass) {
    const st = hass.states[this._config.entity];
    const key = st ? st.state + JSON.stringify(st.attributes) : "missing";
    if (key === this._lastKey) return;
    this._lastKey = key;
    this._state = st;
    if (!this._built) this._build();
    else this._update();
  }

  async _build() {
    this._built = true;
    const root = this.attachShadow({ mode: "open" });
    root.innerHTML = `
      <style>
        :host { display: block; }
        ha-card { display: block; position: relative; overflow: hidden; height: ${this._config.height}px; }
        #gl { position: absolute; inset: 0; }
        .info { position: absolute; left: 12px; top: 8px; font-size: 12px; line-height: 1.45;
                color: var(--primary-text-color); pointer-events: none; text-shadow: 0 1px 2px rgba(0,0,0,.6); }
        .info b { color: ${C.brass}; font-weight: 500; }
        .off { position: absolute; inset: 0; display: none; align-items: center; justify-content: center;
               color: var(--secondary-text-color); font-size: 15px; pointer-events: none; }
        .off.show { display: flex; }
      </style>
      <ha-card><div id="gl"></div><div class="info"></div><div class="off">Mount disconnected</div></ha-card>`;
    try {
      const [THREE, { OrbitControls }] = await Promise.all([import(THREE_URL), import(ORBIT_URL)]);
      this._three = THREE;
      this._initScene(THREE, OrbitControls);
    } catch (e) {
      root.querySelector(".off").textContent = "3D model unavailable: " + e.message;
      root.querySelector(".off").classList.add("show");
      return;
    }
    this._update();
  }

  _initScene(THREE, OrbitControls) {
    const host = this.shadowRoot.getElementById("gl");
    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    host.appendChild(renderer.domElement);
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(38, 1, 0.05, 50);
    camera.position.set(3.1, 2.3, 3.7);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 1.05, 0);
    controls.enableDamping = true;
    controls.maxPolarAngle = Math.PI * 0.49;
    controls.enableZoom = false;
    controls.enablePan = false;
    renderer.domElement.style.touchAction = "pan-y";
    controls.update();

    scene.add(new THREE.HemisphereLight(0xdfe6ff, 0x20242c, 1.1));
    const sun = new THREE.DirectionalLight(0xffffff, 1.6);
    sun.position.set(3, 5, 2);
    scene.add(sun);

    const cv = document.createElement("canvas");
    cv.width = cv.height = 1024;
    const g = cv.getContext("2d");
    const R = 512;
    g.translate(R, R);
    g.strokeStyle = "rgba(230,211,163,0.35)";
    g.lineWidth = 3;
    [470, 380].forEach((r) => { g.beginPath(); g.arc(0, 0, r, 0, 2 * Math.PI); g.stroke(); });
    for (let a = 0; a < 360; a += 5) {
      const r0 = a % 45 === 0 ? 380 : a % 15 === 0 ? 430 : 450;
      g.beginPath();
      g.moveTo(r0 * Math.sin(a * DEG), -r0 * Math.cos(a * DEG));
      g.lineTo(470 * Math.sin(a * DEG), -470 * Math.cos(a * DEG));
      g.stroke();
    }
    const star = (a, len, w, fill) => {
      g.save(); g.rotate(a * DEG); g.fillStyle = fill;
      g.beginPath(); g.moveTo(0, -len); g.lineTo(w, 0); g.lineTo(0, len * 0.08); g.lineTo(-w, 0); g.closePath(); g.fill();
      g.restore();
    };
    [45, 135, 225, 315].forEach((a) => star(a, 260, 26, "rgba(167,134,90,0.55)"));
    [0, 90, 180, 270].forEach((a) => star(a, 370, 38, a === 0 ? "rgba(181,101,74,0.85)" : "rgba(230,211,163,0.6)"));
    g.fillStyle = C.parchment;
    g.font = "600 64px Georgia, serif";
    g.textAlign = "center";
    g.textBaseline = "middle";
    [["N", 0], ["E", 90], ["S", 180], ["W", 270]].forEach(([t, a]) => g.fillText(t, 500 * 0.86 * Math.sin(a * DEG), -500 * 0.86 * Math.cos(a * DEG)));
    g.font = "500 36px Georgia, serif";
    [["NE", 45], ["SE", 135], ["SW", 225], ["NW", 315]].forEach(([t, a]) => g.fillText(t, 300 * Math.sin(a * DEG), -300 * Math.cos(a * DEG)));
    const tex = new THREE.CanvasTexture(cv);
    tex.colorSpace = THREE.SRGBColorSpace;
    const rose = new THREE.Mesh(new THREE.PlaneGeometry(2.6, 2.6), new THREE.MeshBasicMaterial({ map: tex, transparent: true }));
    rose.rotation.x = -Math.PI / 2;
    rose.position.y = 0.001;
    scene.add(rose);
    const grid = new THREE.GridHelper(8, 8, 0x3a3f4a, 0x2a2e36);
    scene.add(grid);

    const mat = (c, rough = 0.6, metal = 0.2) => new THREE.MeshStandardMaterial({ color: c, roughness: rough, metalness: metal });
    const dark = mat(0x1d1f24, 0.7, 0.3), steel = mat(0xb9bec6, 0.35, 0.8);
    const cyl = (r, len, m, seg = 32) => new THREE.Mesh(new THREE.CylinderGeometry(r, r, len, seg), m);

    const pier = cyl(0.06, 1.05, dark);
    pier.position.y = 0.525;
    scene.add(pier);
    const base = cyl(0.16, 0.04, dark);
    base.position.y = 0.02;
    scene.add(base);

    this._parts = {
      polar: cyl(0.075, 0.32, dark),
      decHousing: cyl(0.065, 0.24, dark),
      shaft: cyl(0.015, 0.62, steel, 16),
      weight1: cyl(0.085, 0.07, dark),
      weight2: cyl(0.085, 0.07, dark),
      ota: cyl(0.14, 0.95, mat(C.rust, 0.45, 0.15), 48),
      dew: cyl(0.145, 0.22, mat(0x7a3f2e, 0.5, 0.1), 48),
      saddle: new THREE.Mesh(new THREE.BoxGeometry(0.06, 0.3, 0.06), dark),
    };
    Object.values(this._parts).forEach((m) => scene.add(m));
    const rayGeo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3(0, 1, 0)]);
    this._ray = new THREE.Line(rayGeo, new THREE.LineBasicMaterial({ color: C.link, transparent: true, opacity: 0.7 }));
    scene.add(this._ray);

    this._renderer = renderer; this._scene = scene; this._camera = camera; this._controls = controls;
    const resize = () => {
      const w = host.clientWidth || 400, h = host.clientHeight || this._config.height;
      renderer.setSize(w, h, false);
      renderer.domElement.style.width = "100%";
      renderer.domElement.style.height = "100%";
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      this._render();
    };
    new ResizeObserver(resize).observe(host);
    controls.addEventListener("change", () => this._render());
    controls.addEventListener("end", () => { let n = 30; const tick = () => { controls.update(); this._render(); if (--n > 0) requestAnimationFrame(tick); }; tick(); });
    resize();
  }

  _render() { if (this._renderer) this._renderer.render(this._scene, this._camera); }

  _along(mesh, center, dir) {
    const T = this._three;
    mesh.position.copy(center);
    mesh.quaternion.setFromUnitVectors(new T.Vector3(0, 1, 0), dir.clone().normalize());
  }

  _update() {
    const T = this._three;
    if (!T || !this._parts) return;
    const st = this._state;
    const a = (st && st.attributes) || {};
    const connected = st && !["unavailable", "unknown", "Disconnected"].includes(st.state) && num(a.alt_deg) !== null;
    this.shadowRoot.querySelector(".off").classList.toggle("show", !connected);

    const lat = num(a.latitude) ?? this._config.latitude;
    const phi = Math.abs(lat) * DEG, south = lat < 0;
    const p = new T.Vector3(0, Math.sin(phi), south ? Math.cos(phi) : -Math.cos(phi));
    let d;
    if (connected) {
      const alt = num(a.alt_deg) * DEG, az = num(a.az_deg) * DEG;
      d = new T.Vector3(Math.sin(az) * Math.cos(alt), Math.sin(alt), -Math.cos(az) * Math.cos(alt));
    } else {
      d = p.clone();
    }
    let u = new T.Vector3().crossVectors(p, d);
    const haH = num(a.hour_angle_hours);
    if (u.lengthSq() < 0.01 && haH !== null) {
      const H = (haH - 6) * 15 * DEG, sl = Math.sin(lat * DEG), cl = Math.cos(lat * DEG);
      u.set(-Math.sin(H), cl * Math.cos(H), sl * Math.cos(H));
      if (south) u.multiplyScalar(-1);
    } else if (u.lengthSq() < 1e-6) u.set(1, 0, 0);
    u.normalize();
    const pier = a.pier_side;
    let s = pier === "East" ? 1 : pier === "West" ? -1 : 0;
    if (s === 0) s = u.y >= 0 ? 1 : -1;
    u.multiplyScalar(s);

    const head = new T.Vector3(0, 1.07, 0);
    const P = this._parts;
    this._along(P.polar, head.clone().addScaledVector(p, 0.08), p);
    const decC = head.clone().addScaledVector(p, 0.24);
    this._along(P.decHousing, decC.clone().addScaledVector(u, 0.06), u);
    this._along(P.shaft, decC.clone().addScaledVector(u, -0.31), u);
    this._along(P.weight1, decC.clone().addScaledVector(u, -0.47), u);
    this._along(P.weight2, decC.clone().addScaledVector(u, -0.56), u);
    const saddle = decC.clone().addScaledVector(u, 0.2);
    this._along(P.saddle, saddle, d);
    const ota = saddle.clone().addScaledVector(u, 0.17).addScaledVector(d, 0.12);
    this._along(P.ota, ota, d);
    this._along(P.dew, ota.clone().addScaledVector(d, 0.57), d);

    const tip = ota.clone().addScaledVector(d, 0.68);
    this._ray.geometry.setFromPoints([tip, tip.clone().addScaledVector(d, 3.5)]);
    this._ray.visible = !!connected && !a.at_park;

    const info = this.shadowRoot.querySelector(".info");
    if (connected) {
      const f = (v, n = 1) => (num(v) === null ? "—" : num(v).toFixed(n));
      const flip = num(a.hours_to_flip);
      const flipMin = flip === null || !a.tracking ? null : Math.round(flip * 60);
      const ha = num(a.hour_angle_hours);
      const flipPause = !a.tracking && !a.at_park && !a.slewing && ha !== null && Math.abs(ha) < 1;
      const flipTxt = flipMin !== null ? `${Math.floor(flipMin / 60)}h ${String(flipMin % 60).padStart(2, "0")}m`
        : flipPause ? "paused for flip" : "not tracking";
      info.innerHTML =
        `<b>Alt</b> ${f(a.alt_deg)}° &nbsp; <b>Az</b> ${f(a.az_deg)}°<br>` +
        `<b>RA</b> ${a.ra ?? "—"} &nbsp; <b>Dec</b> ${a.dec ?? "—"}<br>` +
        `<b>Pier</b> ${pier ?? "—"} &nbsp; <b>HA</b> ${f(a.hour_angle_hours, 2)}h &nbsp; <b>Flip in</b> ${flipTxt}` +
        (a.at_park ? "<br><b>Parked</b>" : a.slewing ? "<br><b>Slewing</b>" : "");
    } else {
      info.innerHTML = "";
    }
    this._render();
  }
}

const NUM = "(-?\\d+(?:\\.\\d+)?(?:[eE][-+]?\\d+)?)";

function fitFunction(kind, fittings) {
  if (!fittings) return null;
  const k = (kind || "").toUpperCase();
  if (k.includes("HYPERBOLIC") && fittings.Hyperbolic) {
    const m = fittings.Hyperbolic.match(new RegExp(`y = ${NUM} \\* cosh\\(asinh\\(\\(${NUM} - x\\) / ${NUM}\\)\\)`));
    if (m) { const [A, B, P] = m.slice(1).map(Number); return (x) => A * Math.sqrt(1 + ((B - x) / P) ** 2); }
  }
  if (k.includes("PARABOLIC") && fittings.Quadratic) {
    const m = fittings.Quadratic.match(new RegExp(`y = ${NUM} \\* x\\^2 \\+ ${NUM} \\* x \\+ ${NUM}`));
    if (m) { const [A, B, Q] = m.slice(1).map(Number); return (x) => A * x * x + B * x + Q; }
  }
  return null;
}

function trendLines(kind, fittings) {
  if (!fittings || !(kind || "").toUpperCase().includes("TREND")) return [];
  return [fittings.LeftTrend, fittings.RightTrend].map((s) => {
    const m = s && s.match(new RegExp(`y = ${NUM} \\* x \\+ ${NUM}`));
    return m ? ((a, b) => (x) => a * x + b)(Number(m[1]), Number(m[2])) : null;
  }).filter(Boolean);
}

class NinaAutofocusCard extends HTMLElement {
  setConfig(config) {
    if (!config.entity) throw new Error("entity (the NINA autofocus sensor) is required");
    this._config = { title: "Last autofocus", ...config };
  }
  getCardSize() { return 5; }
  getGridOptions() { return { columns: 12, rows: 5, min_rows: 4 }; }

  set hass(hass) {
    const st = hass.states[this._config.entity];
    const key = st ? st.last_updated + st.state : "missing";
    if (key === this._lastKey) return;
    this._lastKey = key;
    if (!this.shadowRoot) this.attachShadow({ mode: "open" });
    this.shadowRoot.innerHTML = this._html(st, hass);
  }

  _html(st, hass) {
    const a = (st && st.attributes) || {};
    const pts = (a.points || []).filter((p) => num(p[0]) !== null && num(p[1]) !== null);
    const W = 420, H = 236, L = 44, Rp = 12, T = 12, B = 38;
    let svg = "";
    if (pts.length) {
      const xs = pts.map((p) => p[0]), ys = pts.map((p) => p[1] + (p[2] || 0));
      let x0 = Math.min(...xs), x1 = Math.max(...xs);
      const pad = (x1 - x0) * 0.06 || 10; x0 -= pad; x1 += pad;
      const y1 = Math.max(...ys) * 1.12, y0 = 0;
      const X = (x) => L + ((x - x0) / (x1 - x0)) * (W - L - Rp);
      const Y = (y) => T + (1 - (y - y0) / (y1 - y0)) * (H - T - B);
      const ticks = (lo, hi, n) => { const st = (hi - lo) / n; return Array.from({ length: n + 1 }, (_, i) => lo + i * st); };
      ticks(y0, y1, 4).forEach((y) => {
        svg += `<line x1="${L}" x2="${W - Rp}" y1="${Y(y)}" y2="${Y(y)}" class="grid"/>` +
               `<text x="${L - 6}" y="${Y(y) + 4}" text-anchor="end">${y.toFixed(1)}</text>`;
      });
      ticks(x0 + pad, x1 - pad, 4).forEach((x) => {
        svg += `<text x="${X(x)}" y="${H - B + 15}" text-anchor="middle">${Math.round(x)}</text>`;
      });
      const fit = fitFunction(a.fitting, a.fittings);
      if (fit) {
        let d = "";
        for (let i = 0; i <= 120; i++) {
          const x = x0 + ((x1 - x0) * i) / 120, y = fit(x);
          if (isFinite(y) && y <= y1 * 1.3) d += `${d ? "L" : "M"}${X(x).toFixed(1)},${Y(Math.min(y, y1)).toFixed(1)}`;
        }
        svg += `<path d="${d}" class="fit"/>`;
      }
      trendLines(a.fitting, a.fittings).forEach((f) => {
        svg += `<line x1="${X(x0)}" y1="${Y(f(x0))}" x2="${X(x1)}" y2="${Y(f(x1))}" class="trend"/>`;
      });
      pts.forEach(([x, y, e]) => {
        if (e) svg += `<line x1="${X(x)}" x2="${X(x)}" y1="${Y(y - e)}" y2="${Y(y + e)}" class="err"/>`;
        svg += `<circle cx="${X(x)}" cy="${Y(y)}" r="4" class="pt"/>`;
      });
      const pos = num(a.position), hfr = num(a.hfr);
      if (pos !== null && pos >= x0 && pos <= x1) {
        svg += `<line x1="${X(pos)}" x2="${X(pos)}" y1="${T}" y2="${H - B}" class="best"/>`;
        if (hfr !== null) svg += `<circle cx="${X(pos)}" cy="${Y(hfr)}" r="5.5" class="bestpt"/>`;
      }
      svg += `<text x="${(L + W - Rp) / 2}" y="${H - 3}" text-anchor="middle" class="axis">focuser position</text>` +
             `<text x="12" y="${(T + H - B) / 2}" text-anchor="middle" class="axis" transform="rotate(-90 12 ${(T + H - B) / 2})">HFR</text>`;
    }
    const running = a.status === "running";
    const r2 = a.r_squared && a.fitting ? (() => {
      const k = a.fitting.toUpperCase();
      const v = k.includes("HYPERBOLIC") ? a.r_squared.Hyperbolic : k.includes("PARABOLIC") ? a.r_squared.Quadratic : null;
      return num(v) !== null ? `R² ${Number(v).toFixed(3)}` : null;
    })() : null;
    const when = a.finished_at ? new Date(a.finished_at) : null;
    const meta = [
      running ? `<span class="live">Running · ${pts.length} points</span>` : null,
      num(a.position) !== null ? `<b>${Math.round(a.position)}</b> steps` : null,
      num(a.hfr) !== null ? `HFR <b>${Number(a.hfr).toFixed(2)}</b>` : null,
      a.filter ? `filter <b>${a.filter}</b>` : null,
      num(a.temperature) !== null ? `${Number(a.temperature).toFixed(1)} °C` : null,
      r2,
      when && !isNaN(when) && !running ? when.toLocaleString([], { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : null,
    ].filter(Boolean).join(" · ");
    return `
      <style>
        :host { display: block; }
        ha-card { display: block; padding: 12px 14px 8px; }
        .title { font-size: 15px; font-weight: 500; color: var(--primary-text-color); }
        .meta { font-size: 12.5px; color: var(--secondary-text-color); margin: 2px 0 6px; }
        .meta b { color: ${C.brass}; font-weight: 500; }
        .live { color: ${C.ember}; }
        svg { width: 100%; height: auto; display: block; }
        text { fill: var(--secondary-text-color); font-size: 11px; }
        .axis { font-size: 10.5px; opacity: .8; }
        .grid { stroke: var(--divider-color); stroke-width: 1; }
        .fit { fill: none; stroke: ${C.verdigris}; stroke-width: 2; }
        .trend { stroke: ${C.bronze}; stroke-width: 1.2; stroke-dasharray: 4 3; }
        .err { stroke: ${C.parchment}; stroke-opacity: .5; stroke-width: 1.2; }
        .pt { fill: ${C.parchment}; }
        .best { stroke: ${C.brass}; stroke-width: 1; stroke-dasharray: 3 3; }
        .bestpt { fill: ${C.brass}; stroke: #000; stroke-opacity: .4; }
        .empty { color: var(--secondary-text-color); padding: 40px 0; text-align: center; }
      </style>
      <ha-card>
        <div class="title">${this._config.title}</div>
        <div class="meta">${meta || "&nbsp;"}</div>
        ${pts.length ? `<svg viewBox="0 0 ${W} ${H}">${svg}</svg>` : `<div class="empty">No autofocus data yet</div>`}
      </ha-card>`;
  }
}

function niceStep(span, target) {
  const raw = span / target, mag = 10 ** Math.floor(Math.log10(raw)), f = raw / mag;
  return (f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10) * mag;
}

function monotonePath(pts) {
  const n = pts.length;
  if (n < 2) return n ? `M${pts[0][0]},${pts[0][1]}` : "";
  const dx = [], m = [], t = [];
  for (let i = 0; i < n - 1; i++) { dx[i] = pts[i + 1][0] - pts[i][0]; m[i] = dx[i] ? (pts[i + 1][1] - pts[i][1]) / dx[i] : 0; }
  t[0] = m[0]; t[n - 1] = m[n - 2];
  for (let i = 1; i < n - 1; i++) t[i] = m[i - 1] * m[i] <= 0 ? 0 : (m[i - 1] + m[i]) / 2;
  for (let i = 0; i < n - 1; i++) {
    if (m[i] === 0) { t[i] = t[i + 1] = 0; continue; }
    const a = t[i] / m[i], b = t[i + 1] / m[i], h = a * a + b * b;
    if (h > 9) { const k = 3 / Math.sqrt(h); t[i] = k * a * m[i]; t[i + 1] = k * b * m[i]; }
  }
  let d = `M${pts[0][0].toFixed(1)},${pts[0][1].toFixed(1)}`;
  for (let i = 0; i < n - 1; i++) {
    const h = dx[i] / 3;
    d += `C${(pts[i][0] + h).toFixed(1)},${(pts[i][1] + h * t[i]).toFixed(1)} ${(pts[i + 1][0] - h).toFixed(1)},${(pts[i + 1][1] - h * t[i + 1]).toFixed(1)} ${pts[i + 1][0].toFixed(1)},${pts[i + 1][1].toFixed(1)}`;
  }
  return d;
}

class NinaHistoryCard extends HTMLElement {
  setConfig(config) {
    if (!Array.isArray(config.entities) || !config.entities.length) throw new Error("entities is required");
    this._config = { hours: 1, height: 120, decimals: 1, zero: false, ...config };
    this._data = {};
    this._seen = {};
  }
  getCardSize() { return 3; }
  getGridOptions() { return { columns: 12, rows: "auto" }; }

  connectedCallback() {
    this._timer = setInterval(() => this._schedule(), this._windowMs() <= 3600e3 ? 2000 : 30000);
    this._refetch = setInterval(() => { this._fetched = false; this._fetch(); }, 600e3);
  }
  disconnectedCallback() { clearInterval(this._timer); clearInterval(this._refetch); }

  _windowMs() { return this._config.hours * 3600e3; }

  set hass(hass) {
    this._hass = hass;
    if (!this.shadowRoot) this._build();
    if (!this._fetched) this._fetch();
    for (const { entity } of this._config.entities) {
      const st = hass.states[entity];
      if (!st || st.last_updated === this._seen[entity]) continue;
      this._seen[entity] = st.last_updated;
      if (this._fetched) (this._data[entity] ||= []).push([Date.parse(st.last_updated), num(st.state) ?? NaN]);
    }
    this._schedule();
  }

  async _fetch() {
    if (!this._hass || this._fetching) return;
    this._fetching = true;
    try {
      const ids = this._config.entities.map((e) => e.entity);
      const res = await this._hass.callWS({
        type: "history/history_during_period", start_time: new Date(Date.now() - this._windowMs()).toISOString(),
        entity_ids: ids, minimal_response: true, no_attributes: true, significant_changes_only: false,
      });
      for (const id of ids) {
        this._data[id] = (res[id] || []).map((r) => [1000 * (r.lu ?? r.lc), num(r.s) ?? NaN]);
        const st = this._hass.states[id];
        if (st) this._seen[id] = st.last_updated;
      }
      this._fetched = true;
    } catch (e) {
      console.warn("nina-history-card fetch failed", e);
    } finally {
      this._fetching = false;
      this._schedule();
    }
  }

  _build() {
    this.attachShadow({ mode: "open" }).innerHTML = `
      <style>
        :host { display: block; }
        ha-card { display: block; padding: 10px 12px 6px; }
        .head { display: flex; align-items: baseline; gap: 10px; }
        .title { font-size: 14px; font-weight: 500; color: var(--primary-text-color); flex: 1; white-space: nowrap; }
        .vals { font-size: 13px; display: flex; gap: 12px; }
        .vals span { white-space: nowrap; }
        .vals small { color: var(--secondary-text-color); font-size: 11px; margin-right: 3px; }
        svg { display: block; width: 100%; overflow: visible; }
        text { fill: var(--secondary-text-color); font-size: 10.5px; font-family: inherit; }
        .grid { stroke: var(--divider-color); stroke-dasharray: 3 4; }
        .zero { stroke: var(--secondary-text-color); stroke-opacity: .55; }
        .frame { stroke: var(--divider-color); fill: none; }
        .line { fill: none; stroke-width: 1.8; stroke-linejoin: round; }
        .sec { fill: ${C.parchment}; fill-opacity: .75; }
      </style>
      <ha-card><div class="head"><div class="title"></div><div class="vals"></div></div><svg></svg></ha-card>`;
    this.shadowRoot.querySelector(".title").textContent = this._config.title || "";
    new ResizeObserver(() => this._schedule()).observe(this);
  }

  _schedule() {
    if (this._raf) return;
    this._raf = requestAnimationFrame(() => { this._raf = null; this._render(); });
  }

  _render() {
    if (!this.shadowRoot || !this._hass) return;
    const cfg = this._config, now = Date.now(), t0 = now - this._windowMs();
    const svg = this.shadowRoot.querySelector("svg");
    const W = Math.max(svg.clientWidth || this.clientWidth - 24, 120), H = cfg.height;
    const unit = (this._hass.states[cfg.entities[0].entity]?.attributes.unit_of_measurement) || "";
    const sec = cfg.secondary, fac = sec ? (num(this._hass.states[sec.factor_entity]?.attributes?.[sec.factor_attribute]) ?? num(sec.factor)) : null;
    const L = 38, R = fac ? 40 : 6, T = 6, B = 18;

    let lo = Infinity, hi = -Infinity;
    const series = cfg.entities.map((e, i) => {
      let d = this._data[e.entity] || [];
      const first = d.findIndex((p) => p[0] >= t0);
      if (first > 1) d = this._data[e.entity] = d.slice(first - 1);
      for (const [, v] of d) if (isFinite(v)) { lo = Math.min(lo, v); hi = Math.max(hi, v); }
      return { ...e, color: e.color || [C.brass, C.link, C.ember, C.verdigris][i % 4], d };
    });
    if (cfg.min !== undefined) lo = Math.min(lo, cfg.min);
    if (cfg.max !== undefined) hi = Math.max(hi, cfg.max);
    if (!isFinite(lo)) { lo = 0; hi = 1; }
    if (cfg.zero) { const a = Math.max(Math.abs(lo), Math.abs(hi), 0.5); lo = -a; hi = a; }
    if (hi - lo < 1e-9) { lo -= 0.5; hi += 0.5; }
    const step = niceStep(hi - lo, 4);
    lo = Math.floor(lo / step) * step; hi = Math.ceil(hi / step) * step;

    const X = (t) => L + ((t - t0) / (now - t0)) * (W - L - R);
    const Y = (v) => T + (1 - (v - lo) / (hi - lo)) * (H - T - B);
    let g = "";
    const dec = Math.max(0, -Math.floor(Math.log10(step)));
    for (let v = lo; v <= hi + step / 2; v += step) {
      const y = Y(v).toFixed(1), isZero = cfg.zero && Math.abs(v) < step / 2;
      g += `<line x1="${L}" x2="${W - R}" y1="${y}" y2="${y}" class="${isZero ? "zero" : "grid"}"/>` +
           `<text x="${L - 5}" y="${+y + 3.5}" text-anchor="end">${v.toFixed(dec)}${unit.length <= 2 ? unit : ""}</text>`;
      if (fac) g += `<text x="${W - R + 5}" y="${+y + 3.5}" text-anchor="start" class="sec">${(v * fac).toFixed(sec.decimals ?? 2)}${sec.unit || ""}</text>`;
    }
    const steps = [10e3, 30e3, 60e3, 120e3, 300e3, 600e3, 900e3, 1800e3, 3600e3, 7200e3, 10800e3, 21600e3];
    const tStep = steps.find((s) => (now - t0) / s <= 5) || steps[steps.length - 1];
    const fmt = tStep < 60e3 ? { hour: "numeric", minute: "2-digit", second: "2-digit" } : { hour: "numeric", minute: "2-digit" };
    for (let t = Math.ceil(t0 / tStep) * tStep; t <= now; t += tStep) {
      const x = X(t).toFixed(1);
      g += `<line x1="${x}" x2="${x}" y1="${T}" y2="${H - B}" class="grid"/>` +
           `<text x="${x}" y="${H - 4}" text-anchor="middle">${new Date(t).toLocaleTimeString([], fmt)}</text>`;
    }
    g += `<rect x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}" class="frame"/>`;
    for (const s of series) {
      let run = [];
      const flush = () => { if (run.length) g += `<path d="${monotonePath(run)}" class="line" stroke="${s.color}"/>`; run = []; };
      for (const [t, v] of s.d) {
        if (!isFinite(v)) { flush(); continue; }
        run.push([Math.max(X(t), L), Y(Math.min(Math.max(v, lo), hi))]);
      }
      const last = s.d[s.d.length - 1];
      if (run.length && last && isFinite(last[1])) run.push([X(now), Y(Math.min(Math.max(last[1], lo), hi))]);
      flush();
    }
    svg.setAttribute("viewBox", `0 0 ${W} ${H}`);
    svg.style.height = H + "px";
    svg.innerHTML = g;

    this.shadowRoot.querySelector(".vals").innerHTML = series.map((s) => {
      const st = this._hass.states[s.entity], v = num(st?.state);
      const second = fac && v !== null ? ` <small>·</small>${(v * fac).toFixed(sec.decimals ?? 2)}${sec.unit || ""}` : "";
      return `<span style="color:${s.color}"><small>${s.name || ""}</small>${v === null ? "—" : v.toFixed(cfg.decimals)}${v === null ? "" : unit}${second}</span>`;
    }).join("");
  }
}

const CARDS = [
  ["nina-mount-card", NinaMountCard, "NINA mount", "3D German equatorial mount showing where the telescope points"],
  ["nina-autofocus-card", NinaAutofocusCard, "NINA autofocus", "Last NINA autofocus curve with NINA's fit"],
  ["nina-history-card", NinaHistoryCard, "NINA history", "Time series with real axes and monotone lines"],
];

window.customCards = window.customCards || [];
for (const [type, cls, name, description] of CARDS) {
  if (!customElements.get(type)) customElements.define(type, cls);
  if (!window.customCards.some((c) => c.type === type)) window.customCards.push({ type, name, description });
}

console.info("%c nina-telemetry-cards loaded", `color:${C.brass}`);
