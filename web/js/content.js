// all the text and activity data. same sample content as the quest build,
// swap this file for a client's real material

export const ITEMS = {
  reef_brain_coral: { title: 'Brain coral', sub: 'A slow-growing reef builder', body: 'Brain corals are colonies of many tiny animals living together. The grooves are where neighbouring polyps share tissue. A colony this size can be decades old.' },
  reef_sea_fan: { title: 'Sea fan', sub: 'Faces into the current', body: 'Sea fans grow as a flat net, usually side-on to the water flow. That way every branch gets a share of the tiny food drifting past.' },
  reef_conch: { title: 'Conch shell', sub: 'Built in layers', body: 'The shell grows in a spiral, adding new layers at the opening as the animal gets bigger. The flared lip only forms once it is fully grown.', inspect: true },
  reef_staghorn: { title: 'Staghorn coral', sub: 'Shelter for small fish', body: 'Branching corals grow fast and build a maze of gaps that small fish use to hide. They are also some of the most fragile corals on a reef.' },
  exhibit_light: { title: 'Light underwater', sub: 'Why everything turns blue', body: 'Water soaks up red light within the first few metres, then orange and yellow. By the time you are deep, mostly blue is left. Watch the colours change as you go down.' },
  exhibit_pressure: { title: 'Pressure', sub: 'Heavier with every metre', body: 'Every ten metres of seawater adds roughly the weight of the whole atmosphere again. Animals in the deep are built for it, people need equipment.' },
  exhibit_currents: { title: 'Currents', sub: 'The ocean is always moving', body: 'Currents carry heat, food and larvae across huge distances. The swaying kelp and drifting particles around you show the water moving.' },
  wreck_compass: { title: "Ship's compass", sub: 'A find from the wreck', body: 'A brass compass in its wooden case. Brass holds up well in seawater compared to iron, which is why fittings like this are often what survives of a wreck.', inspect: true },
  wreck_anchor: { title: 'Anchor', sub: 'Where the ship stopped', body: 'This anchor lies a few metres from the hull. Its position is a clue to how the ship came to rest.' },
  deep_anemone: { title: 'Glowing anemone', sub: 'Making its own light', body: 'Many deep animals make light with chemistry inside their bodies. It is used to find food, confuse predators or recognise each other in the dark.' },
  deep_columns: { title: 'Rock columns', sub: 'A landmark in the deep', body: 'Columns like these form when lava cools slowly and cracks into regular shapes. Use them to find your way back.' },
};

export const ACTIVITIES = [
  {
    id: 'discover_reef', title: 'Discover the reef', minutes: 3,
    intro: 'Three things on this reef are worth a closer look.',
    summary: "You found the brain coral, the sea fan and the conch shell.",
    steps: [{ kind: 'discover', text: 'Find the brain coral, the sea fan and the conch shell', targets: ['reef_brain_coral', 'reef_sea_fan', 'reef_conch'], all: true }],
  },
  {
    id: 'inspect_object', title: 'Take a closer look', minutes: 3,
    intro: 'Head to the wreck and examine a find up close.',
    summary: "You inspected the ship's compass up close.",
    steps: [
      { kind: 'reach', text: 'Swim to the wreck, follow the arrow', targets: ['dest_wreck'] },
      { kind: 'inspect', text: "Select the ship's compass, press Inspect, turn it around, then Done", targets: ['wreck_compass'] },
    ],
  },
  {
    id: 'guided_tour', title: 'Guided exploration', minutes: 6,
    intro: 'The exhibition, the wreck and the deep.',
    summary: 'You visited the exhibition, the wreck and the deep.',
    steps: [
      { kind: 'reach', text: 'Go to the exhibition clearing', targets: ['dest_exhibition'] },
      { kind: 'interact', text: 'Select the Currents marker and read it', targets: ['exhibit_currents'] },
      { kind: 'reach', text: 'Now head to the wreck', targets: ['dest_wreck'] },
      { kind: 'interact', text: 'Find the anchor and select it', targets: ['wreck_anchor'] },
      { kind: 'reach', text: 'Go down into the deep, past the rock columns', targets: ['dest_deep'] },
      { kind: 'interact', text: 'Find something glowing and select it', targets: ['deep_anemone'] },
    ],
  },
];

export const AREAS = [
  { id: 'descent', name: 'The Descent' },
  { id: 'reef', name: 'The Reef' },
  { id: 'exhibition', name: 'Exhibition' },
  { id: 'wreck', name: 'The Wreck' },
  { id: 'deep', name: 'The Deep' },
];

// the cinematic tour. points are (forward, right, height above floor) in the same layout space as world.js
export const TOUR = [
  { at: [-4, 0, 3.2], look: [12, 0, 1.5], t: 0, cap: ['The Descent', 'Into the blue', 'Sunlight still reaches the sand here. Everything ahead is built from real scanned rock and coral.'] },
  { at: [8, -2, 2.4], look: [16, 1, 1.4], t: 9 },
  { at: [16, 1, 2.0], look: [24, -3, 0.6], t: 16, cap: ['The Reef', 'Nine real coral species', 'Scanned from specimens in the Smithsonian coral collection, coloured back to life.'] },
  { at: [23, 3.5, 1.4], look: [28, -2, 0.4], t: 25 },
  { at: [27, -9, 2.2], look: [30, -24, 1], t: 33 },
  { at: [26, -19, 1.7], look: [31, -24, 0.6], t: 40, cap: ['Exhibition', 'Learn as you go', 'Point at anything that glows. Guided activities walk visitors through it step by step.'] },
  { at: [33, -8, 2.6], look: [44, 16, 2], t: 49 },
  { at: [37, 13, 2.4], look: [48, 23, 2.5], t: 56, cap: ['The Wreck', 'Where the ship came to rest', 'A full ship model laid on its side, with crates, barrels and a compass to find.'] },
  { at: [42, 31, 3.5], look: [49, 23, 2], t: 66 },
  { at: [52, 14, 3.0], look: [64, 2, 0], t: 74 },
  { at: [61, 3, 2.0], look: [70, 0, 0], t: 81, cap: ['The Deep', 'Light runs out', 'Basalt columns, tube worms and animals that make their own light.'] },
  { at: [68, 4, 1.4], look: [73, -2, 0.5], t: 90 },
  { at: [64, 5, 4.5], look: [40, 2, 6], t: 100, cap: ['ABYSS', 'Browser and Meta Quest', 'One world, two ways in.'] },
  { at: [59, 6, 6.0], look: [30, 2, 9], t: 108 },
];
