using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace OpenClawManager;

public class ChatPage
{
    Panel body, bgPanel;
    WebView2 wv;
    Panel toolbar;
    Button toggleBtn, thinkBtn, toolsBtn, schedBtn;
    bool _built;
    string _processedHtml = "";
    bool _showThinking = true, _showTools = true, _showSchedule = true;

    static string ChatTemplate = @"<!DOCTYPE html>
<html lang=""zh-CN"" data-theme=""{0}"" data-theme-mode=""{0}"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0, viewport-fit=cover"">
<style>
/* === web 控制台同款 CSS 变量 === */
:root {{
  --bg:#0e1015; --bg-elevated:#191c24; --bg-hover:#1f2330; --bg-muted:#1f2330;
  --card:#161920; --card-foreground:#f0f0f2; --card-highlight:#ffffff0a;
  --panel:#0e1015; --panel-strong:#191c24;
  --text:#d4d4d8; --text-strong:#f4f4f5; --chat-text:#d4d4d8;
  --muted:#838387; --muted-strong:#75757d;
  --border:#1e2028; --border-strong:#2e3040;
  --input:#1e2028; --ring:#ff5c5c; --accent:#ff5c5c; --accent-hover:#ff7070;
  --accent-subtle:#ff5c5c1a; --accent-foreground:#fafafa; --accent-glow:#ff5c5c33;
  --primary:#ff5c5c; --primary-foreground:#fff;
  --secondary:#161920; --secondary-foreground:#f0f0f2;
  --ok:#22c55e; --ok-subtle:#22c55e14; --warn:#f59e0b; --warn-subtle:#f59e0b14;
  --danger:#ef4444; --danger-subtle:#ef444414; --info:#3b82f6;
  --focus-ring:0 0 0 2px var(--bg), 0 0 0 3px color-mix(in srgb, var(--ring) 80%, transparent);
  --mono:""JetBrains Mono"", ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  --font-body:""Inter"", -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif;
  --radius-sm:6px; --radius-md:10px; --radius-lg:14px; --radius-full:9999px;
  --ease-out:cubic-bezier(.16,1,.3,1);
  --sidebar-w:260px;
}}

/* === 亮色主题 === */
[data-theme-mode=""light""] {{
  --bg:#f5f7fa; --bg-elevated:#fff; --bg-hover:#f0f2f5; --bg-muted:#f0f2f5;
  --card:#fff; --card-foreground:#1a1a2e; --card-highlight:#00000008;
  --panel:#f5f7fa; --panel-strong:#fff;
  --text:#383842; --text-strong:#1a1a2e; --chat-text:#383842;
  --muted:#6b7280; --muted-strong:#4b5563;
  --border:#e5e7eb; --border-strong:#d1d5db;
  --input:#e5e7eb;
  --secondary:#f3f4f6; --secondary-foreground:#1a1a2e;
}}

/* === 基础重置 === */
*,*::before,*::after{{box-sizing:border-box;margin:0;padding:0}}
html,body{{height:100%;overflow:hidden}}
body{{font-family:var(--font-body);font-size:14px;color:var(--text);background:var(--bg);-webkit-font-smoothing:antialiased}}
a{{color:var(--accent);text-decoration:none}}
a:hover{{text-decoration:underline}}
pre{{font-family:var(--mono);background:var(--card);border:1px solid var(--border);border-radius:var(--radius-md);padding:12px;overflow-x:auto;font-size:13px;line-height:1.5}}
code{{font-family:var(--mono);font-size:13px;background:var(--bg-muted);padding:2px 5px;border-radius:4px}}
pre code{{background:0 0;padding:0}}
h1,h2,h3{{color:var(--text-strong);margin:12px 0 6px}}
blockquote{{border-left:3px solid var(--border-strong);color:var(--muted);padding:4px 12px;margin:8px 0}}
ul,ol{{padding-left:20px;margin:4px 0}}
li{{margin:2px 0}}

/* === 布局 === */
.app{{display:flex;flex-direction:column;height:100vh}}
.header{{flex-shrink:0;display:flex;align-items:center;gap:6px;padding:6px 12px;background:var(--bg-elevated);border-bottom:1px solid var(--border);min-height:38px}}
.body-row{{flex:1;display:flex;overflow:hidden}}

.sidebar{{flex-shrink:0;width:var(--sidebar-w);background:var(--bg-elevated);border-right:1px solid var(--border);display:flex;flex-direction:column;transition:width .2s var(--ease-out);overflow:hidden}}
.sidebar.collapsed{{width:0;border-right:none;overflow:hidden}}
.sidebar-header{{padding:8px 12px;border-bottom:1px solid var(--border);flex-shrink:0}}
.sidebar-list{{flex:1;overflow-y:auto;padding:4px}}
.sidebar-list::-webkit-scrollbar{{width:4px}}
.sidebar-list::-webkit-scrollbar-thumb{{background:var(--border-strong);border-radius:2px}}

/* === 会话项 === */
.sess-item{{padding:10px 12px;border-radius:var(--radius-md);cursor:pointer;margin-bottom:2px;position:relative;transition:background .1s}}
.sess-item:hover{{background:var(--bg-hover)}}
.sess-item.active{{background:var(--accent-subtle)}}
.sess-item-title{{font-size:13px;font-weight:600;color:var(--text-strong);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;padding-right:20px}}
.sess-item-sub{{font-size:11px;color:var(--muted);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;margin-top:2px}}
.sess-item-time{{font-size:10px;color:var(--muted);margin-top:4px}}
.sess-item-del{{position:absolute;top:8px;right:8px;width:18px;height:18px;border-radius:50%;border:none;background:0 0;color:var(--muted);cursor:pointer;font-size:12px;display:flex;align-items:center;justify-content:center;opacity:0}}
.sess-item:hover .sess-item-del{{opacity:1}}
.sess-item-del:hover{{color:var(--danger);background:var(--danger-subtle)}}

/* === 主区域 === */
.main-area{{flex:1;display:flex;flex-direction:column;min-width:0}}
.header-toggle{{background:0 0;border:none;color:var(--muted);cursor:pointer;font-size:16px;padding:2px 6px;border-radius:4px}}
.header-toggle:hover{{color:var(--text);background:var(--bg-hover)}}
.header-title{{font-weight:600;font-size:13px;color:var(--text-strong);flex:1}}
.header-actions{{display:flex;gap:4px}}
.filter-btn{{border:1px solid transparent;background:0 0;border-radius:var(--radius-sm);color:var(--muted);cursor:pointer;padding:3px 7px;font-size:12px;transition:all .15s}}
.filter-btn:hover{{color:var(--text);background:var(--bg-hover)}}
.filter-btn.on{{color:var(--accent);background:var(--accent-subtle);border-color:var(--accent-subtle)}}
.btn{{border:1px solid var(--border);background:var(--bg-elevated);color:var(--text);border-radius:var(--radius-md);cursor:pointer;padding:4px 10px;font-size:11px;font-weight:500;transition:all .15s var(--ease-out)}}
.btn:hover{{background:var(--bg-hover);border-color:var(--border-strong)}}
.btn.primary{{background:var(--accent);color:var(--primary-foreground);border-color:var(--accent)}}
.btn.primary:hover{{background:var(--accent-hover)}}
.status-dot{{border-radius:var(--radius-full);width:6px;height:6px;background:var(--danger);flex-shrink:0}}
.status-dot.ok{{background:var(--ok);box-shadow:0 0 6px #22c55e80}}
.status-dot.warn{{background:var(--warn)}}

/* === 消息区 === */
.msg-area{{flex:1;overflow-y:auto;padding:8px 16px 0;display:flex;flex-direction:column;gap:4px}}
.msg-area::-webkit-scrollbar{{width:6px}}
.msg-area::-webkit-scrollbar-track{{background:transparent}}
.msg-area::-webkit-scrollbar-thumb{{background:var(--border-strong);border-radius:3px}}

/* === 消息行、头像、名字 === */
.chat-line{{display:flex;flex-direction:column;margin-bottom:12px}}
.chat-line.user{{align-items:flex-end}}
.chat-line.assistant{{align-items:flex-start}}
.chat-avatar-row{{display:flex;align-items:center;gap:6px;margin-bottom:2px}}
.chat-line.user .chat-avatar-row{{flex-direction:row-reverse}}
.chat-avatar{{border-radius:var(--radius-md);background:var(--panel-strong);border:1px solid var(--border);flex-shrink:0;width:30px;height:30px;display:flex;align-items:center;justify-content:center;font-size:14px;overflow:hidden}}
.chat-avatar.user{{background:var(--accent-subtle);color:var(--accent);border-color:#ff5c5c33}}
.chat-avatar.assistant{{background:var(--secondary);color:var(--muted)}}
.chat-sender-name{{color:var(--text-strong);font-size:13px;font-weight:600}}
.chat-stamp{{color:var(--muted);font-size:11px}}
.chat-content{{max-width:80%}}
.chat-line.user .chat-content{{align-self:flex-end}}
.chat-line.assistant .chat-content{{align-self:flex-start}}
.chat-bubble{{border-radius:var(--radius-md);padding:10px 14px;line-height:1.6;border:1px solid transparent;position:relative}}
.chat-line.user .chat-bubble{{background:var(--accent-subtle);border-color:#ff5c5c33}}
.chat-line.assistant .chat-bubble{{background:var(--secondary);border-color:var(--border)}}
.chat-bubble .copy-btn{{position:absolute;top:6px;right:8px;opacity:0;background:var(--bg-muted);border:1px solid var(--border);border-radius:4px;color:var(--muted);cursor:pointer;font-size:10px;padding:2px 6px;transition:opacity .15s}}
.chat-bubble:hover .copy-btn{{opacity:1}}
.chat-bubble .copy-btn:hover{{color:var(--text)}}
.chat-text{{word-wrap:break-word;overflow-wrap:break-word;font-size:14px;line-height:1.6}}
.chat-text table{{max-width:100%;display:block;overflow-x:auto;border-collapse:collapse;margin:8px 0}}
.chat-text th,.chat-text td{{border:1px solid var(--border);padding:6px 10px;text-align:left}}
.chat-text th{{background:var(--bg-muted);font-weight:600}}
.chat-text img{{max-width:100%;border-radius:var(--radius-md);margin:8px 0}}

/* === 思考块、工具卡片、输入指示器、输入栏 === (同前，略缩) === */
.chat-thinking{{border-radius:var(--radius-md);color:var(--muted);background:#ffffff0a;border:1px dashed #ffffff2e;margin-top:8px;padding:10px 12px;font-size:12px;line-height:1.4}}
[data-theme-mode=""light""] .chat-thinking{{background:#1018280a;border-color:#10182840}}
.chat-thinking summary{{cursor:pointer;font-weight:500;color:var(--muted-strong);margin-bottom:4px}}

.chat-tool-card{{box-sizing:border-box;border:1px solid var(--border);border-radius:var(--radius-md);background:var(--bg-muted);max-height:62px;overflow:hidden;cursor:pointer;margin-top:8px;padding:8px 10px;font-size:13px;transition:max-height .2s var(--ease-out),border-color .15s,background .15s}}
.chat-tool-card:first-child{{margin-top:8px}}
.chat-tool-card:hover{{border-color:var(--border-strong)}}
.chat-tool-card.tool-running{{border-color:var(--warn);box-shadow:0 0 0 1px var(--warn-subtle)}}
.chat-tool-card--expanded{{max-height:600px;overflow:visible;cursor:default}}
.chat-tool-card__header{{display:flex;justify-content:space-between;align-items:flex-start;gap:10px;min-width:0}}
.chat-tool-card__title{{display:flex;align-items:center;gap:6px;min-width:0;font-size:13px;font-weight:600;line-height:1.3;color:var(--text)}}
.chat-tool-card__title .name{{font-family:var(--mono);font-size:12px}}
.chat-tool-card__icon{{flex-shrink:0;width:18px;height:18px;display:inline-flex;align-items:center;justify-content:center;font-size:13px}}
.chat-tool-card__actions{{display:flex;align-items:center;gap:4px;flex-shrink:0}}
.chat-tool-card__action-btn{{border:1px solid var(--border);background:var(--secondary);border-radius:var(--radius-sm);color:var(--muted);cursor:pointer;width:28px;height:28px;display:inline-flex;align-items:center;justify-content:center;font-size:11px;transition:all .15s}}
.chat-tool-card__action-btn:hover{{color:var(--text)}}
.chat-tool-card--expanded .chat-tool-card__action-btn .arrow{{transform:rotate(180deg)}}
.chat-tool-card__action-btn .arrow{{transition:transform .15s;display:inline-block}}
.chat-tool-card__status{{display:flex;align-items:center;gap:4px;font-size:11px;flex-shrink:0}}
.chat-tool-card__status.ok{{color:var(--ok)}}.chat-tool-card__status.running{{color:var(--warn)}}
.chat-tool-card__status-dot{{border-radius:50%;width:6px;height:6px}}
.chat-tool-card__status.ok .chat-tool-card__status-dot{{background:var(--ok)}}
.chat-tool-card__status.running .chat-tool-card__status-dot{{background:var(--warn);animation:pulse 1.5s infinite}}
@keyframes pulse{{0%,100%{{opacity:.4;transform:scale(.8)}}50%{{opacity:1;transform:scale(1.2)}}}}
.chat-tool-card__body{{margin-top:8px}}
.chat-tool-card__detail{{font-family:var(--mono);color:var(--muted);font-size:11px;margin-bottom:6px;word-break:break-all;line-height:1.4}}
.chat-tool-card__block-header{{color:var(--muted);text-transform:uppercase;letter-spacing:.04em;font-size:11px;font-weight:600;margin-bottom:4px}}
.chat-tool-card__output{{font-family:var(--mono);white-space:pre-wrap;color:var(--chat-text);border-radius:var(--radius-md);border:1px solid var(--border);background:var(--card);padding:10px 12px;font-size:12px;max-height:300px;overflow:auto;line-height:1.5}}
.chat-tool-card__output.empty{{color:var(--muted);font-style:italic;text-align:center;padding:16px}}

.chat-reading-indicator{{border:1px solid var(--border);background:var(--secondary);border-radius:var(--radius-md);padding:10px 16px;display:inline-flex}}
.chat-reading-indicator__dots{{display:flex;align-items:center;gap:4px;height:12px}}
.chat-reading-indicator__dots span{{border-radius:50%;background:var(--muted);opacity:.6;width:6px;height:6px;animation:1.2s ease-in-out infinite readingPulse}}
.chat-reading-indicator__dots span:nth-child(2){{animation-delay:.15s}}
.chat-reading-indicator__dots span:nth-child(3){{animation-delay:.3s}}
@keyframes readingPulse{{0%,100%{{opacity:.3;transform:scale(.8)}}50%{{opacity:1;transform:scale(1.2)}}}}

.compose{{flex-shrink:0;background:linear-gradient(to bottom,transparent,var(--bg) 20%);z-index:10;flex-direction:column;gap:6px;padding:10px 12px 12px;display:flex;border-top:1px solid var(--border)}}
.compose__row{{display:flex;align-items:flex-end;gap:8px}}
.compose__field{{flex:1;min-width:0;display:flex}}
.compose__field textarea{{flex:1;resize:none;border:1px solid var(--input);background:var(--card);border-radius:var(--radius-md);color:var(--text);font-family:var(--font-body);font-size:14px;padding:8px 10px;min-height:56px;max-height:180px;line-height:1.5;outline:none;transition:border-color .15s;box-shadow:inset 0 1px 0 var(--card-highlight)}}
.compose__field textarea:focus{{border-color:var(--ring);box-shadow:var(--focus-ring)}}
.compose__actions{{display:flex;align-items:center;gap:6px;flex-shrink:0}}
.compose__toolbar{{display:flex;align-items:center;gap:1px;padding:0;flex-wrap:nowrap}}
.compose__tool-btn{{border:1px solid var(--border);background:var(--bg-elevated);border-radius:var(--radius-md);color:var(--text);cursor:pointer;height:28px;display:inline-flex;align-items:center;justify-content:center;font-size:12px;padding:0 10px;gap:4px;transition:all .15s var(--ease-out);font-weight:500;white-space:nowrap}}
.compose__tool-btn:hover{{background:var(--accent);color:var(--primary-foreground);border-color:var(--accent);box-shadow:0 2px 8px var(--accent-glow);transform:translateY(-1px)}}
.compose__tool-btn.active{{color:var(--accent);background:var(--accent-subtle)}}
.compose__tool-btn.danger:hover{{background:var(--danger);border-color:var(--danger);box-shadow:0 2px 8px var(--danger-subtle)}}
.compose__preview{{display:flex;gap:6px;padding:2px 0;flex-wrap:wrap}}
.compose__preview img{{width:48px;height:48px;object-fit:cover;border-radius:4px;border:1px solid var(--border)}}
.compose__preview .file-tag{{display:flex;align-items:center;gap:4px;background:var(--bg-muted);border-radius:4px;padding:2px 8px;font-size:11px;color:var(--muted)}}
.compose__preview .file-tag .remove{{color:var(--muted);cursor:pointer;font-size:14px;line-height:1}}
.compose__preview .file-tag .remove:hover{{color:var(--danger)}}
.send-btn{{border-radius:var(--radius-md);width:40px;height:40px;min-width:40px;border:none;background:var(--accent);color:var(--primary-foreground);cursor:pointer;font-size:18px;display:flex;align-items:center;justify-content:center;transition:background .15s}}
.send-btn:hover{{background:var(--accent-hover)}}
.send-btn:disabled{{opacity:.3;cursor:not-allowed}}
.send-btn.stop-btn{{background:var(--danger)}}
</style>
</head>
<body style=""background:{1}"">

<div class=""app"">
  <!-- 顶部 Header（全宽） -->
  <div class=""header"">
    <span class=""header-title"">AI 对话</span>
    <div class=""header-actions"">
      <button class=""filter-btn"" id=""stopBtn"" style=""display:none"">⏹</button>
    </div>
  </div>
  <!-- 下方：侧边栏 + 主区域 -->
  <div class=""body-row"">
    <div class=""sidebar collapsed"" id=""sidebar"">
      <div class=""sidebar-header"">
        <button class=""btn primary"" onclick=""newChat()"" style=""width:100%"">+ 新对话</button>
      </div>
      <div class=""sidebar-list"" id=""sessList""></div>
    </div>
    <div class=""main-area"">
      <div class=""msg-area"" id=""msgList""></div>
      <div class=""compose"">
        <div class=""compose__toolbar"">
          <button class=""compose__tool-btn"" onclick=""pickFile()"">📎 添加附件</button>
          <button class=""compose__tool-btn"" onclick=""doCmd('new')"">＋ 新建会话</button>
          <button class=""compose__tool-btn danger"" onclick=""doCmd('stop')"">⏹ 停止</button>
          <button class=""compose__tool-btn"" onclick=""doCmd('clear')"">🗑 清屏</button>
        </div>
        <div class=""compose__row"">
          <div class=""compose__field""><textarea id=""input"" placeholder=""输入消息..."" rows=""1"" oninput=""autoResize(this)""></textarea></div>
          <div class=""compose__actions""><button class=""send-btn"" id=""sendBtn"" onclick=""doSend()"">➤</button></div>
        </div>
      </div>
    </div>
  </div>
</div>

{9}

<script>
// ====== 配置 ======
const CONFIG = {{
  model: '{8}',
  aiName: '{2}', aiEmoji: '{3}', aiAvatar: '{6}',
  userName: '{4}', userEmoji: '{5}',
  sessKey: '{7}', theme: '{0}'
}};

// ====== 状态 ======
let rpcSeq=10, rpcPending={{}}, sessKey, sessCreated, streamBuf='', stopRequested=false;
let messages=[], currentAssistant=null, currentToolCards={{}}, streamTimer=0;
let wsReady=false, sessions=[], curSessId='', curSessGuid='';
let showThinking=true, showTools=true, showSchedule=true;

// ====== 桥接 ======
function wsSend(raw){{window.chrome.webview.postMessage(JSON.stringify({{type:'ws-send',data:raw}}));}}
function saveMsg(role,text){{
  // Gateway 自动持久化到 JSONL，不需要手动写
}}

window.handleWsMsg=function(raw){{
  try{{handleMsg(typeof raw==='string'?JSON.parse(raw):raw);}}catch(e){{}}
}};

// C# 动态更新身份
window.updateIdentity=function(aiName,aiEmoji,userName,userEmoji,aiAvatar){{
  CONFIG.aiName=aiName||CONFIG.aiName;
  CONFIG.aiEmoji=aiEmoji||CONFIG.aiEmoji;
  CONFIG.userName=userName||CONFIG.userName;
  CONFIG.userEmoji=userEmoji||CONFIG.userEmoji;
  if(aiAvatar!==undefined)CONFIG.aiAvatar=aiAvatar;
  render();
}};

// C# 过滤按钮回调
window.setFilter=function(type,val){{
  if(type==='💭'){{showThinking=val;}}
  else if(type==='🔧'){{showTools=val;}}
  else if(type==='⏰'){{showSchedule=val;}}
  render();
}};

// 接收 C# 消息（WebView2 专用通道）
window.chrome.webview.addEventListener('message', function(e){{
  try{{
    var m=typeof e.data==='string'?JSON.parse(e.data):e.data;
    if(m.type==='append-thinking'){{
      var found=currentAssistant;
      if(!found){{for(var i=messages.length-1;i>=0;i--){{
        if(messages[i].role=='assistant'){{found=messages[i];break;}}
      }}}}
      if(found){{for(var j=0;j<m.blocks.length;j++){{found.blocks.unshift({{type:'thinking',text:m.blocks[j]}});}}}}render();
      return;
    }}
    if(m.type==='sessions'){{
      sessions=m.list||[];renderSS();
      // 仅首次加载时切换会话，避免刷新列表时重复 switch 重置状态
      if(!curSessId){{
        var found=sessions.find(function(s){{return s.id===CONFIG.sessKey;}});
        if(found)switchSess(found.key, found.id);
        else if(sessions.length>0)switchSess(sessions[0].key, sessions[0].id);
        else newChat();
      }}
    }}
    else if(m.type==='msgs'){{messages=m.list||[];render();}}
  }}catch(ex){{}}
}});

// 启动
curSessId='';sessKey=CONFIG.sessKey||'';
document.documentElement.setAttribute('data-theme-mode',CONFIG.theme);
window.chrome.webview.postMessage(JSON.stringify({{type:'connect',sessKey:sessKey}}));
setSendMode();

// ====== handleMsg / handleEvent / streamRender / render 等 (同前) ======
function handleMsg(msg){{
  if(msg.type==='event'){{
    if(msg.event==='agent'){{
      const pld=msg.payload, sk=msg.sessionKey||(pld?pld.sessionKey:'')||'';
      if(sk&&sessKey&&!sk.endsWith(sessKey))return;
      if(pld)handleEvent(pld);
    }}
    return;
  }}
  if(msg.type==='res'){{
    const id=String(msg.id||'');
    if(id==='1')showStatus(msg.ok?'connected':'connect fail',msg.ok?'ok':'warn');
    if(id==='1'&&msg.ok){{wsReady=true;
      // sessions.create deferred to newChat or send handler
    }}
    if(rpcPending[id]){{rpcPending[id](msg);delete rpcPending[id];}}
  }}
}}

function rpc(method,params){{return new Promise((resolve,reject)=>{{
  const id=++rpcSeq;rpcPending[id]=resolve;
  wsSend(JSON.stringify({{type:'req',id:String(id),method,params}}));
  setTimeout(()=>{{if(rpcPending[id]){{delete rpcPending[id];reject(new Error('rpc timeout'));}}}},30000);
}});}}

function handleEvent(pld){{
  const data=pld.data||{{}}, stream=pld.stream||'', phase=data.phase||'', kind=data.kind||'';
  if(stream==='assistant'){{
    if(data.text!=null){{
    if(stopRequested)return;
    const fullText=data.text||'';
    if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}
    currentAssistant.streaming=true;
    let last=currentAssistant.blocks.length>0?currentAssistant.blocks[currentAssistant.blocks.length-1]:null;
    if(!last||last.type!=='text'){{last={{type:'text',text:''}};currentAssistant.blocks.push(last);}}
    last.text=fullText;streamRender();return;
    }}
    if(data.thinking!=null){{
      const th=data.thinking||'';
      if(th&&(!currentAssistant||currentAssistant.blocks.length===0||currentAssistant.blocks[currentAssistant.blocks.length-1].type!=='thinking')){{
        if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}
        currentAssistant.blocks.push({{type:'thinking',text:th}});
        render();
      }}
      return;
    }}
    return;
  }}
  if(stream==='lifecycle'){{
    if(phase==='start'){{streamBuf='';stopRequested=false;if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}}}
    else if(phase==='end'){{
      streamBuf='';stopRequested=false;
      var finished=currentAssistant;
      if(finished){{finished.streaming=false;
        // 持久化
        var txt='';for(var i=0;i<finished.blocks.length;i++)if(finished.blocks[i].type==='text')txt=finished.blocks[i].text;
        if(txt)saveMsg('assistant',txt);
      }}
      showStatus('完成','ok');document.getElementById('stopBtn').style.display='none';setSendMode();
      render();
      // 二次渲染确保 markdown 在 streaming=false 后生效
      if(finished)setTimeout(function(){{render();}},100);
    }}
    return;
  }}
  if(stream==='tool'){{
    const tcid=data.toolCallId||'';
    if(phase==='result'){{const card=getCardByTcId(tcid,'tool');if(card){{card.result=data.result||'';card.status='done';}}}}
    if(phase==='result')render();return;
  }}
  if(stream==='item'){{
    if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}
    const itemId=data.itemId||'', tcid=data.toolCallId||'';
    if(phase==='start'){{const card={{type:'tool_use',name:data.name||'tool',id:itemId,tcid,kind,status:'running',title:data.title||'',result:''}};currentAssistant.blocks.push(card);currentToolCards[itemId]=card;}}
    else if(phase==='update'){{const card=currentToolCards[itemId];if(card){{if(kind==='command'&&data.progressText)card.result=(card.result||'')+data.progressText;else if(data.progressText)card.result=data.progressText;}}}}
    else if(phase==='end'){{const card=currentToolCards[itemId];if(card){{card.status='done';if(data.summary)card.result=data.summary;}}}}
    render();return;
  }}
  if(stream==='command_output'){{const itemId=data.itemId||'', card=currentToolCards[itemId];if(card&&data.output){{if(phase==='delta')card.result=(card.result||'')+data.output;else if(phase==='end')card.result=data.output;}}render();return;}}
}}

function getCardByTcId(tcid,kind){{for(const k in currentToolCards){{const c=currentToolCards[k];if(c.tcid===tcid&&c.kind===kind)return c;}}return null;}}

function fmtTime(ts){{try{{const d=new Date(ts);return ('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2);}}catch(e){{return'';}}}}

async function doSend(){{
  const ta=document.getElementById('input'), text=ta.value.trim();if(!text)return;
  // 斜杠命令
  if(text==='/new'){{newChat();ta.value='';return;}}
  if(text==='/stop'){{stop();ta.value='';return;}}
  if(text==='/clear'){{messages.length=0;render();ta.value='';return;}}
  messages.push({{role:'user',stamp:fmtTime(new Date().toISOString()),blocks:[{{type:'text',text}}]}});
  saveMsg('user',text);
  streamBuf='';stopRequested=false;currentAssistant=null;currentToolCards={{}};render();
  ta.value='';autoResize(ta);
  if(!wsReady){{showStatus('未连接','warn');return;}}
  try{{
    showStatus('发送...','warn');
    if(sessKey&&!sessCreated){{await rpc('sessions.create',{{key:sessKey}});sessCreated=true;}}
    await rpc('sessions.send',{{key:sessKey,message:text}});
    showStatus('已发送','ok');
  }}catch(e){{showStatus('发送失败','warn');return;}}
  document.getElementById('stopBtn').style.display='inline-flex';setStopMode();
  // 更新侧边栏标题
  updateSessTitle(text);
}}

function doCmd(cmd){{if(cmd==='new')newChat();else if(cmd==='stop')stop();else if(cmd==='clear'){{messages.length=0;render();}}}}
function stop(){{stopRequested=true;if(wsReady)rpc('sessions.abort',{{key:sessKey}}).catch(()=>{{}});}}

function newChat(){{
  if(currentAssistant&&currentAssistant.streaming)return;
  curSessId='ocmgr_'+Math.random().toString(36).substr(2,12);sessKey=curSessId;
  window.chrome.webview.postMessage(JSON.stringify({{type:'set-sesskey',sessKey:curSessId}}));
  sessCreated=true;messages.length=0;currentAssistant=null;currentToolCards={{}};streamBuf='';stopRequested=false;
  render();
  sessions.unshift({{id:curSessId,key:curSessId,title:'新对话',sub:'',time:new Date().toLocaleTimeString('zh-CN',{{hour:'2-digit',minute:'2-digit'}})}});
  collapseSidebar();
  renderSS();
  window.chrome.webview.postMessage(JSON.stringify({{type:'new-session',sessKey}}));
  if(wsReady&&sessKey)rpc('sessions.create',{{key:sessKey}}).then(()=>{{sessCreated=true;}}).catch(()=>{{}});
}}

function switchSess(key, id){{
  curSessId=id||key;curSessGuid=id||key;sessKey=key;
  sessCreated=false;
  messages.length=0;currentAssistant=null;currentToolCards={{}};streamBuf='';
  window.chrome.webview.postMessage(JSON.stringify({{type:'set-sesskey',sessKey:key,sessGuid:id||key}}));
  window.chrome.webview.postMessage(JSON.stringify({{type:'load-msgs',sessKey:id||key}}));
  collapseSidebar();
  renderSS();render();
}}

function updateSessTitle(text){{
  var t=text.length>30?text.substring(0,30)+'...':text;
  // 多策略匹配 + 即时渲染
  for(var i=0;i<sessions.length;i++){{
    var s=sessions[i];
    if(s.key===sessKey||s.id===sessKey||s.key===curSessId||s.id===curSessId||s.id===curSessGuid){{
      s.title=t;renderSS();break;
    }}
  }}
  window.chrome.webview.postMessage(JSON.stringify({{type:'update-title',sessKey,t}}));
}}

function renderSS(){{
  var html='';
  for(var i=0;i<sessions.length;i++){{
    var s=sessions[i], active=s.key===sessKey||s.id===sessKey||s.key===curSessId||s.id===curSessId||s.id===curSessGuid;
    html+='<div class=""sess-item'+(active?' active':'')+'"" onclick=""switchSess(\''+s.key+'\',\''+s.id+'\')"">';
    html+='<div class=""sess-item-title"">'+escHtml(s.title||'新对话')+'</div>';
    if(s.sub)html+='<div class=""sess-item-sub"">'+escHtml(s.sub)+'</div>';
    html+='<div class=""sess-item-time"">'+s.time+'</div>';
    html+='<button class=""sess-item-del"" onclick=""event.stopPropagation();delSess(\''+s.id+'\')"">×</button>';
    html+='</div>';
  }}
  document.getElementById('sessList').innerHTML=html;
}}

function delSess(id){{
  window.chrome.webview.postMessage(JSON.stringify({{type:'del-sess',sessKey:id}}));
  sessions=sessions.filter(function(s){{return s.id!==id;}});
  if(curSessId===id){{if(sessions.length>0)switchSess(sessions[0].id);else newChat();}}
  renderSS();
}}

function toggleSidebar(){{
  var sb=document.getElementById('sidebar');
  if(sb)sb.classList.toggle('collapsed');
}}
function collapseSidebar(){{
  var sb=document.getElementById('sidebar');
  if(sb)sb.classList.add('collapsed');
}}
// 启动时绑定 ☰ 按钮 + 主区域点击折叠
setTimeout(function(){{
  var tb=document.querySelector('.header-toggle');
  if(tb){{tb.onclick=toggleSidebar;}}
  var ma=document.querySelector('.main-area');
  if(ma){{ma.addEventListener('click',collapseSidebar);}}
}},200);

function toggleFilter(type){{
  var val;
  if(type==='thinking'){{showThinking=!showThinking;val=showThinking;document.getElementById('fThink').classList.toggle('on',showThinking);}}
  else if(type==='tools'){{showTools=!showTools;val=showTools;document.getElementById('fTools').classList.toggle('on',showTools);}}
  else if(type==='schedule'){{showSchedule=!showSchedule;val=showSchedule;document.getElementById('fSched').classList.toggle('on',showSchedule);}}
  try{{localStorage.setItem('ocmgr_filters',JSON.stringify({{think:showThinking,tools:showTools,sched:showSchedule}}));}}catch(e){{}}
  window.chrome.webview.postMessage(JSON.stringify({{type:'save-filter',key:type,value:val}}));
  render();
}}

// 初始应用过滤按钮状态
(function(){{
  if(!showThinking)document.getElementById('fThink').classList.remove('on');
  if(!showTools)document.getElementById('fTools').classList.remove('on');
  if(!showSchedule)document.getElementById('fSched').classList.remove('on');
}})();

// ====== 渲染 ======
function render(){{
  try{{
  let html='';
  for(const msg of messages){{
    if(!msg.blocks||msg.blocks.length===0)continue;
    const isUser=msg.role==='user';
    // 过滤
    var hasText=false, hasTool=false;
    for(const b of msg.blocks){{if(b.type==='text'||(b.type==='thinking'&&showThinking))hasText=true;if(b.type==='tool_use')hasTool=true;}}
    if(!showThinking&&!isUser){{/* skip thinking */}} // handled in block loop
    if(!showTools&&hasTool&&!hasText)continue;
    if(!showSchedule&&hasTool&&!hasText){{/* skip cron */}}

    // 工具卡片先渲染
    if(showTools||isUser){{
      var bidx=0;
      for(const b of msg.blocks){{
        if(b.type==='tool_use'||b.type==='tool_result'){{
          // 定时过滤：跳过 cron 相关工具
          if(!showSchedule && b.name && (b.name==='cron'||b.name==='schedule')) continue;
          html+='<div class=""chat-line assistant""><div class=""chat-content"">';
          if(b.type==='tool_use')html+=renderToolCard(b,bidx++);
          else html+='<div class=""chat-tool-result"">'+escHtml(typeof b.text==='string'?b.text:JSON.stringify(b.text||''))+'</div>';
          html+='</div></div>';
        }}
      }}
    }}

    // 文本气泡
    if(hasText){{
      const senderName=isUser?CONFIG.userName:CONFIG.aiName;
      html+='<div class=""chat-line '+(isUser?'user':'assistant')+'"">';
      html+='<div class=""chat-avatar-row"">';
      var avHtml='';
      if(!isUser&&CONFIG.aiAvatar)avHtml='<img src=""'+CONFIG.aiAvatar+'"" style=""width:100%;height:100%;border-radius:var(--radius-md);object-fit:cover"">';
      else if(isUser&&CONFIG.userEmoji)avHtml=CONFIG.userEmoji;
      else avHtml=senderName[0];
      html+='<div class=""chat-avatar'+(isUser?' user':' assistant')+'"">'+avHtml+'</div>';
      html+='<span class=""chat-sender-name"">'+senderName+'</span>';
      html+='<span class=""chat-stamp"">'+msg.stamp+'</span>';
      html+='</div>';
      html+='<div class=""chat-content""><div class=""chat-bubble"">';
      if(!isUser){{var ct='';for(const b of msg.blocks)if(b.type==='text')ct+=b.text;if(ct)html+='<button class=""copy-btn"" data-text=""'+escAttr(ct)+'"" onclick=""copyMsg(this)"">复制</button>';}}
      for(const b of msg.blocks){{
        if(b.type==='text'){{const isStreaming=msg===currentAssistant&&currentAssistant.streaming;if(isStreaming)html+='<div class=""chat-text"" id=""streamText"" style=""white-space:pre-wrap"">'+escHtml(b.text)+'</div>';else html+='<div class=""chat-text"">'+md(b.text)+'</div>';}}
        else if(b.type==='thinking'&&showThinking)html+='<details class=""chat-thinking""><summary>💭 思考过程</summary><div class=""chat-text"">'+md(b.text)+'</div></details>';
      }}
      html+='</div></div></div>';
    }}
  }}
  if(currentAssistant&&currentAssistant.streaming&&(!currentAssistant.blocks||currentAssistant.blocks.length===0)){{
    var rdAv=CONFIG.aiAvatar?'<img src=""'+CONFIG.aiAvatar+'"" style=""width:100%;height:100%;border-radius:var(--radius-md);object-fit:cover"">':CONFIG.aiName[0];
    html+='<div class=""chat-line assistant""><div class=""chat-avatar-row""><div class=""chat-avatar assistant"">'+rdAv+'</div><span class=""chat-sender-name"">'+CONFIG.aiName+'</span></div><div class=""chat-content""><div class=""chat-reading-indicator""><div class=""chat-reading-indicator__dots""><span></span><span></span><span></span></div></div></div></div>';
  }}
  html+='<div id=""chatEnd""></div>';
  document.getElementById('msgList').innerHTML=html;scrollBottom();
  }}catch(e){{showStatus('render err','warn');}}
}}

function streamRender(){{
  if(!currentAssistant||!currentAssistant.streaming)return;
  const last=currentAssistant.blocks.length>0?currentAssistant.blocks[currentAssistant.blocks.length-1]:null;
  if(last&&last.type==='text'){{const el=document.getElementById('streamText');if(el){{el.textContent=last.text;el.style.whiteSpace='pre-wrap';scrollBottom();return;}}}}
  render();
}}

window._tcSeq=window._tcSeq||0;function renderToolCard(b,idx){{
  const running=b.status==='running', isCmd=b.kind==='command';
  const icon=getToolIcon(b.name), cardId='tc_'+(window._tcSeq++);
  let h='<div class=""chat-tool-card'+(running?' tool-running':'')+'"" id=""'+cardId+'"" onclick=""toggleToolCard(\''+cardId+'\',event)"">';
  h+='<div class=""chat-tool-card__header"">';
  h+='<div class=""chat-tool-card__title""><span class=""chat-tool-card__icon"">'+icon+'</span><span class=""name"">'+(isCmd?'命令: ':'')+escHtml(b.name)+'</span></div>';
  h+='<div class=""chat-tool-card__actions"">';
  h+='<span class=""chat-tool-card__status'+(running?' running':' ok')+'""><span class=""chat-tool-card__status-dot""></span>'+(running?'运行中':'')+'</span>';
  h+='<button class=""chat-tool-card__action-btn"" onclick=""event.stopPropagation();toggleToolCard(\''+cardId+'\')""><span class=""arrow"">▼</span></button>';
  h+='</div></div><div class=""chat-tool-card__body"">';
  if(b.title)h+='<div class=""chat-tool-card__detail"">'+escHtml(b.title)+'</div>';
  if(b.result){{h+='<div class=""chat-tool-card__block-header"">'+(isCmd?'输出':'结果')+'</div>';h+='<div class=""chat-tool-card__output"">'+escHtml(b.result)+'</div>';}}
  h+='</div></div>';return h;
}}

function toggleToolCard(id,e){{if(e)e.stopPropagation();var el=document.getElementById(id);if(el)el.classList.toggle('chat-tool-card--expanded');}}
function getToolIcon(name){{const icons={{exec:'▶','read':'📄','write':'✏','bash':'💻','web_search':'🔍','web_fetch':'🌐','grep':'🔎','glob':'📁','edit':'✂','memory':'🧠','sessions':'💬','cron':'⏰','process':'⚙'}};return icons[name]||'🔧';}}

// ====== Markdown ======
function md(text){{if(!text)return'';try{{if(typeof marked!=='undefined')return marked.parse(text,{{breaks:true,gfm:true}});}}catch(e){{}}return escHtml(text).replace(/\n\n/g,'<br><br>').replace(/\n/g,'<br>');}}
function escHtml(s){{return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}}
function escAttr(s){{return String(s).replace(/&/g,'&amp;').replace(/""/g,'&quot;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}}
function copyMsg(btn){{const text=btn.getAttribute('data-text')||'';navigator.clipboard.writeText(text).then(()=>{{btn.textContent='已复制';btn.classList.add('copied');setTimeout(()=>{{btn.textContent='复制';btn.classList.remove('copied');}},2000);}}).catch(()=>{{}});}}

function scrollBottom(){{const end=document.getElementById('chatEnd');if(end)end.scrollIntoView({{behavior:'smooth',block:'end'}});}}
function showStatus(text,cls){{const el=document.getElementById('status');if(el){{el.title=text;el.className='status-dot '+(cls||'');}}}}
function autoResize(ta){{ta.style.height='auto';ta.style.height=Math.min(ta.scrollHeight,150)+'px';}}

// ====== 工具栏 ======
let thinkingMode=false;
function toggleThinking(){{thinkingMode=!thinkingMode;document.getElementById('thinkToggle').classList.toggle('active',thinkingMode);}}
function pickImage(){{
  var url=prompt('输入图片 URL:');
  if(url){{
    var ta=document.getElementById('input');
    ta.value+='![]('+url+')\n';
    ta.focus();autoResize(ta);
  }}
}}
function pickFile(){{
  window.chrome.webview.postMessage(JSON.stringify({{type:'pick-file'}}));
}}
window.onFilePicked=function(name){{
  var ta=document.getElementById('input');
  ta.value+='📄 '+name+'\n';
  ta.focus();autoResize(ta);
}};

function setStopMode(){{var btn=document.getElementById('sendBtn');btn.textContent='⏹';btn.className='send-btn stop-btn';btn.onclick=stop;}}
function setSendMode(){{var btn=document.getElementById('sendBtn');btn.textContent='➤';btn.className='send-btn';btn.onclick=doSend;}}

document.addEventListener('keydown',e=>{{if(e.key==='Enter'&&!e.shiftKey&&document.activeElement===document.getElementById('input')){{e.preventDefault();doSend();}}}});
</script>

<script>
// ====== 配置 ======
const CONFIG = {{
  model: '{8}',
  aiName: '{2}', aiEmoji: '{3}', aiAvatar: '{6}',
  userName: '{4}', userEmoji: '{5}',
  sessKey: '{7}', theme: '{0}'
}};

// ====== 状态 ======
let rpcSeq=10, rpcPending={{}}, sessKey, sessCreated, streamBuf='', stopRequested=false;
let messages=[], currentAssistant=null, currentToolCards={{}}, streamTimer=0;
let wsReady=false, sessions=[], curSessId='', curSessGuid='';
let showThinking=true, showTools=true, showSchedule=true;

// ====== 桥接 ======
function wsSend(raw){{window.chrome.webview.postMessage(JSON.stringify({{type:'ws-send',data:raw}}));}}
function saveMsg(role,text){{
  // Gateway 自动持久化到 JSONL，不需要手动写
}}

window.handleWsMsg=function(raw){{
  try{{handleMsg(typeof raw==='string'?JSON.parse(raw):raw);}}catch(e){{}}
}};

// C# 动态更新身份
window.updateIdentity=function(aiName,aiEmoji,userName,userEmoji,aiAvatar){{
  CONFIG.aiName=aiName||CONFIG.aiName;
  CONFIG.aiEmoji=aiEmoji||CONFIG.aiEmoji;
  CONFIG.userName=userName||CONFIG.userName;
  CONFIG.userEmoji=userEmoji||CONFIG.userEmoji;
  if(aiAvatar!==undefined)CONFIG.aiAvatar=aiAvatar;
  render();
}};

// C# 过滤按钮回调
window.setFilter=function(type,val){{
  if(type==='💭'){{showThinking=val;}}
  else if(type==='🔧'){{showTools=val;}}
  else if(type==='⏰'){{showSchedule=val;}}
  render();
}};

// 接收 C# 消息（WebView2 专用通道）
window.chrome.webview.addEventListener('message', function(e){{
  try{{
    var m=typeof e.data==='string'?JSON.parse(e.data):e.data;
    if(m.type==='append-thinking'){{
      var found=currentAssistant;
      if(!found){{for(var i=messages.length-1;i>=0;i--){{
        if(messages[i].role=='assistant'){{found=messages[i];break;}}
      }}}}
      if(found){{for(var j=0;j<m.blocks.length;j++){{found.blocks.unshift({{type:'thinking',text:m.blocks[j]}});}}}}render();
      return;
    }}
    if(m.type==='sessions'){{
      sessions=m.list||[];renderSS();
      // 仅首次加载时切换会话，避免刷新列表时重复 switch 重置状态
      if(!curSessId){{
        var found=sessions.find(function(s){{return s.id===CONFIG.sessKey;}});
        if(found)switchSess(found.key, found.id);
        else if(sessions.length>0)switchSess(sessions[0].key, sessions[0].id);
        else newChat();
      }}
    }}
    else if(m.type==='msgs'){{messages=m.list||[];render();}}
  }}catch(ex){{}}
}});

// 启动
curSessId='';sessKey=CONFIG.sessKey||'';
document.documentElement.setAttribute('data-theme-mode',CONFIG.theme);
window.chrome.webview.postMessage(JSON.stringify({{type:'connect',sessKey:sessKey}}));
setSendMode();

// ====== handleMsg / handleEvent / streamRender / render 等 (同前) ======
function handleMsg(msg){{
  if(msg.type==='event'){{
    if(msg.event==='agent'){{
      const pld=msg.payload, sk=msg.sessionKey||(pld?pld.sessionKey:'')||'';
      if(sk&&sessKey&&!sk.endsWith(sessKey))return;
      if(pld)handleEvent(pld);
    }}
    return;
  }}
  if(msg.type==='res'){{
    const id=String(msg.id||'');
    if(id==='1')showStatus(msg.ok?'connected':'connect fail',msg.ok?'ok':'warn');
    if(id==='1'&&msg.ok){{wsReady=true;
      // sessions.create deferred to newChat or send handler
    }}
    if(rpcPending[id]){{rpcPending[id](msg);delete rpcPending[id];}}
  }}
}}

function rpc(method,params){{return new Promise((resolve,reject)=>{{
  const id=++rpcSeq;rpcPending[id]=resolve;
  wsSend(JSON.stringify({{type:'req',id:String(id),method,params}}));
  setTimeout(()=>{{if(rpcPending[id]){{delete rpcPending[id];reject(new Error('rpc timeout'));}}}},30000);
}});}}

function handleEvent(pld){{
  const data=pld.data||{{}}, stream=pld.stream||'', phase=data.phase||'', kind=data.kind||'';
  if(stream==='assistant'&&data.text!=null){{
    if(stopRequested)return;
    const fullText=data.text||'';
    if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}
    currentAssistant.streaming=true;
    let last=currentAssistant.blocks.length>0?currentAssistant.blocks[currentAssistant.blocks.length-1]:null;
    if(!last||last.type!=='text'){{last={{type:'text',text:''}};currentAssistant.blocks.push(last);}}
    last.text=fullText;streamRender();return;
  }}
  if(stream==='lifecycle'){{
    if(phase==='start'){{streamBuf='';stopRequested=false;if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}}}
    else if(phase==='end'){{
      streamBuf='';stopRequested=false;
      var finished=currentAssistant;
      if(finished){{finished.streaming=false;
        // 持久化
        var txt='';for(var i=0;i<finished.blocks.length;i++)if(finished.blocks[i].type==='text')txt=finished.blocks[i].text;
        if(txt)saveMsg('assistant',txt);
      }}
      showStatus('完成','ok');document.getElementById('stopBtn').style.display='none';setSendMode();
      render();
      // 二次渲染确保 markdown 在 streaming=false 后生效
      if(finished)setTimeout(function(){{render();}},100);
    }}
    return;
  }}
  if(stream==='tool'){{
    const tcid=data.toolCallId||'';
    if(phase==='result'){{const card=getCardByTcId(tcid,'tool');if(card){{card.result=data.result||'';card.status='done';}}}}
    if(phase==='result')render();return;
  }}
  if(stream==='item'){{
    if(!currentAssistant){{currentAssistant={{role:'assistant',stamp:fmtTime(new Date().toISOString()),blocks:[],streaming:true}};messages.push(currentAssistant);}}
    const itemId=data.itemId||'', tcid=data.toolCallId||'';
    if(phase==='start'){{const card={{type:'tool_use',name:data.name||'tool',id:itemId,tcid,kind,status:'running',title:data.title||'',result:''}};currentAssistant.blocks.push(card);currentToolCards[itemId]=card;}}
    else if(phase==='update'){{const card=currentToolCards[itemId];if(card){{if(kind==='command'&&data.progressText)card.result=(card.result||'')+data.progressText;else if(data.progressText)card.result=data.progressText;}}}}
    else if(phase==='end'){{const card=currentToolCards[itemId];if(card){{card.status='done';if(data.summary)card.result=data.summary;}}}}
    render();return;
  }}
  if(stream==='command_output'){{const itemId=data.itemId||'', card=currentToolCards[itemId];if(card&&data.output){{if(phase==='delta')card.result=(card.result||'')+data.output;else if(phase==='end')card.result=data.output;}}render();return;}}
}}

function getCardByTcId(tcid,kind){{for(const k in currentToolCards){{const c=currentToolCards[k];if(c.tcid===tcid&&c.kind===kind)return c;}}return null;}}

function fmtTime(ts){{try{{const d=new Date(ts);return ('0'+d.getHours()).slice(-2)+':'+('0'+d.getMinutes()).slice(-2);}}catch(e){{return'';}}}}

async function doSend(){{
  const ta=document.getElementById('input'), text=ta.value.trim();if(!text)return;
  // 斜杠命令
  if(text==='/new'){{newChat();ta.value='';return;}}
  if(text==='/stop'){{stop();ta.value='';return;}}
  if(text==='/clear'){{messages.length=0;render();ta.value='';return;}}
  messages.push({{role:'user',stamp:fmtTime(new Date().toISOString()),blocks:[{{type:'text',text}}]}});
  saveMsg('user',text);
  streamBuf='';stopRequested=false;currentAssistant=null;currentToolCards={{}};render();
  ta.value='';autoResize(ta);
  if(!wsReady){{showStatus('未连接','warn');return;}}
  try{{
    showStatus('发送...','warn');
    if(sessKey&&!sessCreated){{await rpc('sessions.create',{{key:sessKey}});sessCreated=true;}}
    await rpc('sessions.send',{{key:sessKey,message:text}});
    showStatus('已发送','ok');
  }}catch(e){{showStatus('发送失败','warn');return;}}
  document.getElementById('stopBtn').style.display='inline-flex';setStopMode();
  // 更新侧边栏标题
  updateSessTitle(text);
}}

function doCmd(cmd){{if(cmd==='new')newChat();else if(cmd==='stop')stop();else if(cmd==='clear'){{messages.length=0;render();}}}}
function stop(){{stopRequested=true;if(wsReady)rpc('sessions.abort',{{key:sessKey}}).catch(()=>{{}});}}

function newChat(){{
  if(currentAssistant&&currentAssistant.streaming)return;
  curSessId='ocmgr_'+Math.random().toString(36).substr(2,12);sessKey=curSessId;
  window.chrome.webview.postMessage(JSON.stringify({{type:'set-sesskey',sessKey:curSessId}}));
  sessCreated=true;messages.length=0;currentAssistant=null;currentToolCards={{}};streamBuf='';stopRequested=false;
  render();
  sessions.unshift({{id:curSessId,key:curSessId,title:'新对话',sub:'',time:new Date().toLocaleTimeString('zh-CN',{{hour:'2-digit',minute:'2-digit'}})}});
  collapseSidebar();
  renderSS();
  window.chrome.webview.postMessage(JSON.stringify({{type:'new-session',sessKey}}));
  if(wsReady&&sessKey)rpc('sessions.create',{{key:sessKey}}).then(()=>{{sessCreated=true;}}).catch(()=>{{}});
}}

function switchSess(key, id){{
  curSessId=id||key;curSessGuid=id||key;sessKey=key;
  sessCreated=false;
  messages.length=0;currentAssistant=null;currentToolCards={{}};streamBuf='';
  window.chrome.webview.postMessage(JSON.stringify({{type:'set-sesskey',sessKey:key,sessGuid:id||key}}));
  window.chrome.webview.postMessage(JSON.stringify({{type:'load-msgs',sessKey:id||key}}));
  collapseSidebar();
  renderSS();render();
}}

function updateSessTitle(text){{
  var t=text.length>30?text.substring(0,30)+'...':text;
  // 多策略匹配 + 即时渲染
  for(var i=0;i<sessions.length;i++){{
    var s=sessions[i];
    if(s.key===sessKey||s.id===sessKey||s.key===curSessId||s.id===curSessId||s.id===curSessGuid){{
      s.title=t;renderSS();break;
    }}
  }}
  window.chrome.webview.postMessage(JSON.stringify({{type:'update-title',sessKey,t}}));
}}

function renderSS(){{
  var html='';
  for(var i=0;i<sessions.length;i++){{
    var s=sessions[i], active=s.key===sessKey||s.id===sessKey||s.key===curSessId||s.id===curSessId||s.id===curSessGuid;
    html+='<div class=""sess-item'+(active?' active':'')+'"" onclick=""switchSess(\''+s.key+'\',\''+s.id+'\')"">';
    html+='<div class=""sess-item-title"">'+escHtml(s.title||'新对话')+'</div>';
    if(s.sub)html+='<div class=""sess-item-sub"">'+escHtml(s.sub)+'</div>';
    html+='<div class=""sess-item-time"">'+s.time+'</div>';
    html+='<button class=""sess-item-del"" onclick=""event.stopPropagation();delSess(\''+s.id+'\')"">×</button>';
    html+='</div>';
  }}
  document.getElementById('sessList').innerHTML=html;
}}

function delSess(id){{
  window.chrome.webview.postMessage(JSON.stringify({{type:'del-sess',sessKey:id}}));
  sessions=sessions.filter(function(s){{return s.id!==id;}});
  if(curSessId===id){{if(sessions.length>0)switchSess(sessions[0].id);else newChat();}}
  renderSS();
}}

function toggleSidebar(){{
  var sb=document.getElementById('sidebar');
  if(sb)sb.classList.toggle('collapsed');
}}
function collapseSidebar(){{
  var sb=document.getElementById('sidebar');
  if(sb)sb.classList.add('collapsed');
}}
// 启动时绑定 ☰ 按钮 + 主区域点击折叠
setTimeout(function(){{
  var tb=document.querySelector('.header-toggle');
  if(tb){{tb.onclick=toggleSidebar;}}
  var ma=document.querySelector('.main-area');
  if(ma){{ma.addEventListener('click',collapseSidebar);}}
}},200);

function toggleFilter(type){{
  var val;
  if(type==='thinking'){{showThinking=!showThinking;val=showThinking;document.getElementById('fThink').classList.toggle('on',showThinking);}}
  else if(type==='tools'){{showTools=!showTools;val=showTools;document.getElementById('fTools').classList.toggle('on',showTools);}}
  else if(type==='schedule'){{showSchedule=!showSchedule;val=showSchedule;document.getElementById('fSched').classList.toggle('on',showSchedule);}}
  try{{localStorage.setItem('ocmgr_filters',JSON.stringify({{think:showThinking,tools:showTools,sched:showSchedule}}));}}catch(e){{}}
  window.chrome.webview.postMessage(JSON.stringify({{type:'save-filter',key:type,value:val}}));
  render();
}}

// 初始应用过滤按钮状态
(function(){{
  if(!showThinking)document.getElementById('fThink').classList.remove('on');
  if(!showTools)document.getElementById('fTools').classList.remove('on');
  if(!showSchedule)document.getElementById('fSched').classList.remove('on');
}})();

// ====== 渲染 ======
function render(){{
  try{{
  let html='';
  for(const msg of messages){{
    if(!msg.blocks||msg.blocks.length===0)continue;
    const isUser=msg.role==='user';
    // 过滤
    var hasText=false, hasTool=false;
    for(const b of msg.blocks){{if(b.type==='text'||(b.type==='thinking'&&showThinking))hasText=true;if(b.type==='tool_use')hasTool=true;}}
    if(!showThinking&&!isUser){{/* skip thinking */}} // handled in block loop
    if(!showTools&&hasTool&&!hasText)continue;
    if(!showSchedule&&hasTool&&!hasText){{/* skip cron */}}

    // 工具卡片先渲染
    if(showTools||isUser){{
      var bidx=0;
      for(const b of msg.blocks){{
        if(b.type==='tool_use'||b.type==='tool_result'){{
          // 定时过滤：跳过 cron 相关工具
          if(!showSchedule && b.name && (b.name==='cron'||b.name==='schedule')) continue;
          html+='<div class=""chat-line assistant""><div class=""chat-content"">';
          if(b.type==='tool_use')html+=renderToolCard(b,bidx++);
          else html+='<div class=""chat-tool-result"">'+escHtml(typeof b.text==='string'?b.text:JSON.stringify(b.text||''))+'</div>';
          html+='</div></div>';
        }}
      }}
    }}

    // 文本气泡
    if(hasText){{
      const senderName=isUser?CONFIG.userName:CONFIG.aiName;
      html+='<div class=""chat-line '+(isUser?'user':'assistant')+'"">';
      html+='<div class=""chat-avatar-row"">';
      var avHtml='';
      if(!isUser&&CONFIG.aiAvatar)avHtml='<img src=""'+CONFIG.aiAvatar+'"" style=""width:100%;height:100%;border-radius:var(--radius-md);object-fit:cover"">';
      else if(isUser&&CONFIG.userEmoji)avHtml=CONFIG.userEmoji;
      else avHtml=senderName[0];
      html+='<div class=""chat-avatar'+(isUser?' user':' assistant')+'"">'+avHtml+'</div>';
      html+='<span class=""chat-sender-name"">'+senderName+'</span>';
      html+='<span class=""chat-stamp"">'+msg.stamp+'</span>';
      html+='</div>';
      html+='<div class=""chat-content""><div class=""chat-bubble"">';
      if(!isUser){{var ct='';for(const b of msg.blocks)if(b.type==='text')ct+=b.text;if(ct)html+='<button class=""copy-btn"" data-text=""'+escAttr(ct)+'"" onclick=""copyMsg(this)"">复制</button>';}}
      for(const b of msg.blocks){{
        if(b.type==='text'){{const isStreaming=msg===currentAssistant&&currentAssistant.streaming;if(isStreaming)html+='<div class=""chat-text"" id=""streamText"" style=""white-space:pre-wrap"">'+escHtml(b.text)+'</div>';else html+='<div class=""chat-text"">'+md(b.text)+'</div>';}}
        else if(b.type==='thinking'&&showThinking)html+='<details class=""chat-thinking""><summary>💭 思考过程</summary><div class=""chat-text"">'+md(b.text)+'</div></details>';
      }}
      html+='</div></div></div>';
    }}
  }}
  if(currentAssistant&&currentAssistant.streaming&&(!currentAssistant.blocks||currentAssistant.blocks.length===0)){{
    var rdAv=CONFIG.aiAvatar?'<img src=""'+CONFIG.aiAvatar+'"" style=""width:100%;height:100%;border-radius:var(--radius-md);object-fit:cover"">':CONFIG.aiName[0];
    html+='<div class=""chat-line assistant""><div class=""chat-avatar-row""><div class=""chat-avatar assistant"">'+rdAv+'</div><span class=""chat-sender-name"">'+CONFIG.aiName+'</span></div><div class=""chat-content""><div class=""chat-reading-indicator""><div class=""chat-reading-indicator__dots""><span></span><span></span><span></span></div></div></div></div>';
  }}
  html+='<div id=""chatEnd""></div>';
  document.getElementById('msgList').innerHTML=html;scrollBottom();
  }}catch(e){{showStatus('render err','warn');}}
}}

function streamRender(){{
  if(!currentAssistant||!currentAssistant.streaming)return;
  const last=currentAssistant.blocks.length>0?currentAssistant.blocks[currentAssistant.blocks.length-1]:null;
  if(last&&last.type==='text'){{const el=document.getElementById('streamText');if(el){{el.textContent=last.text;el.style.whiteSpace='pre-wrap';scrollBottom();return;}}}}
  render();
}}

function renderToolCard(b,idx){{
  const running=b.status==='running', isCmd=b.kind==='command';
  const icon=getToolIcon(b.name), cardId='tc_'+(window._tcSeq++);
  let h='<div class=""chat-tool-card'+(running?' tool-running':'')+'"" id=""'+cardId+'"" onclick=""toggleToolCard(\''+cardId+'\',event)"">';
  h+='<div class=""chat-tool-card__header"">';
  h+='<div class=""chat-tool-card__title""><span class=""chat-tool-card__icon"">'+icon+'</span><span class=""name"">'+(isCmd?'命令: ':'')+escHtml(b.name)+'</span></div>';
  h+='<div class=""chat-tool-card__actions"">';
  h+='<span class=""chat-tool-card__status'+(running?' running':' ok')+'""><span class=""chat-tool-card__status-dot""></span>'+(running?'运行中':'')+'</span>';
  h+='<button class=""chat-tool-card__action-btn"" onclick=""event.stopPropagation();toggleToolCard(\''+cardId+'\')""><span class=""arrow"">▼</span></button>';
  h+='</div></div><div class=""chat-tool-card__body"">';
  if(b.title)h+='<div class=""chat-tool-card__detail"">'+escHtml(b.title)+'</div>';
  if(b.result){{h+='<div class=""chat-tool-card__block-header"">'+(isCmd?'输出':'结果')+'</div>';h+='<div class=""chat-tool-card__output"">'+escHtml(b.result)+'</div>';}}
  h+='</div></div>';return h;
}}

function toggleToolCard(id,e){{if(e)e.stopPropagation();var el=document.getElementById(id);if(el)el.classList.toggle('chat-tool-card--expanded');}}
function getToolIcon(name){{const icons={{exec:'▶','read':'📄','write':'✏','bash':'💻','web_search':'🔍','web_fetch':'🌐','grep':'🔎','glob':'📁','edit':'✂','memory':'🧠','sessions':'💬','cron':'⏰','process':'⚙'}};return icons[name]||'🔧';}}

// ====== Markdown ======
function md(text){{if(!text)return'';try{{if(typeof marked!=='undefined')return marked.parse(text,{{breaks:true,gfm:true}});}}catch(e){{}}return escHtml(text).replace(/\n\n/g,'<br><br>').replace(/\n/g,'<br>');}}
function escHtml(s){{return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}}
function escAttr(s){{return String(s).replace(/&/g,'&amp;').replace(/""/g,'&quot;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}}
function copyMsg(btn){{const text=btn.getAttribute('data-text')||'';navigator.clipboard.writeText(text).then(()=>{{btn.textContent='已复制';btn.classList.add('copied');setTimeout(()=>{{btn.textContent='复制';btn.classList.remove('copied');}},2000);}}).catch(()=>{{}});}}

function scrollBottom(){{const end=document.getElementById('chatEnd');if(end)end.scrollIntoView({{behavior:'smooth',block:'end'}});}}
function showStatus(text,cls){{const el=document.getElementById('status');if(el){{el.title=text;el.className='status-dot '+(cls||'');}}}}
function autoResize(ta){{ta.style.height='auto';ta.style.height=Math.min(ta.scrollHeight,150)+'px';}}

// ====== 工具栏 ======
let thinkingMode=false;
function toggleThinking(){{thinkingMode=!thinkingMode;document.getElementById('thinkToggle').classList.toggle('active',thinkingMode);}}
function pickImage(){{
  var url=prompt('输入图片 URL:');
  if(url){{
    var ta=document.getElementById('input');
    ta.value+='![]('+url+')\n';
    ta.focus();autoResize(ta);
  }}
}}
function pickFile(){{
  window.chrome.webview.postMessage(JSON.stringify({{type:'pick-file'}}));
}}
window.onFilePicked=function(name){{
  var ta=document.getElementById('input');
  ta.value+='📄 '+name+'\n';
  ta.focus();autoResize(ta);
}};

function setStopMode(){{var btn=document.getElementById('sendBtn');btn.textContent='⏹';btn.className='send-btn stop-btn';btn.onclick=stop;}}
function setSendMode(){{var btn=document.getElementById('sendBtn');btn.textContent='➤';btn.className='send-btn';btn.onclick=doSend;}}

document.addEventListener('keydown',e=>{{if(e.key==='Enter'&&!e.shiftKey&&document.activeElement===document.getElementById('input')){{e.preventDefault();doSend();}}}});
</script>
</body>
</html>";

    static string _markedJs = "";

    string BuildPageHtml(string theme, string bodyBg, string aiName, string aiEmoji, string userName, string userEmoji, string aiAvatar, string sessKey)
    {
        var mk = string.IsNullOrEmpty(_markedJs) ? "<script>" + File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "marked.min.js"), Encoding.UTF8) + "</" + "script>" : "<script>" + _markedJs + "</" + "script>";
        var html = string.Format(ChatTemplate, theme, bodyBg, aiName, aiEmoji, userName, userEmoji, aiAvatar, sessKey, "__default__", mk);
        html = html.Replace("新对话", OpenClawManager.Properties.LanguageManager.GetString("ChatNewChat"))
                   .Replace("📎 添加附件", OpenClawManager.Properties.LanguageManager.GetString("ChatAttach"))
                   .Replace("＋ 新建会话", OpenClawManager.Properties.LanguageManager.GetString("ChatNewSess"))
                   .Replace("⏹ 停止", OpenClawManager.Properties.LanguageManager.GetString("ChatStop"))
                   .Replace("🗑 清屏", OpenClawManager.Properties.LanguageManager.GetString("ChatClear"))
                   .Replace("输入消息...", OpenClawManager.Properties.LanguageManager.GetString("ChatInputPlaceholder"))
                   .Replace(">复制<", ">" + OpenClawManager.Properties.LanguageManager.GetString("ChatCopy") + "<")
                   .Replace("🤔 思考过程", OpenClawManager.Properties.LanguageManager.GetString("ChatThinking"))
                   .Replace("'命令: '", "'" + OpenClawManager.Properties.LanguageManager.GetString("ChatCommand") + ": '")
                   .Replace("'运行中'", "'" + OpenClawManager.Properties.LanguageManager.GetString("ChatRunning") + "'")
                   .Replace("'输出'", "'" + OpenClawManager.Properties.LanguageManager.GetString("ChatOutput") + "'")
                   .Replace("'结果'", "'" + OpenClawManager.Properties.LanguageManager.GetString("ChatResult") + "'")
                   .Replace("发送...", OpenClawManager.Properties.LanguageManager.GetString("ChatSending"))
                   .Replace("已发送", OpenClawManager.Properties.LanguageManager.GetString("ChatSent"))
                   .Replace("发送失败", OpenClawManager.Properties.LanguageManager.GetString("ChatSendFailed"))
                   .Replace("'AI 对话'", "'" + OpenClawManager.Properties.LanguageManager.GetString("NavChat") + "'");
        return html;
    }

    void LoadMarkedJs()
    {
        try
        {
            var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "marked.min.js");
            if (File.Exists(p)) _markedJs = File.ReadAllText(p, Encoding.UTF8);
        }
        catch { }
    }
    static string _persistSessKey = ""; // 首次运行时自动生成

    ClientWebSocket? _ws;
    CancellationTokenSource? _wsCts;
    string? _sessionKey;
    bool _wsReady;
    static int _lastGoodProtocol = 5;

    string SDir => _sd ??= ResolveSDir();
    string? _sd;
    string ResolveSDir()
    {
        var d = Path.Combine(Path.GetDirectoryName(MainForm.CfgFullPath)!, "agents", "main", "sessions");
        if (!Directory.Exists(d)) d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "agents", "main", "sessions");
        return d;
    }

    static Dictionary<string, string> _keyMap = new(); // GUID → rpcKey
    static string? _pendingKey; // 新建会话时的待匹配 key
    static string MapPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sess-keys.json");
    static void LoadKeyMap() { try { if (File.Exists(MapPath)) _keyMap = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(MapPath)) ?? new(); } catch { } }
    static void SaveKeyMap() { try { File.WriteAllText(MapPath, JsonSerializer.Serialize(_keyMap)); } catch { } }

    static string FilterPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "filter-state.json");
    static bool LoadFilter(string key) { try { if (File.Exists(FilterPath)) { var d = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(FilterPath)); if (d?.TryGetValue(key, out var v) == true) return v; } } catch { } return true; }
    public static void SaveFilter(string key, bool value) { try { var d = new Dictionary<string, bool>(); if (File.Exists(FilterPath)) d = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(FilterPath)) ?? d; d[key] = value; File.WriteAllText(FilterPath, JsonSerializer.Serialize(d)); } catch { } }

    public void Build(Panel p)
    {
        body = p; body.Controls.Clear(); body.BackColor = Theme.Bg;
        _built = true;

        // 清理旧连接
        try { _wsCts?.Cancel(); _ws?.Dispose(); } catch { }
        _ws = null; _wsCts = null; _wsReady = false;

        LoadMarkedJs();

        // C# 工具栏
        BuildToolbar();

        // 主题色面板（WebView2 加载前显示，防止闪白/黑）
        bgPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.IsDark ? Color.FromArgb(0x0e, 0x10, 0x15) : Color.FromArgb(0xf5, 0xf7, 0xfa) };
        body.Controls.Add(bgPanel);

        wv = new WebView2 { Dock = DockStyle.Fill };
        bgPanel.Controls.Add(wv);
        _ = InitWV();
    }

    void BuildToolbar()
    {
        toolbar = new Panel { Height = 36, Dock = DockStyle.Top, BackColor = Theme.BgWhite, Padding = new Padding(4, 4, 4, 4) };
        body.Controls.Add(toolbar);

        toggleBtn = new Button { Text = "☰", FlatStyle = FlatStyle.Flat, BackColor = Color.Transparent, ForeColor = Theme.Fc, Font = Theme.Font(14f, FontStyle.Bold), Cursor = Cursors.Hand, Size = new Size(30, 28), Location = new Point(6, 4), FlatAppearance = { BorderSize = 0 }, TabStop = false, TextAlign = ContentAlignment.MiddleCenter };
        toggleBtn.Click += (_, _) => ToggleSidebar();
        toolbar.Controls.Add(toggleBtn);

        // 过滤按钮靠右
        _showThinking = LoadFilter("think");
        _showTools = LoadFilter("tools");
        _showSchedule = LoadFilter("sched");
        int rx = toolbar.Width - 4;
        schedBtn = MakeFilterBtn("⏰", "定时", ref rx, _showSchedule, v => { _showSchedule = v; SaveFilter("sched", v); _ = wv?.CoreWebView2?.ExecuteScriptAsync($"setFilter('⏰',{v.ToString().ToLower()})"); });
        toolsBtn = MakeFilterBtn("🔧", "工具", ref rx, _showTools, v => { _showTools = v; SaveFilter("tools", v); _ = wv?.CoreWebView2?.ExecuteScriptAsync($"setFilter('🔧',{v.ToString().ToLower()})"); });
        thinkBtn = MakeFilterBtn("💭", "思考", ref rx, _showThinking, v => { _showThinking = v; SaveFilter("think", v); _ = wv?.CoreWebView2?.ExecuteScriptAsync($"setFilter('💭',{v.ToString().ToLower()})"); });
        toolbar.Controls.Add(schedBtn);
        toolbar.Controls.Add(toolsBtn);
        toolbar.Controls.Add(thinkBtn);

        toolbar.Resize += (_, _) =>
        {
            int rrx = toolbar.Width - 8;
            schedBtn.Left = (rrx -= 44);
            toolsBtn.Left = (rrx -= 44);
            thinkBtn.Left = (rrx -= 44);
        };

        toolbar.Paint += (_, e) => e.Graphics.DrawLine(new Pen(Theme.BdrLight), 0, toolbar.Height - 1, toolbar.Width, toolbar.Height - 1);
    }

    Button MakeFilterBtn(string text, string label, ref int x, bool initialState, Action<bool> onToggle)
    {
        x -= 44;
        var btn = new Button { Text = text, FlatStyle = FlatStyle.Flat, Font = Theme.Font(14f), Cursor = Cursors.Hand, Size = new Size(42, 30), Location = new Point(x, 3), FlatAppearance = { BorderSize = 0 }, TabStop = false };
        var state = initialState;
        btn.Tag = label;
        UpdateFilterBtn(btn, state);
        btn.Click += (_, _) =>
        {
            state = !state;
            UpdateFilterBtn(btn, state);
            onToggle(state);
        };
        return btn;
    }

    void UpdateFilterBtn(Button btn, bool on)
    {
        btn.BackColor = on ? (Theme.IsDark ? Color.FromArgb(38, 63, 98) : Color.FromArgb(224, 239, 255)) : Color.Transparent;
        btn.ForeColor = Theme.Fc;
        var label = btn.Tag as string ?? "";
        new ToolTip().SetToolTip(btn, label + (on ? ": 开" : ": 关"));
    }

    void ToggleSidebar()
    {
        _ = wv?.CoreWebView2?.ExecuteScriptAsync("toggleSidebar()");
    }

    public void CollapseSidebar()
    {
        _ = wv?.CoreWebView2?.ExecuteScriptAsync("collapseSidebar()");
    }

    async Task InitWV()
    {
        try
        {
            var ud = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenClaw-Manager.exe.WebView2");
            var envOpts = new CoreWebView2EnvironmentOptions();
            var env = await CoreWebView2Environment.CreateAsync(null, ud, envOpts);
            await wv.EnsureCoreWebView2Async(env);
            wv.CoreWebView2.Settings.IsScriptEnabled = true;
            // 拦截刷新（右键菜单和快捷键都不行），但保留复制等功能
            wv.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (e.NavigationKind == CoreWebView2NavigationKind.Reload) e.Cancel = true;
            };
            SetWvBg();

            wv.CoreWebView2.WebMessageReceived += OnWebMessage;

            // 读取名字+头像
            var aiName = "AI"; var aiEmoji = "🤖"; var userName = "YOU"; var userEmoji = "👤"; var aiAvatarB64 = "";
            try
            {
                var ws = ReadWorkspacePath();
                if (!Directory.Exists(ws)) ws = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "workspace");
                if (Directory.Exists(ws))
                {
                    ReadMdField(Path.Combine(ws, "IDENTITY.md"), ref aiName, ref aiEmoji);
                    ReadMdField(Path.Combine(ws, "USER.md"), ref userName, ref userEmoji);
                }
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apple-touch-icon.png");
                if (!File.Exists(iconPath)) iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "control-ui", "apple-touch-icon.png");
                if (File.Exists(iconPath)) aiAvatarB64 = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(iconPath));
            }
            catch { }

            var markedJs = "";
            var markedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "marked.min.js");
            if (File.Exists(markedPath)) markedJs = "<script>" + File.ReadAllText(markedPath, Encoding.UTF8) + "</script>";

            // 恢复上次会话（保持同一个 key）
            if (string.IsNullOrEmpty(_persistSessKey))
                _persistSessKey = "ocmgr_" + Guid.NewGuid().ToString("N")[..12];

            var aiName2 = aiName; var aiEmoji2 = aiEmoji; var userName2 = userName; var userEmoji2 = userEmoji; var aiAvatarB64_2 = aiAvatarB64;
            var html = BuildPageHtml(Theme.IsDark ? "dark" : "light", Theme.IsDark ? "#0e1015" : "#f5f7fa", aiName2, aiEmoji2, userName2, userEmoji2, aiAvatarB64_2, _persistSessKey);

            _processedHtml = html;
            wv.NavigateToString(html);

            wv.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                await Task.Delay(500);
                ApplyTheme();
                PushIdentity(aiName, aiEmoji, userName, userEmoji, aiAvatarB64);
                // 同步 C# 过滤状态到 JS
                _ = wv.CoreWebView2.ExecuteScriptAsync($"setFilter('💭',{_showThinking.ToString().ToLower()})");
                _ = wv.CoreWebView2.ExecuteScriptAsync($"setFilter('🔧',{_showTools.ToString().ToLower()})");
                _ = wv.CoreWebView2.ExecuteScriptAsync($"setFilter('⏰',{_showSchedule.ToString().ToLower()})");
                SendSessions();
            };

            wv.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { }
            };
        }
        catch (Exception ex)
        {
            body.Controls.Clear();
            body.Controls.Add(new Label { Text = "err:\n" + ex.Message, ForeColor = Theme.Red, Font = Theme.Font(10f), AutoSize = true, BackColor = Color.Transparent, Location = new Point(20, 20) });
        }
    }

    async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var raw = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(raw)) return;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var rt = doc.RootElement;
            var type = rt.TryGetProperty("type", out var tp) ? tp.GetString() : "";

            switch (type)
            {
                case "connect":
                    _sessionKey = rt.TryGetProperty("sessKey", out var k) ? k.GetString() : "";
                    _ = Task.Run(ConnectWsLoop);
                    break;
                case "ws-send":
                    var data = rt.TryGetProperty("data", out var d) ? d.GetString() : "";
                    if (!string.IsNullOrEmpty(data) && _ws?.State == WebSocketState.Open)
                    {
                        var bytes = Encoding.UTF8.GetBytes(data);
                        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _wsCts?.Token ?? CancellationToken.None);
                    }
                    break;
                case "new-session":
                    _sessionKey = rt.TryGetProperty("sessKey", out var sk2) ? sk2.GetString() : _sessionKey;
                    break;
                case "set-sesskey":
                    _pendingKey = rt.TryGetProperty("sessKey", out var pk) ? pk.GetString() ?? "" : "";
                    _persistSessKey = _pendingKey;
                    _sessionKey = _persistSessKey;
                    break;
                case "load-msgs":
                    LoadMessages(rt.TryGetProperty("sessKey", out var lk) ? lk.GetString() : "");
                    break;
                case "update-title":
                    UpdateTitle(rt.TryGetProperty("sessKey", out var tk) ? tk.GetString() : "",
                                rt.TryGetProperty("t", out var tv) ? tv.GetString() : "");
                    _ = Task.Run(async () => { await Task.Delay(500); body?.BeginInvoke(SendSessions); });
                    break;
                case "del-sess":
                    DeleteSession(rt.TryGetProperty("sessKey", out var dk) ? dk.GetString() : "");
                    SendSessions();
                    break;
                case "refresh-ss":
                    SendSessions();
                    break;
                case "save-filter":
                    SaveFilter(rt.TryGetProperty("key", out var fk) ? fk.GetString() ?? "" : "",
                               rt.TryGetProperty("value", out var fv) && fv.GetBoolean());
                    break;
                case "pick-file":
                    body.BeginInvoke(() =>
                    {
                        using var fd = new OpenFileDialog { Title = "选择文件" };
                        if (fd.ShowDialog() == DialogResult.OK && !string.IsNullOrEmpty(fd.FileName))
                        {
                            var name = Path.GetFileName(fd.FileName);
                            _ = wv?.CoreWebView2?.ExecuteScriptAsync($"window.onFilePicked('{name.Replace("'", "\\'").Replace("\\", "\\\\")}')");
                        }
                    });
                    break;
            }
        }
        catch { }
    }

    // ═══ 会话 & 持久化 ═══
    string GetSessPath(string key) => Path.Combine(SDir, key + ".jsonl");


    static string ExtractTextFromContent(JsonElement ct)
    {
        if (ct.ValueKind != JsonValueKind.Array) return "";
        foreach (var ci in ct.EnumerateArray())
            if (ci.TryGetProperty("text", out var ctx))
                return ctx.GetString() ?? "";
        return "";
    }

    static string ExtractTextFromList(List<JsonElement> items)
    {
        foreach (var ci in items)
            if (ci.TryGetProperty("text", out var ctx))
                return ctx.GetString() ?? "";
        return "";
    }

    static void ParseBlock(JsonElement pt, List<object> blocks, Dictionary<string, string> allToolResults)
    {
        if (!pt.TryGetProperty("type", out var ptt)) return;
        var bt = ptt.GetString() ?? "";
        if (bt == "text")
        {
            var tt = pt.TryGetProperty("text", out var tx) ? tx.GetString() ?? "" : "";
            tt = StripSenderMeta(tt);
            if (!string.IsNullOrWhiteSpace(tt))
                blocks.Add(new { type = "text", text = tt });
        }
        else if (bt == "thinking")
        {
            var th = pt.TryGetProperty("thinking", out var tx) ? tx.GetString() ?? "" : "";
            if (!string.IsNullOrWhiteSpace(th))
                blocks.Add(new { type = "thinking", text = th });
        }
        else if (bt == "toolCall")
        {
            var id = pt.TryGetProperty("id", out var tci) ? tci.GetString() ?? "" : "";
            if (!string.IsNullOrEmpty(id))
            {
                var nm = pt.TryGetProperty("name", out var tn2) ? tn2.GetString() ?? "?" : "?";
                var result = allToolResults.TryGetValue(id, out var r) ? r : "";
                var args = "{}";
                if (pt.TryGetProperty("arguments", out var ta) || pt.TryGetProperty("input", out ta))
                {
                    try { args = JsonSerializer.Serialize(JsonDocument.Parse(ta.ToString()), new JsonSerializerOptions { WriteIndented = true }); } catch { args = ta.ToString(); }
                }
                blocks.Add(new { type = "tool_use", name = nm, title = args, result = result, tcid = id, kind = "", status = "done" });
            }
        }
    }

    void LoadMessages(string key)
    {
        try
        {
            var path = GetSessPath(key);
            var items = new List<object>();
            if (File.Exists(path))
            {
                // 收集所有消息，之后合并 toolCall + toolResult
                var rawMsgs = new List<(string role, string stamp, List<JsonElement> content, string toolCallId)>();
                foreach (var line in File.ReadLines(path, Encoding.UTF8).Reverse().Take(100).Reverse())
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var rt = doc.RootElement;
                        if (rt.TryGetProperty("type", out var tp) && tp.GetString() == "message" &&
                            rt.TryGetProperty("message", out var mg) &&
                            mg.TryGetProperty("role", out var rl))
                        {
                            var role = rl.GetString() == "user" ? "user" : "assistant";
                            var stamp = "";
                            if (rt.TryGetProperty("timestamp", out var ts))
                            {
                                try { var d = DateTime.Parse(ts.GetString()!).ToLocalTime(); stamp = d.ToString("MM-dd HH:mm"); } catch { }
                            }
                            var msgTcId = mg.TryGetProperty("toolCallId", out var mtc) ? mtc.GetString() ?? "" : "";
                            var content = new List<JsonElement>();
                            if (mg.TryGetProperty("content", out var ct) && ct.ValueKind == JsonValueKind.Array)
                                foreach (var pt in ct.EnumerateArray())
                                    content.Add(pt.Clone());
                            rawMsgs.Add((role, stamp, content, msgTcId));
                        }
                    }
                    catch { }
                }

                // 先全局收集：遍历两层结构 {role,content:[...]}
                var allToolCalls = new Dictionary<string, JsonElement>();
                var allToolResults = new Dictionary<string, string>();
                foreach (var (role, stamp, content, msgTcId) in rawMsgs)
                {
                    // 消息级 toolCallId（toolResult 为独立消息时）
                    if (!string.IsNullOrEmpty(msgTcId))
                    {
                        var cn = ExtractTextFromList(content);
                        if (!string.IsNullOrEmpty(cn)) allToolResults[msgTcId] = cn;
                    }
                    foreach (var item in content)
                    {
                        if (item.TryGetProperty("role", out var _) && item.TryGetProperty("content", out var subCt) && subCt.ValueKind == JsonValueKind.Array)
                        {
                            var tcid = item.TryGetProperty("toolCallId", out var tr) ? tr.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(tcid))
                            {
                                var cn = ExtractTextFromContent(subCt);
                                if (!string.IsNullOrEmpty(cn)) allToolResults[tcid] = cn;
                            }
                            foreach (var pt in subCt.EnumerateArray())
                            {
                                if (pt.TryGetProperty("type", out var ptt) && ptt.GetString() == "toolCall")
                                {
                                    var id = pt.TryGetProperty("id", out var tci) ? tci.GetString() ?? "" : "";
                                    if (!string.IsNullOrEmpty(id)) allToolCalls[id] = pt.Clone();
                                }
                            }
                        }
                        else if (item.TryGetProperty("type", out var ptt2) && ptt2.GetString() == "toolCall")
                        {
                            var id = item.TryGetProperty("id", out var tci2) ? tci2.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(id)) allToolCalls[id] = item.Clone();
                        }
                    }
                }

                // 第二遍：构建 blocks
                foreach (var (role, stamp, content, msgTcId) in rawMsgs)
                {
                    if (!string.IsNullOrEmpty(msgTcId)) continue; // 独立 toolResult 消息，已合并
                    var blocks = new List<object>();
                    foreach (var item in content)
                    {
                        // 两层嵌套：{role, content:[...]}
                        if (item.TryGetProperty("role", out var _) && item.TryGetProperty("content", out var subCt) && subCt.ValueKind == JsonValueKind.Array)
                        {
                            if (item.TryGetProperty("toolCallId", out var _)) continue; // 跳过 toolResult（已合并）
                            foreach (var pt in subCt.EnumerateArray())
                                ParseBlock(pt, blocks, allToolResults);
                        }
                        else
                        {
                            // 平层结构：[{type:"text"},...]
                            ParseBlock(item, blocks, allToolResults);
                        }
                    }
                    if (blocks.Count > 0)
                        items.Add(new { role = role, stamp = stamp, blocks = blocks });
                }
            }
            var json = JsonSerializer.Serialize(new { type = "msgs", list = items });
            wv?.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch { }
    }

    static string StripSenderMeta(string text)
    {
        // 格式: [timestamp] Sender (untrusted metadata):\n\n{json}\n[timestamp] actual message
        var idx = text.IndexOf("Sender (untrusted metadata)", StringComparison.Ordinal);
        if (idx < 0)
        {
            // 没元数据头，但可能有时间戳前缀 [Day Mon DD HH:MM GMT+X]
            return StripTimestampPrefix(text);
        }
        // 找到元数据后的下一个时间戳
        var afterMeta = text.IndexOf("\n[", idx + 30);
        if (afterMeta < 0) return text.Substring(0, idx).Trim();
        return StripTimestampPrefix(text.Substring(afterMeta + 1).Trim());
    }

    static string StripTimestampPrefix(string text)
    {
        // 去掉开头的 [Day Mon DD HH:MM GMT+X] 时间戳
        if (text.Length > 4 && text[0] == '[')
        {
            var close = text.IndexOf(']');
            if (close > 3 && close < 40)
            {
                var bracket = text.Substring(1, close - 1);
                if (bracket.Contains("GMT"))
                    return text.Substring(close + 1).TrimStart();
            }
        }
        return text;
    }

    void UpdateTitle(string key, string title)
    {
        // Title is managed in-memory for session list; actual JSONL is more complex
    }

    void DeleteSession(string key)
    {
        try
        {
            var path = GetSessPath(key);
            if (File.Exists(path)) File.Delete(path);
            var tPath = Path.Combine(SDir, key + ".trajectory.jsonl");
            if (File.Exists(tPath)) File.Delete(tPath);
        }
        catch { }
    }

    void SendSessions()
    {
        try
        {
            LoadKeyMap();
            var d = SDir;
            if (!Directory.Exists(d)) return;
            var files = Directory.GetFiles(d, "*.jsonl")
                .Where(f => Guid.TryParse(Path.GetFileNameWithoutExtension(f), out _))
                .Where(f => !f.EndsWith(".trajectory.jsonl"))
                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                .Take(50);

            var items = new List<JsonObject>();
            foreach (var f in files)
            {
                try
                {
                    var id = Path.GetFileNameWithoutExtension(f);
                    string first = "", last = "";
                    using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    string? line;
                    int c = 0;
                    while ((line = sr.ReadLine()) != null && c < 30)
                    {
                        c++;
                        try
                        {
                            using var jd = JsonDocument.Parse(line);
                            var rt = jd.RootElement;
                            if (rt.TryGetProperty("type", out var tp) && tp.GetString() == "message" &&
                                rt.TryGetProperty("message", out var mg) &&
                                mg.TryGetProperty("role", out var rl) && rl.GetString() == "user" &&
                                mg.TryGetProperty("content", out var ct) && ct.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var pt in ct.EnumerateArray())
                                    if (pt.TryGetProperty("text", out var tx))
                                    {
                                        var t = CleanTitle(tx.GetString() ?? "");
                                        if (string.IsNullOrWhiteSpace(t)) continue;
                                        if (string.IsNullOrEmpty(first)) first = t;
                                        last = t;
                                    }
                            }
                        }
                        catch { }
                    }
                    var time = new FileInfo(f).LastWriteTime.ToString("MM-dd HH:mm");
                    var title = string.IsNullOrEmpty(first) ? "新对话" : first;
                    // 确定 key：优先用映射 → pending → persistSessKey → GUID
                    string rpcKey;
                    if (_keyMap.TryGetValue(id, out var mk)) rpcKey = mk;
                    else if (!string.IsNullOrEmpty(_pendingKey)) { rpcKey = _pendingKey; _keyMap[id] = _pendingKey; }
                    else if (!string.IsNullOrEmpty(_persistSessKey)) { rpcKey = _persistSessKey; _keyMap[id] = _persistSessKey; }
                    else rpcKey = id;
                    items.Add(new JsonObject {
                        ["id"] = id, ["key"] = rpcKey,
                        ["title"] = title, ["sub"] = last ?? "", ["time"] = time
                    });
                }
                catch { }
            }
            _pendingKey = null;
            SaveKeyMap();
            var json = JsonSerializer.Serialize(new { type = "sessions", list = items });
            wv?.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch { }
    }

    static string CleanTitle(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // 移除 markdown 代码块
        s = System.Text.RegularExpressions.Regex.Replace(s, @"```[\s\S]*?```", "");
        // 移除 JSON 块
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\{[^}]*\}", "");
        // 移除 Sender 行
        s = System.Text.RegularExpressions.Regex.Replace(s, @"^Sender[^\n]*", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        // 移除时间戳前缀
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[.{3,4} \d{4}-\d{2}-\d{2} \d{2}:\d{2} GMT[+\-]\d+\]\s*", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[图片:\s*[^\]]+\]\s*", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[Subagent[^\]]*\]\s*", "");
        // 移除残留的引号和 metadata
        s = System.Text.RegularExpressions.Regex.Replace(s, "untrusted metadata[^\n]*", "");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
        s = s.Replace("`", "").Replace("\"", "").Replace(":", "").Replace(",", "");
        return s.Trim();
    }

    // ═══ WS ═══
    async Task ConnectWsLoop()
    {
        for (int proto = _lastGoodProtocol; proto >= 3; proto--)
        {
            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    _wsCts?.Cancel(); _ws?.Dispose();
                    _wsCts = new CancellationTokenSource();
                    _ws = new ClientWebSocket();
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(_wsCts.Token);
                    cts.CancelAfter(8000);
                    await _ws.ConnectAsync(new Uri("ws://127.0.0.1:18789/ws"), cts.Token);
                    PostStatus("已连接", "ok");

                    var buf = new byte[65536];
                    while (_ws.State == WebSocketState.Open && !_wsCts.Token.IsCancellationRequested)
                    {
                        var r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _wsCts.Token);
                        if (r.MessageType == WebSocketMessageType.Close) break;
                        var t = Encoding.UTF8.GetString(buf, 0, r.Count);
                        await HandleWsMsg(t, proto);
                    }
                    _wsReady = false; return;
                }
                catch (OperationCanceledException) { _wsReady = false; return; }
                catch { if (retry < 1) await Task.Delay(2000); }
            }
        }
        _wsReady = false;
    }

    async Task HandleWsMsg(string raw, int proto)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var rt = doc.RootElement;
            var ty = rt.TryGetProperty("type", out var tp) ? tp.GetString() : "";

            if (ty == "event")
            {
                var ev = rt.GetProperty("event").GetString();
                if (ev == "connect.challenge") { await SendConnect(proto); return; }
                // 检测 lifecycle end - 只处理当前会话
                if (ev == "agent" && raw.Contains("\"stream\":\"lifecycle\"") && raw.Contains("\"phase\":\"end\"") && raw.Contains(_persistSessKey))
                {
                    var f = GetCurrentSessFile();
                    _ = Task.Run(async () => { await Task.Delay(500); ReloadLastMessage(f); });
                }
            }
            else if (ty == "res")
            {
                var idEl = rt.TryGetProperty("id", out var e2) ? e2 : default;
                var idS = idEl.ValueKind == JsonValueKind.String ? idEl.GetString()!
                        : idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString() : "0";
                if (idS == "1" && rt.TryGetProperty("ok", out var o) && o.GetBoolean())
                {
                    _wsReady = true; _lastGoodProtocol = proto;
                    PostStatus("已握手", "ok");
                }
            }
            PostWsMsg(raw);
        }
        catch { }
    }

    async Task SendConnect(int proto)
    {
        var token = GetToken();
        var ps = JsonSerializer.SerializeToElement(new
        {
            minProtocol = 3, maxProtocol = proto,
            client = new { id = "gateway-client", mode = "backend", version = "1.0", platform = "windows" },
            role = "operator",
            scopes = new[] { "operator.read", "operator.write", "operator.admin" },
            caps = new[] { "tool-events" },
            auth = string.IsNullOrEmpty(token) ? null : new { token },
            userAgent = "OpenClaw-Manager/" + MainForm.AppVersion
        });
        var req = JsonSerializer.SerializeToUtf8Bytes(new { type = "req", id = "1", method = "connect", @params = ps });
        if (_ws?.State == WebSocketState.Open)
            await _ws.SendAsync(new ArraySegment<byte>(req), WebSocketMessageType.Text, true, _wsCts?.Token ?? CancellationToken.None);
    }

        string? GetCurrentSessFile()
    {
        try
        {
            var d = SDir; if (!Directory.Exists(d) || string.IsNullOrEmpty(_persistSessKey)) return null;
            // 通过 sessions.json 查 sessionKey → sessionId → JSONL 文件名
            var sj = Path.Combine(Path.GetDirectoryName(d)!, "sessions.json");
            if (!File.Exists(sj)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(sj, Encoding.UTF8));
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (p.Value.TryGetProperty("sessionId", out var sid) &&
                    p.Value.TryGetProperty("key", out var k) &&
                    (k.GetString() ?? "").Contains(_persistSessKey, StringComparison.OrdinalIgnoreCase))
                {
                    var f = Path.Combine(d, (sid.GetString() ?? "") + ".jsonl");
                    if (File.Exists(f)) return f;
                }
            }
        }
        catch { }
        return null;
    }


    static string ReadWorkspacePath()
    {
        try
        {
            var cfg = JsonNode.Parse(File.ReadAllText(MainForm.CfgFullPath, Encoding.UTF8));
            var ws = cfg?["agents"]?["defaults"]?["workspace"]?.ToString();
            if (!string.IsNullOrEmpty(ws)) return ws;
        }
        catch { }
        return Path.Combine(Path.GetDirectoryName(MainForm.CfgFullPath)!, "workspace");
    }

    void ReloadLastMessage(string? filePath)
    {
        try
        {
            var d = SDir; if (!Directory.Exists(d)) return;
            var files = filePath != null && File.Exists(filePath)
                ? new[] { filePath }
                : Directory.GetFiles(d, "*.jsonl").OrderByDescending(f => new FileInfo(f).LastWriteTime).Take(3);
            foreach (var f in files)
            {
                var lines = File.ReadLines(f, Encoding.UTF8).Reverse().Take(5).Reverse().ToList();
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var rt = doc.RootElement;
                    if (rt.TryGetProperty("type", out var tp) && tp.GetString() == "message" &&
                        rt.TryGetProperty("message", out var mg) &&
                        mg.TryGetProperty("content", out var ct) && ct.ValueKind == JsonValueKind.Array)
                    {
                        var thinkingBlocks = new List<string>();
                        foreach (var item in ct.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var ptt) && ptt.GetString() == "thinking" &&
                                item.TryGetProperty("thinking", out var th))
                            {
                                thinkingBlocks.Add(th.GetString() ?? "");
                            }
                        }
                        if (thinkingBlocks.Count > 0)
                        {
                            var json = JsonSerializer.Serialize(new { type = "append-thinking", blocks = thinkingBlocks });
                            body?.BeginInvoke(() => wv?.CoreWebView2?.PostWebMessageAsJson(json));
                        }
                        return;
                    }
                }
            }
        }
        catch { }
    }

    void PostWsMsg(string raw)
    {
        body?.BeginInvoke(() =>
        {
            try
            {
                var escaped = JsonSerializer.Serialize(raw);
                _ = wv?.CoreWebView2?.ExecuteScriptAsync($"handleWsMsg({escaped})");
            }
            catch { }
        });
    }

    void PostStatus(string text, string cls)
    {
        body?.BeginInvoke(() =>
        {
            try
            {
                var escaped = JsonSerializer.Serialize(text);
                _ = wv?.CoreWebView2?.ExecuteScriptAsync($"showStatus({escaped},'{cls}')");
            }
            catch { }
            try { /* status updated via JS showStatus */ } catch { }
        });
    }

    public void Reattach(Panel p)
    {
        body = p;
        body.Controls.Clear();
        body.Controls.Add(toolbar);
        if (bgPanel == null) { bgPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.IsDark ? Color.FromArgb(0x0e, 0x10, 0x15) : Color.FromArgb(0xf5, 0xf7, 0xfa) }; bgPanel.Controls.Add(wv); }
        else bgPanel.BackColor = Theme.IsDark ? Color.FromArgb(0x0e, 0x10, 0x15) : Color.FromArgb(0xf5, 0xf7, 0xfa);
        body.Controls.Add(bgPanel);
        ApplyTheme();
        // 重新读取 chat.html + 导航（CSS 改了要生效）
        try
        {
            var aiName = "AI"; var aiEmoji = "🤖"; var userName = "YOU"; var userEmoji = "👤"; var aiAvatarB64 = "";
            try
            {
                var ws = ReadWorkspacePath();
                if (!Directory.Exists(ws)) ws = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "workspace");
                if (Directory.Exists(ws))
                {
                    ReadMdField(Path.Combine(ws, "IDENTITY.md"), ref aiName, ref aiEmoji);
                    ReadMdField(Path.Combine(ws, "USER.md"), ref userName, ref userEmoji);
                }
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apple-touch-icon.png");
                if (!File.Exists(iconPath)) iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtime", "node_modules", "openclaw", "dist", "control-ui", "apple-touch-icon.png");
                if (File.Exists(iconPath)) aiAvatarB64 = "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(iconPath));
            }
            catch { }
            PushIdentity(aiName, aiEmoji, userName, userEmoji, aiAvatarB64); // Reattach 时也要推
            var fresh = BuildPageHtml(Theme.IsDark ? "dark" : "light", Theme.IsDark ? "#0e1015" : "#f5f7fa", aiName, aiEmoji, userName, userEmoji, aiAvatarB64, _persistSessKey);
            _processedHtml = fresh;
            ;
            WriteTempAndNavigate();
        }
        catch { }
    }

    string _lastAiName = "AI", _lastAiEmoji = "🤖", _lastUserName = "YOU", _lastUserEmoji = "👤", _lastAiAvatar = "";

    void PushIdentity(string aiName, string aiEmoji, string userName, string userEmoji, string aiAvatar)
    {
        _lastAiName = aiName; _lastAiEmoji = aiEmoji;
        _lastUserName = userName; _lastUserEmoji = userEmoji;
        _lastAiAvatar = aiAvatar;
        try
        {
            var js = $"updateIdentity({JsonSerializer.Serialize(aiName)},{JsonSerializer.Serialize(aiEmoji)},{JsonSerializer.Serialize(userName)},{JsonSerializer.Serialize(userEmoji)},{JsonSerializer.Serialize(aiAvatar)})";
            _ = wv?.CoreWebView2?.ExecuteScriptAsync(js);
        }
        catch { }
    }

    void WriteTempAndNavigate()
    {
        try
        {
            if (string.IsNullOrEmpty(_processedHtml)) return;
            if (wv.CoreWebView2 != null)
            {
                // 导航完成后推送身份
                wv.CoreWebView2.NavigationCompleted += (_, e) =>
                {
                    if (e.IsSuccess) PushIdentity(_lastAiName, _lastAiEmoji, _lastUserName, _lastUserEmoji, _lastAiAvatar);
                };
                wv.NavigateToString(_processedHtml);
                // 兜底：1s 后再推一次防止时序问题
                _ = Task.Run(async () => { await Task.Delay(1000); body?.BeginInvoke(() => PushIdentity(_lastAiName, _lastAiEmoji, _lastUserName, _lastUserEmoji, _lastAiAvatar)); });
            }
        }
        catch { }
    }

    string GetToken()
    {
        try
        {
            var p = MainForm.CfgFullPath;
            if (File.Exists(p))
            {
                var d = JsonDocument.Parse(File.ReadAllText(p, Encoding.UTF8));
                if (d.RootElement.TryGetProperty("gateway", out var g) &&
                    g.TryGetProperty("auth", out var a) &&
                    a.TryGetProperty("token", out var t))
                    return t.GetString() ?? "";
            }
        }
        catch { }
        return "";
    }

    static void ReadMdField(string path, ref string name, ref string emoji)
    {
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            var ci = line.IndexOf(':');
            if (ci < 0) continue;
            var key = line.Substring(0, ci).Trim().ToLower();
            var val = line.Substring(ci + 1).Trim();
            if (key.Contains("name") || key.EndsWith("**"))
            {
                val = val.Trim('*', ' ', '"');
                if (val.Length > 0 && !val.StartsWith("*")) name = val;
            }
            if (key.Contains("emoji"))
                if (val.Length > 0) emoji = val;
        }
    }

    public void ApplyTheme()
    {
        SetWvBg();
        // 刷新 C# 工具栏颜色
        if (toolbar != null)
        {
            toolbar.BackColor = Theme.BgWhite;
            toggleBtn.BackColor = Color.Transparent; toggleBtn.ForeColor = Theme.Fc;
            UpdateFilterBtn(thinkBtn, _showThinking);
            UpdateFilterBtn(toolsBtn, _showTools);
            UpdateFilterBtn(schedBtn, _showSchedule);
        }
        if (wv?.CoreWebView2 != null)
        {
            var mode = Theme.IsDark ? "dark" : "light";
            var bg = Theme.IsDark ? "#0e1015" : "#f5f7fa";
            _ = wv.CoreWebView2.ExecuteScriptAsync(
                "document.documentElement.setAttribute('data-theme-mode','" + mode + "');" +
                "document.body.style.background='" + bg + "'");
        }
    }

    void SetWvBg()
    {
        var c = Theme.IsDark ? Color.FromArgb(0x0e, 0x10, 0x15) : Color.FromArgb(0xf5, 0xf7, 0xfa);
        wv.BackColor = c;
    }
}
