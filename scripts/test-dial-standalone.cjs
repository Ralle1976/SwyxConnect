const{spawn}=require('child_process');
const{createInterface}=require('readline');
const{readFileSync}=require('fs');
const envContent=readFileSync('.env','utf8');
const env={};
for(const line of envContent.split('\n')){const t=line.trim();if(!t||t.startsWith('#'))continue;const eq=t.indexOf('=');if(eq<0)continue;env[t.substring(0,eq).trim()]=t.substring(eq+1).trim();}
const proc=spawn('out/bridge/SwyxMessenger.exe',[
  '--server',env.SWYX_SERVER,
  '--public-server',env.SWYX_PUBLIC_SERVER||'yourserver.example.com:15021',
  '--user',env.SWYX_USERNAME,
  '--password',env.SWYX_PASSWORD,
  '--auth-mode','1'
],{stdio:['pipe','pipe','pipe'],windowsHide:true});
proc.stderr.on('data',d=>d.toString().split('\n').filter(l=>l.trim()).forEach(l=>{
  let s=l;if(env.SWYX_USERNAME)s=s.split(env.SWYX_USERNAME).join('<U>');if(env.SWYX_PASSWORD)s=s.split(env.SWYX_PASSWORD).join('<P>');
  if(s.includes('Audio')||s.includes('Dial')||s.includes('Line')||s.includes('RC-Login')||s.includes('Error')||s.includes('Apply')||s.includes('ComSocket')||s.includes('LoginDevice')) console.log('[B]',s);
}));
let id=1;const pending=new Map();
const rl=createInterface({input:proc.stdout});
rl.on('line',line=>{try{const o=JSON.parse(line);if(o.id&&pending.has(o.id)){pending.get(o.id)(o);pending.delete(o.id);}}catch(e){}});
function send(m,p={}){return new Promise((r,j)=>{const i=id++;pending.set(i,r);proc.stdin.write(JSON.stringify({jsonrpc:'2.0',id:i,method:m,params:p})+'\n');setTimeout(()=>{if(pending.has(i)){pending.delete(i);j('timeout');}},15000);});}
async function run(){
  console.log('Wait 18s for RC-Login + Audio...');
  await new Promise(r=>setTimeout(r,18000));
  const si=await send('getSystemInfo');
  console.log('lines:',si.result?.numberOfLines,'ctiMaster:',si.result?.isCtiMaster,'audioMode:',si.result?.audioMode);

  // Force 2 lines
  await send('setNumberOfLines',{count:2});
  const si2=await send('getSystemInfo');
  console.log('lines after set:',si2.result?.numberOfLines);

  const ad=await send('getAudioDevices');
  console.log('HF:',JSON.stringify(ad.result?.handsfree?.playback));
  console.log('\nDial 99 (10s):');
  await send('dial',{number:'99'});
  for(let i=1;i<=10;i++){await new Promise(r=>setTimeout(r,1000));const l=await send('getLines');const s=l.result?.lines?.[0];console.log(i+'s:',s?.state,s?.callerNumber||'');}
  await send('hangup',{lineId:0});
  console.log('\nSwyxIt running?');
  try{require('child_process').execSync('tasklist /FI "IMAGENAME eq SwyxIt!.exe" 2>nul | findstr SwyxIt',{stdio:'pipe'});console.log('YES');}catch{console.log('NO - STANDALONE!');}
  proc.kill();process.exit(0);
}
run().catch(e=>{console.error('Error:',e.message);proc.kill();process.exit(1);});
setTimeout(()=>{proc.kill();process.exit(0);},55000);
