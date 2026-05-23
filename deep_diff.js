const fs = require('fs');
const homeConfig = JSON.parse(fs.readFileSync('D:/OpenClaw-Manager-20260521/home_config.json','utf8'));
const pubConfig = JSON.parse(fs.readFileSync('D:/OpenClaw-Manager-20260521/pub_config.json','utf8'));

function deepDiff(a, b, prefix, diffs) {
  if (a === b) return;
  if (typeof a !== typeof b) { diffs.push(prefix + ': TYPE ' + typeof a + ' -> ' + typeof b); return; }
  if (typeof a !== 'object' || a === null || b === null) { diffs.push(prefix + ': ' + JSON.stringify(a) + ' -> ' + JSON.stringify(b)); return; }
  const allKeys = [...new Set([...Object.keys(a), ...Object.keys(b)])];
  for (const k of allKeys) {
    const sub = prefix === '' ? k : prefix + '.' + k;
    if (!(k in a)) { diffs.push('+ ' + sub + ': ' + JSON.stringify(b[k]).substring(0, 120)); continue; }
    if (!(k in b)) { diffs.push('- ' + sub); continue; }
    deepDiff(a[k], b[k], sub, diffs);
  }
}

const diffs = [];
deepDiff(homeConfig, pubConfig, '', diffs);

console.log('=== DIFF: home -> publish (after SaveAll) ===');
for (const d of diffs) console.log(d);

// Check if home config would work
console.log('\n=== Would home config pass Gateway validation? ===');
// Write home config to publish dir and try to start gateway
// But first show the actual differences more explicitly

// Show agents.defaults comparison
console.log('\nhome agents.defaults:', JSON.stringify(homeConfig.agents?.defaults, null, 2).substring(0, 500));
console.log('\npub  agents.defaults:', JSON.stringify(pubConfig.agents?.defaults, null, 2).substring(0, 500));

// Show home gateway section
console.log('\nhome gateway:', JSON.stringify(homeConfig.gateway, null, 2).substring(0, 500));
console.log('\npub  gateway:', JSON.stringify(pubConfig.gateway, null, 2).substring(0, 500));

// CRITICAL: Check if pub has any field home doesn't
console.log('\n=== Fields only in publish ===');
function findExtra(a, b, prefix) {
  if (typeof a !== 'object' || typeof b !== 'object' || a === null || b === null) return;
  for (const k of Object.keys(b)) {
    const sub = prefix ? prefix + '.' + k : k;
    if (!(k in a)) { console.log('EXTRA: ' + sub + ' = ' + JSON.stringify(b[k]).substring(0, 100)); continue; }
    findExtra(a[k], b[k], sub);
  }
}
findExtra(homeConfig, pubConfig, '');
