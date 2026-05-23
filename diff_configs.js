const fs = require('fs');
const before = JSON.parse(fs.readFileSync('D:/OpenClaw-Manager-20260521/before_save.json', 'utf8'));
const after = JSON.parse(JSON.stringify({
  agents: { defaults: { workspace: "D:\\OpenClaw-Manager\\publish\\workspace", models: { "custom-proxy-scito-us-kg/deepseek-v3.1:671b": { alias: "deepseek-v3.1:671b" }, "deepseek/deepseek-v4-flash": { alias: "deepseek-v4-flash" }, "deepseek/deepseek-v4-pro": { alias: "deepseek-v4-pro" }, "custom-proxy-scito-us-kg/gemini-3.1-flash-lite-preview": { alias: "gemini-3.1-flash-lite-preview" } }, model: { primary: "deepseek/deepseek-v4-pro" }, contextTokens: 409600, session: { limits: { maxTurnsPerSession: 100 } }, heartbeat: { everyMinutes: 30 } }, list: [{ id: "main" }, { id: "desktop-chat", name: "desktop-chat", workspace: "D:\\OpenClaw-Manager\\publish\\workspace", agentDir: "C:\\Users\\yangh\\.openclaw\\agents\\desktop-chat\\agent", model: "deepseek/deepseek-v4-pro" }] },
  gateway: { http: { endpoints: { chatCompletions: { enabled: true }, responses: { enabled: true } } }, mode: "local", auth: { mode: "token", token: "6f07e407a6914fdaddf47008dd9ee65ca0294ada0d3164ac" }, port: 18789, bind: "loopback", tailscale: { mode: "off", resetOnExit: false }, controlUi: { allowInsecureAuth: true }, nodes: { denyCommands: ["camera.snap","camera.clip","screen.record","contacts.add","calendar.add","reminders.add","sms.send","sms.search"] }, logging: { level: "info" } },
  session: { dmScope: "per-channel-peer" },
  tools: { profile: "coding" },
  models: { mode: "merge", providers: { "custom-proxy-scito-us-kg": { baseUrl: "https://proxy.scito.us.kg/v1", api: "openai-completions", apiKey: "sk-bNtoN0j7gffGrikQU", models: [{ id: "deepseek-v3.1:671b", name: "deepseek-v3.1:671b (Custom Provider)", contextWindow: 131072, maxTokens: 40960, input: ["text"], cost: { input: 0, output: 0, cacheRead: 0, cacheWrite: 0 }, reasoning: false, contextTokens: 409600 }] }, deepseek: { baseUrl: "https://api.deepseek.com", api: "openai-completions", apiKey: "sk-3854f704121e480693215e2e2944052f", models: [{ id: "deepseek-v4-pro", name: "deepseek-v4-pro", api: "openai-completions", contextTokens: 409600 }] } } },
  channels: { qqbot: { enabled: true, appId: "1903922097", clientSecret: "PRUYchnu19IRbmx9MZn2HXo5Ng0Kf0Mj" } },
  wizard: { lastRunAt: "2026-05-03T15:26:25.133Z", lastRunVersion: "2026.5.2", lastRunCommand: "onboard", lastRunMode: "local" },
  meta: { lastTouchedVersion: "2026.5.7", lastTouchedAt: "2026-05-11T10:15:13.388Z" },
  plugins: { entries: { qqbot: { enabled: true }, deepseek: { enabled: true }, "talk-voice": { enabled: true } } },
  skills: { entries: {}, install: { nodeManager: "npm" } },
  auth: { profiles: { "deepseek:default": { provider: "deepseek", mode: "api_key" } } }
}));

function deepDiff(a, b, prefix) {
  const allKeys = [...new Set([...Object.keys(a||{}), ...Object.keys(b||{})])];
  for (const k of allKeys) {
    if (!(k in (a||{}))) { console.log('  +' + prefix + '.' + k + ': ' + JSON.stringify(b[k]).substring(0, 100)); continue; }
    if (!(k in (b||{}))) { console.log('  -' + prefix + '.' + k); continue; }
    if (typeof a[k] !== typeof b[k]) { console.log('  ~' + prefix + '.' + k + ': ' + typeof a[k] + ' -> ' + typeof b[k]); continue; }
    if (typeof a[k] === 'object' && a[k] !== null) { deepDiff(a[k], b[k], prefix + '.' + k); continue; }
    if (a[k] !== b[k]) console.log('  ~' + prefix + '.' + k + ': ' + JSON.stringify(a[k]) + ' -> ' + JSON.stringify(b[k]));
  }
}

console.log('=== DIFF: before -> after ===');
deepDiff(before, after, 'root');

// Also check what SaveAll adds that wasn't there before  
console.log('\n=== ANALYSIS ===');
console.log('Before gateway.logging:', JSON.stringify(before.gateway?.logging));
console.log('After  gateway.logging:', JSON.stringify(after.gateway?.logging));
console.log('Before agents.defaults.session:', JSON.stringify(before.agents?.defaults?.session));
console.log('After  agents.defaults.session:', JSON.stringify(after.agents?.defaults?.session));
console.log('Before agents.defaults.heartbeat:', JSON.stringify(before.agents?.defaults?.heartbeat));
console.log('After  agents.defaults.heartbeat:', JSON.stringify(after.agents?.defaults?.heartbeat));
console.log('Before agents.defaults.contextTokens:', before.agents?.defaults?.contextTokens);
console.log('After  agents.defaults.contextTokens:', after.agents?.defaults?.contextTokens);

// Key question: does the changed contextTokens match the model-level value?
console.log('\nBefore model contextTokens:', before.models?.providers?.['custom-proxy-scito-us-kg']?.models?.[0]?.contextTokens);
console.log('After  model contextTokens:', after.models?.providers?.['custom-proxy-scito-us-kg']?.models?.[0]?.contextTokens);
