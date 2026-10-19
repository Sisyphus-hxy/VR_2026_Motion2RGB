import fs from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.dirname(fileURLToPath(import.meta.url));
const out=path.join(root,'svg_output');
await fs.mkdir(out,{recursive:true});
const C={bg:'#FFFFFF',light:'#F2F5F7',blue:'#123D64',cyan:'#0089AA',amber:'#B87924',ink:'#222C35',muted:'#576772',line:'#D6DFE5'};
const esc=s=>String(s).replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const txt=(x,y,s,size=28,color=C.ink,bold=false,anchor='start')=>`<text x="${x}" y="${y}" font-size="${size}" fill="${color}"${bold?' font-weight="bold"':''}${anchor!=='start'?` text-anchor="${anchor}"`:''}>${esc(s)}</text>`;
const para=(x,y,lines,size=28,color=C.ink,dy=42,bold=false)=>`<text x="${x}" y="${y}" font-size="${size}" fill="${color}"${bold?' font-weight="bold"':''}>${lines.map((s,i)=>`<tspan x="${x}"${i?` dy="${dy}"`:''}>${esc(s)}</tspan>`).join('')}</text>`;
const rect=(x,y,w,h,fill=C.light,stroke='none')=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${fill}" stroke="${stroke}"${stroke!=='none'?' stroke-width="1.5"':''}/>`;
const line=(x1,y1,x2,y2,col=C.line,width=1.5,arrow=false)=>`<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-width="${width}"${arrow?` marker-end="url(#arrow-${col===C.cyan?'cyan':'blue'})"`:''}/>`;
const group=(id,b,body,role='')=>`<g id="${id}" data-pptx-bounds="${b.join(' ')}"${role?` data-pptx-role="${role}"`:''}>${body}</g>`;
function crop(file,x,y,w,h,sx,sy,sw,sh,iw=1805,ih=960){
  const unit=[sx/iw,sy/ih,sw/iw,sh/ih].map(v=>v.toFixed(8));
  return `<svg x="${x}" y="${y}" width="${w}" height="${h}" viewBox="${unit.join(' ')}" preserveAspectRatio="none" overflow="hidden"><image href="../images/${file}" x="0" y="0" width="1" height="1" preserveAspectRatio="none"/></svg>`;
}
const image=(file,x,y,w,h,id='')=>`<image${id?` id="${id}"`:''} href="../images/${file}" x="${x}" y="${y}" width="${w}" height="${h}" preserveAspectRatio="xMidYMid meet"/>`;
const math=(id,x,y,w,h,latex,preview,size=30)=>`<g id="${id}" data-pptx-replace-with="formula" data-pptx-x="${x}" data-pptx-y="${y}" data-pptx-width="${w}" data-pptx-height="${h}" data-pptx-bounds="${x} ${y} ${w} ${h}"><metadata type="application/json"><![CDATA[${JSON.stringify({latex,display:'block',font_size:size,color:C.blue,align:'left'})}]]></metadata>${txt(x,y+h*.64,preview,size,C.blue)}</g>`;
function shell(n,title,english,body,role='content'){
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1280 720" width="1280" height="720" lang="zh-CN" font-family="Microsoft YaHei, Arial, sans-serif" font-size="28" data-pptx-page-role="${role}">
  <defs><marker id="arrow-blue" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><polygon points="0,0 10,5 0,10" fill="${C.blue}"/></marker><marker id="arrow-cyan" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><polygon points="0,0 10,5 0,10" fill="${C.cyan}"/></marker></defs>
  <rect id="page-background" data-pptx-role="background" x="0" y="0" width="1280" height="720" fill="${C.bg}"/>
  ${title?group('page-title',[48,35,1184,112],txt(48,89,title,40,C.blue,true)+txt(50,128,english,22,C.muted)):''}
  ${body}
  ${group('page-footer',[48,679,1184,29],line(48,680,1232,680)+txt(48,703,'Motion2RGB   /   VR 课程开题   /   2026.10.15',14,C.muted)+txt(1232,703,String(n).padStart(2,'0')+' / 07',14,C.muted,false,'end'),'footer')}
  </svg>`;
}
const slides=[];
slides.push(['01_cover',shell(1,'','',
  group('project-title',[48,40,1184,197],txt(48,117,'Motion2RGB',64,C.blue,true)+txt(51,162,'Motion-Conditioned Visual Degradation Simulation',30,C.ink)+txt(51,203,'面向 Sim-to-Real 的运动状态驱动视觉退化仿真系统',28,C.ink)+txt(1232,224,'for Sim-to-Real',22,C.cyan,false,'end'))+
  group('hero-demo',[48,263,1184,404],crop('demo_1.png',71,263,1137.9,403.5,0,70,1805,640)), 'cover')]);

slides.push(['02_motivation',shell(2,'背景与动机','Background & Motivation',
  group('ideal-camera',[48,171,566,407],txt(48,206,'理想仿真采集',30,C.blue,true)+para(48,250,['瞬时渲染的清晰 RGB','较少显式模拟步态和曝光运动'],28,C.ink,42)+crop('demo_1.png',48,321,566,240,10,350,880,373)+rect(48,534,566,27,C.bg)+txt(48,555,'Demo 中的 Clean RGB 参考',22,C.muted))+
  group('moving-camera',[666,171,566,407],txt(666,206,'真实运动采集的因素',30,C.cyan,true)+para(666,250,['步态、转向扰动相机轨迹','曝光期间运动造成图像退化'],28,C.ink,42)+crop('demo_1.png',666,321,566,240,913,350,880,373)+rect(666,534,566,27,C.bg)+txt(666,555,'Demo 中的退化模拟，非实机数据',22,C.muted))+
  group('scope',[48,592,1184,66],rect(48,592,1184,66,C.light)+txt(67,633,'研究范围：Sim-to-Real 中的视觉采集差异',30,C.blue,true))+
  group('references',[48,660,1184,19],txt(48,673,'[1] Chattopadhyay et al., RobustNav, ICCV 2021.   [2] Tobin et al., Domain Randomization, IROS 2017.',14,C.muted)))]);

const chain=[['运动状态','Motion State','速度 v / 角速度 ω',C.blue],['相机运动','Camera Motion','步态 / 转向 / 跳跃',C.cyan],['视觉退化','Visual Degradation','重投影 / 加权采样',C.cyan],['RGB 观测','RGB Observation','Clean / Degraded',C.blue]];
slides.push(['03_formulation',shell(3,'问题建模','Problem Formulation',
  group('causal-chain',[48,201,1184,213],chain.map((d,i)=>{const x=48+306*i;return rect(x,228,264,164,i===2?'#E7F3F6':C.light)+txt(x+20,275,d[0],30,d[3],true)+txt(x+20,317,d[1],22,C.muted)+txt(x+20,374,d[2],22,C.ink)+(i<3?line(x+270,310,x+299,310,C.cyan,3,true):'');}).join(''))+
  group('gait-relation',[48,457,549,146],txt(48,494,'速度与步幅决定步频',30,C.blue,true)+para(48,541,['共享步态相位产生头部扰动','左右完整周期包含两步'],28,C.ink,42))+
  group('image-motion-relation',[662,457,570,146],txt(662,494,'相机位姿变化决定屏幕运动',30,C.cyan,true)+para(662,541,['深度参与每个像素的重投影','转向可在低平移速度下产生模糊'],28,C.ink,42))+
  group('model-boundary',[48,620,1184,42],txt(48,649,'Walking / Running / Turning 是状态标签；模糊由实际相机运动计算。',22,C.muted)))]);

function node(x,y,w,title,code,col=C.blue,small=false){return rect(x,y,w,95,C.light)+txt(x+18,y+36,title,small?22:28,col,true)+txt(x+18,y+72,code,22,C.muted);}
slides.push(['04_system',shell(4,'系统架构','System Overview   /   Unity 2022.3.62f3 · Built-in Render Pipeline',
  group('implementation-diagram',[48,173,1184,448],
    node(48,188,255,'输入与 PlayerRig','FirstPersonController')+line(175,289,175,325,C.blue,2.5,true)+
    node(48,332,255,'运动测量','MotionStateTracker')+line(175,433,175,477,C.cyan,2.5,true)+
    node(48,484,255,'头部 / 机器人步态','GaitMotionDriver',C.cyan,true)+
    txt(356,210,'共享 Unity 3D Scene',30,C.blue,true)+line(356,230,1232,230,C.line)+
    node(356,270,250,'Clean Camera','稳定基础位姿')+line(613,317,652,317,C.blue,2.5,true)+
    node(660,270,250,'Clean RT','RenderTexture')+line(917,317,952,317,C.blue,2.5,true)+
    node(960,270,272,'参考视图','UGUI RawImage')+
    line(310,531,348,531,C.cyan,2.5,true)+node(356,484,250,'Degraded Camera','步态扰动相机',C.cyan,true)+
    line(613,531,652,531,C.cyan,2.5,true)+node(660,484,250,'RGB 后处理','DirectionalMotionBlur',C.cyan)+
    line(917,531,952,531,C.cyan,2.5,true)+node(960,484,272,'退化 RT / 视图','UGUI RawImage',C.cyan,true)+
    txt(661,434,'OnRenderImage + Graphics.Blit',22,C.cyan)+
    txt(960,401,'并排实时显示',22,C.blue)+
    txt(356,608,'Same Pose：复制 Clean 位姿，仅比较图像退化',22,C.muted))+
  group('system-status',[48,630,1184,35],txt(48,658,'当前代码与场景已接入上述链路；UI 可调速度、步幅、扰动幅度、模糊强度与 Exposure Proxy。',22,C.muted)))]);

slides.push(['05_method',shell(5,'技术方法','Technical Method   /   Gait phase + depth reprojection',
  group('gait-method',[48,173,530,299],txt(48,207,'01  共享步态时钟',30,C.blue,true)+
    math('gait-clock',48,230,520,67,'f=\\frac{v}{L},\\quad \\phi_{t+1}=\\phi_t+\\pi f\\Delta t','f = v / L,   φ(t+1) = φ(t) + π f Δt',28)+
    math('gait-offset',48,307,520,54,'x=A_x\\cos\\phi,\\quad y=-A_y\\cos(2\\phi)','x = Aₓ cos φ,   y = −Aᵧ cos(2φ)',28)+
    para(48,403,['f 为每秒步数，左右周期为两步','pitch / roll 同相位并指数平滑'],22,C.muted,36))+
  group('reprojection-method',[652,173,580,306],txt(652,207,'02  深度重投影与采样',30,C.cyan,true)+
    math('blur-vector',652,235,575,78,'\\mathbf{d}=(\\mathbf{u}-\\mathbf{u}_{prev})\\frac{T(E)IG}{\\Delta t}','d = (u − u_prev) · T(E) I G / Δt',30)+
    para(652,357,['深度 + 当前 / 历史 VP 得到 u_prev','沿轨迹追加 20 个样本，深度差降权','归一化采样权重，保留常量亮度'],22,C.ink,36)+
    txt(652,466,'T(E)：曝光代理映射；I：强度；G：运动增益',22,C.muted))+
  group('shader-pipeline',[48,488,1184,87],rect(48,488,1184,87,C.light)+
    txt(71,521,'RGB + Depth',22,C.blue,true)+line(260,533,300,533,C.cyan,2.5,true)+txt(322,542,'位姿重投影',28,C.ink)+line(520,533,560,533,C.cyan,2.5,true)+txt(583,542,'Shader 加权采样',28,C.ink)+line(855,533,897,533,C.cyan,2.5,true)+txt(922,542,'Degraded RT',28,C.cyan,true))+
  group('heuristic-boundary',[48,594,1184,68],txt(48,620,'Heuristic approximation：运动增益与深度距离截断均为艺术参数',22,C.muted)+txt(48,655,'Exposure Proxy 未经实机标定，当前模型不等价于完整物理曝光积分。',22,C.muted)))]);

// Default Generate's mandatory early gate is run before the last two pages are authored.
const requested=process.argv[2]||'early';
for(const [stem,svg] of slides) await fs.writeFile(path.join(out,stem+'.svg'),svg,'utf8');
if(requested==='early'){console.log('P01–P05 authored.');process.exit(0);}

slides.push(['06_demo',shell(6,'当前 Demo','Current Demo Results   /   20 秒真实录屏',
  group('demo-video',[48,149,928,522],image('video_08s.png',48,149,928,522,'demo-video-poster'))+
  group('demo-evidence',[1003,166,229,290],txt(1003,202,'已可展示',28,C.cyan,true)+para(1003,251,['双 RGB 对比','Walking / Running','转向过程','实时参数 UI','机器人观察窗'],22,C.ink,38))+
  group('demo-boundary',[1003,474,229,188],line(1003,479,1232,479)+txt(1003,523,'待验证',28,C.blue,true)+para(1003,568,['实机标定','图像质量量化','导航鲁棒性收益'],22,C.muted,37)))]);

slides.push(['07_next_steps',shell(7,'后续工作与预期产出','Next Steps & Expected Outcomes',
  group('required-work',[48,174,744,383],txt(48,210,'必做：完善可重复实验原型',30,C.blue,true)+
    txt(48,273,'01',30,C.cyan,true)+txt(111,273,'丰富室内场景与运动模式',28,C.ink)+
    txt(48,346,'02',30,C.cyan,true)+txt(111,346,'验证深度重投影与运动平滑',28,C.ink)+
    txt(48,419,'03',30,C.cyan,true)+txt(111,419,'Same Pose 消融与量化测试',28,C.ink)+txt(111,456,'清晰度 / 图像差异 / 帧耗时，记录参数与状态',22,C.muted)+
    txt(48,529,'04',30,C.cyan,true)+txt(111,529,'最终系统展示与期末报告',28,C.ink))+
  group('optional-extension',[858,174,374,383],line(858,178,858,553,C.line)+txt(892,210,'可选研究扩展',30,C.amber,true)+para(892,277,['真实相机数据标定','动态物体 Motion Blur','视觉导航评测接口'],28,C.ink,65)+para(892,501,['深度感知已在代码中实现','后续重点是验证与校准'],22,C.muted,36))+
  group('expected-result',[48,590,1184,72],rect(48,590,1184,72,C.light)+txt(69,624,'预期产出：可控的第一视角 RGB 采集与退化对比系统',28,C.blue,true)+txt(69,655,'为后续视觉导航鲁棒性评测提供实验原型',22,C.muted)), 'ending')]);
for(const [stem,svg] of slides.slice(5))await fs.writeFile(path.join(out,stem+'.svg'),svg,'utf8');
console.log('All seven editable SVG pages authored.');
