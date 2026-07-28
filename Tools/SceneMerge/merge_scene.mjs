// Unity 씬(YAML)을 최상위 블록 단위로 3-way 병합한다.
// 텍스트 diff는 블록 경계를 무시해 서로 다른 오브젝트를 한 덩어리로 뭉개지만,
// 씬은 "--- !u!<type> &<fileID>" 로 시작하는 독립 블록의 나열이므로
// fileID를 키로 삼으면 안전하게 합칠 수 있다.
import { readFileSync, writeFileSync } from 'node:fs';

const HEADER_RE = /^--- !u!(\d+) &(\d+)(.*)$/;

function parse(path) {
  const text = readFileSync(path, 'utf8').replace(/^﻿/, '');
  const lines = text.split(/\r?\n/);
  const preamble = [];
  const blocks = new Map(); // fileID -> { id, type, suffix, body }
  const order = [];

  let cur = null;
  for (const line of lines) {
    const m = HEADER_RE.exec(line);
    if (m) {
      if (cur) blocks.set(cur.id, cur);
      cur = { id: m[2], type: m[1], suffix: m[3], body: [] };
      order.push(m[2]);
      continue;
    }
    if (cur) cur.body.push(line);
    else preamble.push(line);
  }
  if (cur) blocks.set(cur.id, cur);

  // 파일 끝 개행으로 생긴 빈 줄은 블록 본문에서 떼어낸다.
  for (const b of blocks.values()) {
    while (b.body.length && b.body[b.body.length - 1] === '') b.body.pop();
  }
  return { preamble, blocks, order };
}

const key = (b) => (b ? `!u!${b.type}${b.suffix}\n${b.body.join('\n')}` : null);

const base = parse(process.argv[2]);
const ours = parse(process.argv[3]);
const theirs = parse(process.argv[4]);
const outPath = process.argv[5];

const allIds = new Set([...ours.order, ...theirs.order, ...base.order]);
const result = new Map();
const conflicts = [];
const stats = { kept: 0, oursOnly: 0, theirsOnly: 0, addedOurs: 0, addedTheirs: 0, deleted: 0 };

for (const id of allIds) {
  const b = base.blocks.get(id);
  const o = ours.blocks.get(id);
  const t = theirs.blocks.get(id);
  const kb = key(b), ko = key(o), kt = key(t);

  if (o && t) {
    if (ko === kt) { result.set(id, o); stats.kept++; }
    else if (kb !== null && kb === ko) { result.set(id, t); stats.theirsOnly++; }
    else if (kb !== null && kb === kt) { result.set(id, o); stats.oursOnly++; }
    else if (kb === null) { result.set(id, o); conflicts.push({ id, kind: 'both-added-same-id' }); }
    else { result.set(id, o); conflicts.push({ id, kind: 'both-modified' }); }
  } else if (o && !t) {
    if (!b) { result.set(id, o); stats.addedOurs++; }
    else if (kb === ko) { stats.deleted++; }              // theirs가 삭제, ours 무변경 -> 삭제 확정
    else { result.set(id, o); conflicts.push({ id, kind: 'ours-modified-theirs-deleted' }); }
  } else if (!o && t) {
    if (!b) { result.set(id, t); stats.addedTheirs++; }
    else if (kb === kt) { stats.deleted++; }              // ours가 삭제, theirs 무변경 -> 삭제 확정
    else { result.set(id, t); conflicts.push({ id, kind: 'theirs-modified-ours-deleted' }); }
  } // 양쪽 다 삭제 -> 버림
}

// 출력 순서: ours 순서를 기준으로 하고, theirs에만 있는 신규 블록은
// theirs에서의 직전 블록 뒤에 끼워 넣어 원본 인접성을 최대한 보존한다.
const emitted = new Set();
const order = [];
const pushId = (id) => {
  if (!emitted.has(id) && result.has(id)) { emitted.add(id); order.push(id); }
};

const theirsNewAfter = new Map(); // anchorId -> [newIds]
{
  let anchor = null;
  for (const id of theirs.order) {
    if (ours.blocks.has(id)) { anchor = id; continue; }
    if (!result.has(id)) continue;
    if (!theirsNewAfter.has(anchor)) theirsNewAfter.set(anchor, []);
    theirsNewAfter.get(anchor).push(id);
  }
}

for (const id of theirsNewAfter.get(null) ?? []) pushId(id);
for (const id of ours.order) {
  pushId(id);
  for (const nid of theirsNewAfter.get(id) ?? []) pushId(nid);
}
for (const id of theirs.order) pushId(id);   // 안전망
for (const id of result.keys()) pushId(id);

const out = [...ours.preamble.filter((l) => l !== '')];
for (const id of order) {
  const b = result.get(id);
  out.push(`--- !u!${b.type} &${b.id}${b.suffix}`);
  out.push(...b.body);
}
writeFileSync(outPath, out.join('\n') + '\n', 'utf8');

console.log(JSON.stringify({
  base: base.blocks.size, ours: ours.blocks.size, theirs: theirs.blocks.size,
  merged: result.size, stats, conflicts,
}, null, 2));
