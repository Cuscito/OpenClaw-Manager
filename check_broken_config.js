const fs = require('fs');
const p = 'D:/OpenClaw-Manager/publish/.openclaw/openclaw.json';
const raw = fs.readFileSync(p);
console.log('Size:', raw.length);
console.log('First 10 bytes hex:', raw.subarray(0, 10).toString('hex'));
console.log('First 10 bytes:', raw.subarray(0, 10).toString());
console.log('---');

// Try to locate the parse error
try {
  JSON.parse(raw.toString('utf8'));
} catch(e) {
  const pos = parseInt(e.message.match(/position (\d+)/)?.[1]);
  console.log('Parse error at byte', pos);
  console.log('Around error (50 bytes):');
  const start = Math.max(0, pos - 25);
  const end = Math.min(raw.length, pos + 25);
  console.log('  HEX:', raw.subarray(start, end).toString('hex'));
  console.log('  STR:', raw.subarray(start, end).toString('utf8'));
  
  // Replace the problematic bytes and show what character it is
  const badByte = raw[pos];
  console.log('Bad byte:', badByte, 'hex:', badByte?.toString(16));
}
