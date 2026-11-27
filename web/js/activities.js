// same rules as ActivityRunner in the unity build (Abyss.Logic), ported straight across
export class Runner {
  constructor() { this.reset(); }
  reset() { this.activity = null; this.index = -1; this.hits = new Set(); this.state = 'idle'; }
  start(a) {
    if (!a?.steps?.length) throw new Error(`activity ${a?.id} has no steps`);
    this.activity = a; this.state = 'running'; this.index = -1; this.advance();
  }
  get step() { return this.state === 'running' ? this.activity.steps[this.index] : null; }
  // kind: discover | inspect | reach | interact
  report(kind, id) {
    const s = this.step;
    if (!s || !id) return 'ignored';
    const kindOk = s.kind === kind || (s.kind === 'discover' && kind === 'inspect') || (s.kind === 'interact' && kind === 'discover');
    if (!kindOk || !s.targets.includes(id)) return kind === 'reach' ? 'ignored' : 'wrong';
    if (this.hits.has(id)) return 'ignored';
    this.hits.add(id);
    this.onHit?.(s, id);
    if (!s.all || s.targets.every((t) => this.hits.has(t))) { this.onStepDone?.(s); this.advance(); }
    return 'ok';
  }
  get progress() {
    if (!this.activity) return 0;
    if (this.state === 'done') return 1;
    let total = 0, hit = 0;
    this.activity.steps.forEach((s, i) => {
      const need = s.all ? s.targets.length : 1;
      total += need;
      if (i < this.index) hit += need;
      else if (i === this.index) hit += Math.min(need, s.targets.filter((t) => this.hits.has(t)).length);
    });
    return hit / total;
  }
  advance() {
    this.hits.clear(); this.index++;
    if (this.index >= this.activity.steps.length) { this.state = 'done'; this.onDone?.(this.activity); return; }
    this.onStep?.(this.activity.steps[this.index], this.index);
  }
}
