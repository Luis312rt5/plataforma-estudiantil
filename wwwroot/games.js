const E=id=>document.getElementById(id);
let run=0; // se incrementa al cambiar de pantalla para cancelar rondas pendientes
const COLORS=[['rojo','#E53935'],['azul','#1E88E5'],['verde','#43A047'],['amarillo','#FDD835'],['naranja','#FB8C00'],['morado','#8E24AA']];
const FRUITS=['🍎','🍌','🍓','🍇','🍊','🐟','⭐','🎈'];
const MENU=[['colores','🎨','Colores'],['numeros','🔢','Números'],['memoria','🃏','Memoria'],['vocales','🔤','Vocales'],['formas','🔷','Formas'],['sumas','➕','Sumas'],['patrones','🧩','Patrones'],['logros','🏅','Mis logros']];
const sh=a=>[...a].sort(()=>Math.random()-.5);
function say(t){try{speechSynthesis.cancel();const u=new SpeechSynthesisUtterance(t);u.lang='es-ES';speechSynthesis.speak(u)}catch{}}

async function startPlay(){await refreshStars();
  if(!E('gback'))E('game').insertAdjacentHTML('beforebegin','<p><button class="ghost hide" id="gback" onclick="menu()">⬅ Volver al menú</button></p>');
  menu()}
async function refreshStars(){try{E('stars').textContent='⭐ '+(await api('child/stars')).total}catch{}}
function menu(){run++;try{speechSynthesis.cancel()}catch{}const gb=E('gback');if(gb)gb.classList.add('hide');E('game').innerHTML='<div class="grid" style="margin:20px 0">'+MENU
  .map(([k,i,n])=>`<div class="kid"><button class="big" onclick="play('${k}')" aria-label="${n}">${i}</button>${n}</div>`).join('')+'</div>'}
function play(k){run++;const gb=E('gback');if(gb)gb.classList.toggle('hide',k==='logros');({colores,numeros,memoria,vocales,formas,sumas,patrones,logros})[k]()}

function rounds(game,n,make){let i=0,err=0;const my=run;
  const next=()=>{if(my!==run)return;if(i>=n)return finish(game,err<=1?3:err<=3?2:1);
    make(i++,()=>{chime();say('¡Muy bien!');setTimeout(next,700)},()=>{err++;buzz();say('Intenta otra vez')})};
  next()}

function colores(){rounds('colores',5,(i,ok,bad)=>{const o=sh(COLORS).slice(0,3),t=o[0],q='Toca el color '+t[0];say(q);
  E('game').innerHTML=`<h2>${q}</h2><div class="grid">${sh(o).map(c=>`<button class="big" data-c="${c[0]}" style="background:${c[1]};height:110px" aria-label="${c[0]}"></button>`).join('')}</div>`;
  E('game').querySelectorAll('button').forEach(b=>b.onclick=()=>b.dataset.c===t[0]?ok():bad())})}

function numeros(){rounds('numeros',5,(i,ok,bad)=>{const n=1+Math.floor(Math.random()*10),f=sh(FRUITS)[0];
  const others=sh([...Array(10)].map((_,k)=>k+1).filter(x=>x!==n)).slice(0,2),opts=sh([n,...others]);say('¿Cuántos hay?');
  E('game').innerHTML=`<h2>¿Cuántos hay?</h2><div style="font-size:2.5rem;margin:12px">${f.repeat(n)}</div><div class="grid">${opts.map(x=>`<button class="big" data-n="${x}">${x}</button>`).join('')}</div>`;
  E('game').querySelectorAll('button').forEach(b=>b.onclick=()=>+b.dataset.n===n?ok():bad())})}

function memoria(){const cards=sh(sh(FRUITS).slice(0,4).flatMap(x=>[x,x]));const my=run;let open=[],moves=0,found=0,lock=false;
  E('game').innerHTML=`<h2>Encuentra las parejas</h2><div class="grid" id="mem">${cards.map((c,i)=>`<button class="big" data-i="${i}">❓</button>`).join('')}</div>`;
  say('Encuentra las parejas');
  E('mem').querySelectorAll('button').forEach(b=>b.onclick=()=>{
    if(lock||b.disabled||open.includes(b))return;b.textContent=cards[b.dataset.i];open.push(b);
    if(open.length<2)return;moves++;const[a,c]=open;
    if(cards[a.dataset.i]===cards[c.dataset.i]){a.disabled=c.disabled=true;open=[];chime();
      if(++found===4){say('¡Lo lograste!');setTimeout(()=>my===run&&finish('memoria',moves<=6?3:moves<=9?2:1),800)}}
    else{lock=true;setTimeout(()=>{a.textContent=c.textContent='❓';open=[];lock=false},900)}})}


async function finish(game,stars){const my=run;const gb=E('gback');if(gb)gb.classList.add('hide');let nb=[];
  try{nb=(await api('child/play','POST',{game,stars})).newBadges||[]}catch{}
  await refreshStars();if(my!==run)return;confetti();fanfare();say('¡Ganaste '+stars+' estrellas!');
  E('game').innerHTML=`<div style="font-size:3.2rem">${[...Array(stars)].map((_,i)=>`<span class="pop" style="animation-delay:${i*.25}s">⭐</span>`).join('')}</div><h2>¡Muy bien!</h2>`
   +(nb.length?`<div class="card" style="border-color:var(--sun)"><h2>🎉 ¡Nuevo logro!</h2>${nb.map(b=>`<div class="pop" style="animation-delay:.9s"><div style="font-size:3.5rem">${b.emoji}</div><b>${b.name}</b></div>`).join('')}</div>`:'')
   +`<button onclick="menu()">Seguir jugando</button>`;
  if(nb.length)setTimeout(()=>{say('¡Nuevo logro! '+nb[0].name);fanfare()},1500)}

async function logros(){let bs=[];try{bs=await api('child/badges')}catch{}
  E('game').innerHTML=`<h2>🏅 Mis logros</h2><div class="grid">${bs.map(b=>`<div class="kid" style="${b.got?'':'opacity:.35;filter:grayscale(1)'}"><div class="big">${b.got?b.emoji:'🔒'}</div>${b.name}</div>`).join('')}</div><p><button onclick="menu()">⬅ Volver</button></p>`}

/* Celebraciones: confeti, sonidos con WebAudio (sin archivos) y animación de estrellas */
let AC;
function tone(seq){try{AC=AC||new(window.AudioContext||window.webkitAudioContext)();let t=AC.currentTime;
  seq.forEach(([f,d])=>{const o=AC.createOscillator(),g=AC.createGain();o.type='triangle';o.frequency.value=f;
    g.gain.setValueAtTime(.18,t);g.gain.exponentialRampToValueAtTime(.001,t+d);o.connect(g);g.connect(AC.destination);o.start(t);o.stop(t+d);t+=d*.8})}catch{}}
const chime=()=>tone([[660,.12],[880,.18]]),buzz=()=>tone([[220,.2]]),fanfare=()=>tone([[523,.12],[659,.12],[784,.12],[1047,.3]]);
function confetti(){const c=['#E5483A','#FFC233','#1F8F5F','#3566D6','#8E44C7'];
  for(let i=0;i<70;i++){const d=document.createElement('i');d.className='cf';
    d.style.cssText=`left:${Math.random()*100}vw;background:${c[i%5]};animation-delay:${Math.random()*.6}s;animation-duration:${1.8+Math.random()*1.4}s;width:${8+Math.random()*8}px;height:${12+Math.random()*10}px`;
    document.body.appendChild(d);setTimeout(()=>d.remove(),4200)}}
document.head.insertAdjacentHTML('beforeend','<style>.cf{position:fixed;top:-30px;z-index:50;pointer-events:none;border-radius:2px;animation:fall linear forwards}@keyframes fall{to{transform:translateY(110vh) rotate(720deg)}}.pop{display:inline-block;animation:pop .5s cubic-bezier(.3,1.6,.5,1) both}@keyframes pop{from{transform:scale(0) rotate(-90deg)}to{transform:scale(1)}}@media (prefers-reduced-motion:reduce){.cf{display:none}.pop{animation:none}}</style>');

/* ---- Juegos nuevos (un poco más difíciles: las rondas suben de nivel) ---- */
const mix=(c,w)=>{const o=sh([c,...w]);return[o,o.indexOf(c)]};
function pick(q,body,opts,right,ok,bad){say(q);
  E('game').innerHTML=`<h2>${q}</h2>${body}<div class="grid">${opts.map((o,i)=>`<button class="big" data-v="${i}">${o}</button>`).join('')}</div>`;
  E('game').querySelectorAll('button').forEach(b=>b.onclick=()=>+b.dataset.v===right?ok():bad())}

const WORDS=[['🐝','A','abeja'],['✈️','A','avión'],['🐘','E','elefante'],['⭐','E','estrella'],['🏝️','I','isla'],['🧲','I','imán'],['🐻','O','oso'],['🐑','O','oveja'],['🍇','U','uva'],['🦄','U','unicornio']];
function vocales(){rounds('vocales',5,(i,ok,bad)=>{const[e,v,n]=sh(WORDS)[0];
  const[o,r]=mix(v,sh(['A','E','I','O','U'].filter(x=>x!==v)).slice(0,i<2?1:2));
  pick('¿Con qué vocal empieza '+n+'?',`<div style="font-size:4rem">${e}</div>`,o,r,ok,bad)})}

const SHAPES=[['🔴','el círculo'],['🟦','el cuadrado'],['🔺','el triángulo'],['⭐','la estrella'],['❤️','el corazón'],['🔶','el rombo']];
function formas(){rounds('formas',5,(i,ok,bad)=>{const s=sh(SHAPES),t=s[0];
  const[o,r]=mix(t[0],s.slice(1,i<2?3:4).map(x=>x[0]));pick('Toca '+t[1],'',o,r,ok,bad)})}

function sumas(){rounds('sumas',5,(i,ok,bad)=>{const max=i<2?5:10,a=1+Math.floor(Math.random()*(max-1)),b=1+Math.floor(Math.random()*(max-a)),c=a+b;
  const w=sh([...Array(max)].map((_,k)=>k+1).filter(x=>x!==c)).slice(0,2),[o,r]=mix(c,w),f=sh(FRUITS)[0];
  pick(`¿Cuánto es ${a} más ${b}?`,i<2?`<div style="font-size:2.2rem">${f.repeat(a)} ➕ ${f.repeat(b)}</div>`:`<div style="font-size:2.5rem;font-weight:800">${a} + ${b} = ?</div>`,o,r,ok,bad)})}

const ICONS=['🔴','🔵','🟢','🟡','🟣'];
function patrones(){rounds('patrones',5,(i,ok,bad)=>{const k=i<2?2:3,base=sh(ICONS).slice(0,k),L=k*2+Math.floor(Math.random()*k);
  const seq=[...Array(L)].map((_,x)=>base[x%k]),nx=base[L%k];
  const[o,r]=mix(nx,sh(ICONS.filter(x=>x!==nx)).slice(0,2));
  pick('¿Qué sigue?',`<div style="font-size:2.6rem;margin:10px">${seq.join(' ')} ❓</div>`,o,r,ok,bad)})}
