// two ambience loops + short cues, all generated from scratch for the project.
// browsers block sound until the first click, so start() runs on "Dive in"
const CUES = {
  hover: ['ui_hover', 0.15], click: ['ui_click', 0.35], select: ['int_select', 0.45], fail: ['int_fail', 0.4],
  discover: ['int_discover', 0.5], step: ['int_step_complete', 0.5], done: ['int_activity_complete', 0.55], teleport: ['nav_teleport', 0.3], menu: ['ui_menu_open', 0.3],
};

export class Sound {
  constructor() { this.muted = false; this.loops = []; this.cache = {}; }
  start() {
    if (this.loops.length) return;
    for (const [n, v] of [['amb_underwater_loop', 0.55], ['amb_bubbles_loop', 0.22]]) {
      const a = new Audio(`assets/audio/${n}.mp3`);
      a.loop = true; a.volume = this.muted ? 0 : v; a.dataset.vol = v;
      a.play().catch(() => {});
      this.loops.push(a);
    }
  }
  play(name) {
    if (this.muted || !CUES[name]) return;
    const [file, vol] = CUES[name];
    const a = (this.cache[file] ||= new Audio(`assets/audio/${file}.mp3`)).cloneNode();
    a.volume = vol;
    a.play().catch(() => {});
  }
  setMuted(m) {
    this.muted = m;
    for (const a of this.loops) a.volume = m ? 0 : +a.dataset.vol;
  }
}
