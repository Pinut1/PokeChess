// 병합된 씬의 참조 무결성을 검사한다.
// guid가 붙은 fileID는 외부 에셋 참조이므로 제외하고,
// 씬 내부를 가리켜야 하는 fileID만 실제 블록 존재 여부를 확인한다.
import { readFileSync } from 'node:fs';

const HEADER_RE = /^--- !u!(\d+) &(\d+)(.*)$/;

function parse(path) {
  const lines = readFileSync(path, 'utf8').replace(/^﻿/, '').split(/\r?\n/);
  const blocks = new Map();
  const dupes = [];
  let cur = null;
  for (const line of lines) {
    const m = HEADER_RE.exec(line);
    if (m) {
      if (cur) blocks.set(cur.id, cur);
      if (blocks.has(m[2])) dupes.push(m[2]);
      cur = { id: m[2], type: m[1], stripped: /stripped/.test(m[3]), body: [] };
      continue;
    }
    if (cur) cur.body.push(line);
  }
  if (cur) blocks.set(cur.id, cur);
  return { blocks, dupes, markers: lines.filter((l) => /^(<{7}|={7}$|>{7})/.test(l)).length };
}

function dangling(scene) {
  const bad = [];
  for (const b of scene.blocks.values()) {
    for (const line of b.body) {
      // guid가 있으면 프로젝트 에셋 참조 -> 씬 내부 검사 대상 아님
      if (line.includes('guid:')) continue;
      for (const m of line.matchAll(/fileID: (\d+)/g)) {
        const id = m[1];
        if (id === '0') continue;
        if (!scene.blocks.has(id)) bad.push({ from: b.id, type: b.type, ref: id, line: line.trim() });
      }
    }
  }
  return bad;
}

function structure(scene) {
  const problems = [];
  for (const b of scene.blocks.values()) {
    if (b.type !== '1' || b.stripped) continue;           // GameObject만
    const comps = b.body.join('\n').match(/- component: \{fileID: (\d+)\}/g) ?? [];
    if (comps.length === 0) problems.push({ id: b.id, why: '컴포넌트 0개' });
  }
  // Transform/RectTransform 부모-자식 상호 등록 검사
  for (const b of scene.blocks.values()) {
    if (b.type !== '4' && b.type !== '224') continue;
    if (b.stripped) continue;
    const fm = /m_Father: \{fileID: (\d+)\}/.exec(b.body.join('\n'));
    if (!fm || fm[1] === '0') continue;
    const parent = scene.blocks.get(fm[1]);
    if (!parent) { problems.push({ id: b.id, why: `부모 ${fm[1]} 없음` }); continue; }
    if (parent.stripped) continue;                        // 프리팹 인스턴스의 부모는 본문이 없음
    if (!parent.body.join('\n').includes(`{fileID: ${b.id}}`))
      problems.push({ id: b.id, why: `부모 ${fm[1]}의 m_Children에 미등재` });
  }
  return problems;
}

for (const path of process.argv.slice(2)) {
  const s = parse(path);
  const d = dangling(s);
  const st = structure(s);
  console.log(`\n=== ${path.split(/[\\/]/).pop()} ===`);
  console.log(`  블록 ${s.blocks.size} / 중복ID ${s.dupes.length} / 충돌마커 ${s.markers}`);
  console.log(`  끊어진 씬 내부 참조 ${d.length}`);
  for (const x of d.slice(0, 8)) console.log(`    - &${x.from}(!u!${x.type}) -> ${x.ref}   ${x.line}`);
  console.log(`  구조 이상 ${st.length}`);
  for (const x of st.slice(0, 8)) console.log(`    - &${x.id}: ${x.why}`);
}
